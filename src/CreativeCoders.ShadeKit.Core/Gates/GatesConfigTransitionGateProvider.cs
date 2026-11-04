using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Provides the gates of transitions from the gate configuration <c>.shadekit/gates.json</c> of a SHADE kit store.
/// </summary>
/// <remarks>
/// <para>
/// The provider reads the file once per store instance, checks its entries against the known workflow profiles, and
/// keeps a read-only view of the gates that apply. The profile and phase names are matched ordinally. A profile or a
/// phase without an applicable entry has no gate in the result; the <see cref="IPhaseEngine" /> gives such a
/// transition <see cref="TransitionGate.Default" />.
/// </para>
/// <para>
/// The views are held in a <see cref="ConditionalWeakTable{TKey, TValue}" />, so a cached view lives only as long as
/// its store, which lives for one command.
/// </para>
/// </remarks>
/// <param name="workflowProfileCatalog">
/// The catalog that the profiles and phases of the entries are checked against.
/// </param>
public sealed class GatesConfigTransitionGateProvider(IWorkflowProfileCatalog workflowProfileCatalog)
    : ITransitionGateProvider
{
    private readonly IWorkflowProfileCatalog _workflowProfileCatalog = Ensure.NotNull(workflowProfileCatalog);

    private readonly Lock _lock = new Lock();

    private readonly ConditionalWeakTable<IKitStore, Lazy<Task<GatesView>>> _reads =
        new ConditionalWeakTable<IKitStore, Lazy<Task<GatesView>>>();

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The file is read with <see cref="FormatAccess.Read" />. A missing file or a missing <c>profiles</c> field gives
    /// the profiles of <see cref="GatesConfig.CreateDefault" /> and a <see cref="GatesDefaultsUsedWarning" />. An
    /// explicit empty <c>profiles</c> field is used as it is, without defaults and without a warning. Every entry of
    /// the whole file, also of the profiles other than <paramref name="profile" />, that names an unknown profile, a
    /// phase that its profile does not have, or the first phase of its profile gives a
    /// <see cref="GateEntryIgnoredWarning" />, in file order, and has no gate in the result; the entry stays in the
    /// file.
    /// </para>
    /// <para>
    /// Of the gates into the phases after the first one of a known profile that name the same checklist (compared
    /// without regard to case on every operating system, because a file system that ignores case stores both
    /// spellings as one file), the gate into the earliest phase of the profile keeps it. Every later one gets a
    /// <see cref="GateEntryIgnoredWarning" /> with <see cref="GateEntryIgnoredReason.SharedChecklist" />, after the
    /// other warnings of its profile and in phase order, and has no checklist in the result; its approval still
    /// applies. So two gates never share one filled copy in a feature folder. The file stays valid.
    /// </para>
    /// <para>
    /// The configuration is cached per store instance: a later call with the same store returns the same gates and
    /// warnings without reading the file again, and a call with another store instance reads the file again. The gates
    /// are a read-only view, so no caller can change the cached configuration. Calls that run while a read of the same
    /// store is in progress share that read and its outcome. The shared read is not tied to the cancellation token of
    /// any call: a canceled call stops only its own wait, and the other calls still get the result. A failed read is
    /// not cached, so a call after it reads the file again.
    /// </para>
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the applicable configured
    /// gates of <paramref name="profile" />, keyed by the name of the target phase in file order, with the checklist
    /// path unchanged unless it is ignored as a shared checklist, or an empty dictionary if <c>gates.json</c> has no
    /// entry for the profile; and the warnings of reading the whole configuration: the format warnings first, then the
    /// gate warnings.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    /// <exception cref="InvalidKitFileException">
    /// <c>gates.json</c>, or <c>config.json</c> when it is read for the setting, is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// <c>gates.json</c>, or <c>config.json</c> when it is read for the setting, uses a format version newer than the
    /// engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// <c>gates.json</c> uses an older format version and <c>outdatedFormatOnRead</c> is <c>stop</c>.
    /// </exception>
    public async Task<WorkflowProfileGates> GetGatesAsync(IKitStore store, WorkflowProfile profile,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(profile);

        cancellationToken.ThrowIfCancellationRequested();

        var view = await GetOrStartRead(store).WaitAsync(cancellationToken).ConfigureAwait(false);

        var gates = view.Profiles.GetValueOrDefault(profile.Name) ?? ReadOnlyDictionary<string, TransitionGate>.Empty;

        return new WorkflowProfileGates(gates, view.Warnings);
    }

    // The table only holds the lazy read; the read itself starts outside the lock. The shared read takes no caller's
    // token, so a caller that is canceled stops only its own wait and every other caller still gets the result. A read
    // that failed is replaced, so only calls that join it while it is in progress share its failure.
    private Task<GatesView> GetOrStartRead(IKitStore store)
    {
        Lazy<Task<GatesView>> read;

        lock (_lock)
        {
            if (_reads.TryGetValue(store, out var cachedRead) && !HasFailed(cachedRead))
            {
                read = cachedRead;
            }
            else
            {
                read = new Lazy<Task<GatesView>>(() => ReadCoreAsync(store));
                _reads.AddOrUpdate(store, read);
            }
        }

        return read.Value;
    }

    private static bool HasFailed(Lazy<Task<GatesView>> read)
    {
        return read is { IsValueCreated: true, Value: { IsFaulted: true } or { IsCanceled: true } };
    }

    private async Task<GatesView> ReadCoreAsync(IKitStore store)
    {
        var readResult = await store
            .ReadJsonAsync<GatesConfig>(KitStorePaths.GatesFileName, FormatIds.Gates, FormatAccess.Read,
                CancellationToken.None)
            .ConfigureAwait(false);

        var profiles = readResult?.Document.Profiles;
        KitWarning? defaultsWarning = profiles is null
            ? new GatesDefaultsUsedWarning(IsFileMissing: readResult is null)
            : null;

        return CreateView(profiles ?? GatesConfig.CreateDefault().Profiles!, readResult?.Warnings ?? [],
            defaultsWarning);
    }

    // The view holds read-only dictionaries, never the document, so no caller can change the cached configuration.
    // The deserializer adds the entries in file order and the view is filled in the same order, so the dictionaries
    // enumerate in file order.
    private GatesView CreateView(Dictionary<string, Dictionary<string, GateConfig>> profiles,
        IReadOnlyList<KitWarning> formatWarnings, KitWarning? defaultsWarning)
    {
        var view = new Dictionary<string, IReadOnlyDictionary<string, TransitionGate>>(profiles.Count,
            StringComparer.Ordinal);
        List<KitWarning> gateWarnings = defaultsWarning is null ? [] : [defaultsWarning];

        foreach (var (profileName, gates) in profiles)
        {
            if (!_workflowProfileCatalog.TryGetProfile(profileName, out var profile))
            {
                gateWarnings.Add(new GateEntryIgnoredWarning(profileName, null, GateEntryIgnoredReason.UnknownProfile));

                continue;
            }

            gateWarnings.AddRange(CheckPhases(profileName, profile, gates));

            var sharedChecklistPhases = FindSharedChecklistPhases(profile, gates);

            gateWarnings.AddRange(sharedChecklistPhases.Select(phaseName =>
                new GateEntryIgnoredWarning(profileName, phaseName, GateEntryIgnoredReason.SharedChecklist)));

            view[profileName] = CreateGatesView(profile, gates, sharedChecklistPhases);
        }

        return new GatesView(view.AsReadOnly(), KitWarningOrder.Arrange([.. formatWarnings, .. gateWarnings]));
    }

    private static IEnumerable<GateEntryIgnoredWarning> CheckPhases(string profileName, WorkflowProfile profile,
        Dictionary<string, GateConfig> gates)
    {
        foreach (var phaseName in gates.Keys)
        {
            var phaseIndex = profile.IndexOf(phaseName);

            if (phaseIndex < 0)
            {
                yield return new GateEntryIgnoredWarning(profileName, phaseName, GateEntryIgnoredReason.UnknownPhase);
            }
            else if (phaseIndex == 0)
            {
                yield return new GateEntryIgnoredWarning(profileName, phaseName, GateEntryIgnoredReason.FirstPhase);
            }
        }
    }

    // Only the gates that apply, those into a phase after the first one, count. Of the gates that name the same
    // checklist, the gate into the earliest phase of the profile keeps it; the later ones are returned in phase order.
    // The paths are compared without regard to case on every operating system: on a file system that ignores case,
    // such as the default ones of macOS and Windows, checks/dor.md and checks/DoR.md are one file, and a gates.json
    // must give the same gates on every client.
    private static IReadOnlyList<string> FindSharedChecklistPhases(WorkflowProfile profile,
        Dictionary<string, GateConfig> gates)
    {
        var namedChecklists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. profile.Phases
                .Skip(1)
                .Select(phase => phase.Name)
                .Where(phaseName => gates.TryGetValue(phaseName, out var gate)
                                    && gate.Checklist is { } checklist
                                    && !namedChecklists.Add(checklist))
        ];
    }

    // Only the gates that apply get an entry: an entry for an unknown phase or for the first phase of the profile is
    // left out, so the engine gives that transition the default gate.
    private static ReadOnlyDictionary<string, TransitionGate> CreateGatesView(WorkflowProfile profile,
        Dictionary<string, GateConfig> gates, IReadOnlyList<string> phasesWithoutChecklist)
    {
        var view = new Dictionary<string, TransitionGate>(gates.Count, StringComparer.Ordinal);

        foreach (var (phaseName, gate) in gates.Where(gate => profile.IndexOf(gate.Key) > 0))
        {
            var checklist = phasesWithoutChecklist.Contains(phaseName, StringComparer.Ordinal) ? null : gate.Checklist;

            view[phaseName] = new TransitionGate(gate.Approval, checklist);
        }

        return view.AsReadOnly();
    }

    // The gates of every known profile of a store, keyed by profile name, and the warnings of reading the whole
    // configuration, arranged with KitWarningOrder.
    private sealed record GatesView(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, TransitionGate>> Profiles,
        IReadOnlyList<KitWarning> Warnings);
}
