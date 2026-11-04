using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Reads the gate configuration <c>.shadekit/gates.json</c> of a SHADE kit store once per store instance and checks
/// its entries against the known workflow profiles.
/// </summary>
/// <remarks>
/// The results are held in a <see cref="ConditionalWeakTable{TKey, TValue}" />, so a cached result lives only as long
/// as its store, which lives for one command.
/// </remarks>
/// <param name="workflowProfileCatalog">
/// The catalog that the profiles and phases of the entries are checked against.
/// </param>
public sealed class GatesConfigReader(IWorkflowProfileCatalog workflowProfileCatalog) : IGatesConfigReader
{
    private readonly IWorkflowProfileCatalog _workflowProfileCatalog = Ensure.NotNull(workflowProfileCatalog);

    private readonly Lock _lock = new Lock();

    private readonly ConditionalWeakTable<IKitStore, Lazy<Task<GatesConfigReadResult>>> _reads =
        new ConditionalWeakTable<IKitStore, Lazy<Task<GatesConfigReadResult>>>();

    /// <inheritdoc />
    public Task<GatesConfigReadResult> ReadAsync(IKitStore store, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        cancellationToken.ThrowIfCancellationRequested();

        return GetOrStartRead(store).WaitAsync(cancellationToken);
    }

    // The table only holds the lazy read; the read itself starts outside the lock. The shared read takes no caller's
    // token, so a caller that is canceled stops only its own wait and every other caller still gets the result. A read
    // that failed is replaced, so only calls that join it while it is in progress share its failure.
    private Task<GatesConfigReadResult> GetOrStartRead(IKitStore store)
    {
        Lazy<Task<GatesConfigReadResult>> read;

        lock (_lock)
        {
            if (_reads.TryGetValue(store, out var cachedRead) && !HasFailed(cachedRead))
            {
                read = cachedRead;
            }
            else
            {
                read = new Lazy<Task<GatesConfigReadResult>>(() => ReadCoreAsync(store));
                _reads.AddOrUpdate(store, read);
            }
        }

        return read.Value;
    }

    private static bool HasFailed(Lazy<Task<GatesConfigReadResult>> read)
    {
        return read is { IsValueCreated: true, Value: { IsFaulted: true } or { IsCanceled: true } };
    }

    private async Task<GatesConfigReadResult> ReadCoreAsync(IKitStore store)
    {
        var readResult = await store
            .ReadJsonAsync<GatesConfig>(KitStorePaths.GatesFileName, FormatIds.Gates, FormatAccess.Read,
                CancellationToken.None)
            .ConfigureAwait(false);

        if (readResult is null)
        {
            return CreateResult(GatesConfig.CreateDefault().Profiles!, [],
                [new GatesDefaultsUsedWarning(IsFileMissing: true)]);
        }

        if (readResult.Document.Profiles is not { } profiles)
        {
            return CreateResult(GatesConfig.CreateDefault().Profiles!, readResult.Warnings,
                [new GatesDefaultsUsedWarning(IsFileMissing: false)]);
        }

        return CreateResult(profiles, readResult.Warnings, []);
    }

    // The result holds a read-only view of the profiles, never the document, so no caller can change the cached
    // configuration. The deserializer adds the entries in file order and the view is filled in the same order, so the
    // dictionaries enumerate in file order.
    private GatesConfigReadResult CreateResult(Dictionary<string, Dictionary<string, GateConfig>> profiles,
        IReadOnlyList<KitWarning> formatWarnings, IReadOnlyList<KitWarning> defaultsWarnings)
    {
        var view = new Dictionary<string, IReadOnlyDictionary<string, TransitionGate>>(profiles.Count,
            StringComparer.Ordinal);
        List<KitWarning> gateWarnings = [.. defaultsWarnings];

        foreach (var (profileName, gates) in profiles)
        {
            if (!_workflowProfileCatalog.TryGetProfile(profileName, out var profile))
            {
                gateWarnings.Add(new GateEntryIgnoredWarning(profileName, null, GateEntryIgnoredReason.UnknownProfile));
                view[profileName] = CreateGatesView(gates, []);

                continue;
            }

            gateWarnings.AddRange(CheckPhases(profileName, profile, gates));

            var sharedChecklistPhases = FindSharedChecklistPhases(profile, gates);

            gateWarnings.AddRange(sharedChecklistPhases.Select(phaseName =>
                new GateEntryIgnoredWarning(profileName, phaseName, GateEntryIgnoredReason.SharedChecklist)));

            view[profileName] = CreateGatesView(gates, sharedChecklistPhases);
        }

        return new GatesConfigReadResult(view.AsReadOnly(),
            KitWarningOrder.Arrange([.. formatWarnings, .. gateWarnings]));
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

    private static ReadOnlyDictionary<string, TransitionGate> CreateGatesView(Dictionary<string, GateConfig> gates,
        IReadOnlyList<string> phasesWithoutChecklist)
    {
        var view = new Dictionary<string, TransitionGate>(gates.Count, StringComparer.Ordinal);

        foreach (var (phaseName, gate) in gates)
        {
            var checklist = phasesWithoutChecklist.Contains(phaseName, StringComparer.Ordinal) ? null : gate.Checklist;

            view[phaseName] = new TransitionGate(gate.Approval, checklist);
        }

        return view.AsReadOnly();
    }
}
