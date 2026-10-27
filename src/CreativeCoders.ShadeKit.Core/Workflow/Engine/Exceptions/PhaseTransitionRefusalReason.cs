namespace CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;

/// <summary>
/// Specifies why a <c>phase start</c> or <c>phase approve</c> request is not an allowed transition.
/// </summary>
public enum PhaseTransitionRefusalReason
{
    /// <summary>
    /// The requested phase comes more than one phase after the current phase, so the transition would skip phases.
    /// </summary>
    SkipsPhases,

    /// <summary>
    /// A transition to another phase is pending, and the requested phase is neither its target nor the current phase.
    /// </summary>
    OtherTransitionPending,

    /// <summary>
    /// The requested phase is an earlier phase, but a transition is pending, so it cannot be reopened now.
    /// </summary>
    ReopenWhilePending,

    /// <summary>
    /// The requested phase is an earlier phase that its profile does not allow to reopen.
    /// </summary>
    PhaseCannotBeReopened
}
