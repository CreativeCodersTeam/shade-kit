using System.Runtime.CompilerServices;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
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
/// <param name="workflowProfileCatalog">The catalog that the profiles and phases of the entries are checked against.</param>
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

        return GetOrStartRead(store, cancellationToken).WaitAsync(cancellationToken);
    }

    // The table only holds the lazy read; the read itself starts outside the lock. A read that failed or was canceled
    // is replaced, so only calls that join it while it is in progress share its failure.
    private Task<GatesConfigReadResult> GetOrStartRead(IKitStore store, CancellationToken cancellationToken)
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
                read = new Lazy<Task<GatesConfigReadResult>>(() => ReadCoreAsync(store, cancellationToken));
                _reads.AddOrUpdate(store, read);
            }
        }

        return read.Value;
    }

    private static bool HasFailed(Lazy<Task<GatesConfigReadResult>> read)
    {
        return read is { IsValueCreated: true, Value: { IsFaulted: true } or { IsCanceled: true } };
    }

    private async Task<GatesConfigReadResult> ReadCoreAsync(IKitStore store, CancellationToken cancellationToken)
    {
        var readResult = await store
            .ReadJsonAsync<GatesConfig>(KitStorePaths.GatesFileName, FormatIds.Gates, FormatAccess.Read,
                cancellationToken)
            .ConfigureAwait(false);

        if (readResult is null)
        {
            return new GatesConfigReadResult(GatesConfig.CreateDefault(),
                [new GatesDefaultsUsedWarning(IsFileMissing: true)]);
        }

        var config = readResult.Document;
        List<KitWarning> gateWarnings = [];

        if (config.Profiles is null)
        {
            config.Profiles = GatesConfig.CreateDefault().Profiles;
            gateWarnings.Add(new GatesDefaultsUsedWarning(IsFileMissing: false));
        }
        else
        {
            gateWarnings.AddRange(CheckEntries(config.Profiles));
        }

        return new GatesConfigReadResult(config, KitWarningOrder.Arrange([.. readResult.Warnings, .. gateWarnings]));
    }

    // The deserializer adds the entries in file order and nothing removes one, so the dictionaries enumerate in file
    // order.
    private IEnumerable<GateEntryIgnoredWarning> CheckEntries(
        Dictionary<string, Dictionary<string, GateConfig>> profiles)
    {
        foreach (var (profileName, gates) in profiles)
        {
            if (!_workflowProfileCatalog.TryGetProfile(profileName, out var profile))
            {
                yield return new GateEntryIgnoredWarning(profileName, null, GateEntryIgnoredReason.UnknownProfile);

                continue;
            }

            foreach (var phaseName in gates.Keys)
            {
                var phaseIndex = profile.IndexOf(phaseName);

                if (phaseIndex < 0)
                {
                    yield return new GateEntryIgnoredWarning(profileName, phaseName,
                        GateEntryIgnoredReason.UnknownPhase);
                }
                else if (phaseIndex == 0)
                {
                    yield return new GateEntryIgnoredWarning(profileName, phaseName,
                        GateEntryIgnoredReason.FirstPhase);
                }
            }
        }
    }
}
