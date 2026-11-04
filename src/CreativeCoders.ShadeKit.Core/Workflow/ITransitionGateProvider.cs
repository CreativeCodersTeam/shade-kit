using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Provides the gates of the transitions into the phases of a workflow profile: whether a human must approve a
/// transition and which checklist the agent checks before it asks.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="IPhaseEngine" /> applies the gate. A reopen of an earlier phase always needs an approval and has no
/// checklist, whatever the provider returns.
/// </para>
/// <para>
/// The <see cref="TransitionGate.Checklist" /> of a gate is the value from <c>gates.json</c>: a path relative to
/// <c>.shadekit/templates/</c> that equals the path of the feature's copy relative to the feature folder, for example
/// <c>checks/definition-of-ready.md</c>.
/// </para>
/// </remarks>
public interface ITransitionGateProvider
{
    /// <summary>
    /// Gets the gates of the transitions into every phase of a workflow profile and the warnings of reading the gate
    /// configuration.
    /// </summary>
    /// <param name="store">The SHADE kit store whose gate configuration applies.</param>
    /// <param name="profile">The workflow profile of the feature.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the gate of the transition
    /// into each phase of <paramref name="profile" />, keyed by the name of the target phase, and the warnings of
    /// reading the gate configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="profile" /> is <see langword="null" />.
    /// </exception>
    Task<WorkflowProfileGates> GetGatesAsync(IKitStore store, WorkflowProfile profile,
        CancellationToken cancellationToken = default);
}
