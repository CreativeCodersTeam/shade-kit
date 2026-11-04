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
/// <see cref="TransitionGate.Default" />. The warnings of reading the configuration are not returned; they come from
/// the <see cref="IGatesConfigReader" />.
/// </remarks>
/// <param name="gatesConfigReader">The reader of the gate configuration.</param>
public sealed class GatesConfigTransitionGateProvider(IGatesConfigReader gatesConfigReader) : ITransitionGateProvider
{
    private readonly IGatesConfigReader _gatesConfigReader = Ensure.NotNull(gatesConfigReader);

    /// <inheritdoc />
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the configured gate of the
    /// transition into <paramref name="targetPhase" />, with its checklist path unchanged unless the reader ignores the
    /// checklist, or <see cref="TransitionGate.Default" /> if <c>gates.json</c> has no entry for the profile or the
    /// phase.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    /// <remarks>
    /// Every exception of <see cref="IGatesConfigReader.ReadAsync" />, for example for an invalid <c>gates.json</c>,
    /// is passed on unchanged.
    /// </remarks>
    public async Task<TransitionGate> GetGateAsync(IKitStore store, WorkflowProfile profile, string targetPhase,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(profile);
        Ensure.NotNull(targetPhase);

        var readResult = await _gatesConfigReader.ReadAsync(store, cancellationToken).ConfigureAwait(false);

        return readResult.Profiles.TryGetValue(profile.Name, out var gates)
               && gates.TryGetValue(targetPhase, out var gate)
            ? gate
            : TransitionGate.Default;
    }
}
