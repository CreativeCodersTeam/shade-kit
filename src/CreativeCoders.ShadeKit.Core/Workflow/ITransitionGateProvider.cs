using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Provides the gate of a transition into a target phase: whether a human must approve the transition and which
/// checklist the agent checks before it asks.
/// </summary>
/// <remarks>
/// The <see cref="IPhaseEngine" /> applies the gate. A reopen of an earlier phase always needs an approval and has no
/// checklist, whatever the provider returns.
/// </remarks>
public interface ITransitionGateProvider
{
    /// <summary>
    /// Gets the gate of the transition into the specified phase of a workflow profile.
    /// </summary>
    /// <param name="store">The SHADE kit store whose gate configuration applies.</param>
    /// <param name="profile">The workflow profile of the feature.</param>
    /// <param name="targetPhase">The name of the phase the transition leads into.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the gate of the transition
    /// into <paramref name="targetPhase" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" />, <paramref name="profile" />, or <paramref name="targetPhase" /> is
    /// <see langword="null" />.
    /// </exception>
    Task<TransitionGate> GetGateAsync(IKitStore store, WorkflowProfile profile, string targetPhase,
        CancellationToken cancellationToken = default);
}
