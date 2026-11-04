using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Provides the gates of transitions from the gate configuration <c>.shadekit/gates.json</c> of a SHADE kit store.
/// </summary>
/// <remarks>
/// The configuration is read through an <see cref="IGatesConfigReader" />, which reads the file once per store. The
/// profile and phase names are matched ordinally. A transition whose profile or target phase has no entry gets
/// <see cref="TransitionGate.Default" />. The warnings of reading the configuration are returned with the gates, as
/// the <see cref="IGatesConfigReader" /> reports them.
/// </remarks>
/// <param name="gatesConfigReader">The reader of the gate configuration.</param>
public sealed class GatesConfigTransitionGateProvider(IGatesConfigReader gatesConfigReader) : ITransitionGateProvider
{
    private readonly IGatesConfigReader _gatesConfigReader = Ensure.NotNull(gatesConfigReader);

    /// <inheritdoc />
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains, for each phase of
    /// <paramref name="profile" />, the configured gate of the transition into it, with its checklist path unchanged
    /// unless the reader ignores the checklist, or <see cref="TransitionGate.Default" /> if <c>gates.json</c> has no
    /// entry for the profile or the phase; and the warnings of <see cref="IGatesConfigReader.ReadAsync" /> unchanged.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    /// <remarks>
    /// The configuration is read once per call. Every exception of <see cref="IGatesConfigReader.ReadAsync" />, for
    /// example for an invalid <c>gates.json</c>, is passed on unchanged.
    /// </remarks>
    public async Task<WorkflowProfileGates> GetGatesAsync(IKitStore store, WorkflowProfile profile,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(profile);

        var readResult = await _gatesConfigReader.ReadAsync(store, cancellationToken).ConfigureAwait(false);

        readResult.Profiles.TryGetValue(profile.Name, out var configuredGates);

        var gates = new Dictionary<string, TransitionGate>(profile.Phases.Count, StringComparer.Ordinal);

        foreach (var phaseName in profile.Phases.Select(phase => phase.Name))
        {
            gates[phaseName] = configuredGates is not null && configuredGates.TryGetValue(phaseName, out var gate)
                ? gate
                : TransitionGate.Default;
        }

        return new WorkflowProfileGates(gates.AsReadOnly(), readResult.Warnings);
    }
}
