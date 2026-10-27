namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Specifies what a <c>phase start</c> request did.
/// </summary>
public enum PhaseStartOutcome
{
    /// <summary>
    /// A transition to the next phase or a reopen of an earlier phase was requested and waits for an approval.
    /// Written as <c>transition-requested</c>.
    /// </summary>
    TransitionRequested,

    /// <summary>
    /// The transition to the requested phase was already pending; nothing changed. Written as
    /// <c>transition-pending</c>.
    /// </summary>
    TransitionPending,

    /// <summary>
    /// The gate of the next phase needs no approval, so the transition was performed at once. Written as
    /// <c>transition-performed</c>.
    /// </summary>
    TransitionPerformed,

    /// <summary>
    /// The requested phase is the current phase and in progress; nothing changed. Written as
    /// <c>already-in-progress</c>.
    /// </summary>
    AlreadyInProgress,

    /// <summary>
    /// The requested phase is the current phase, and a transition away from it is pending; nothing changed. Written
    /// as <c>resume-pending</c>.
    /// </summary>
    ResumePending
}
