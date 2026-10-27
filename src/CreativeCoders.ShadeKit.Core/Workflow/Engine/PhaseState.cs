namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Specifies the state of a phase of a feature, stored as a value of the <c>phases</c> field of <c>feature.json</c>.
/// </summary>
public enum PhaseState
{
    /// <summary>
    /// Work on the phase has not started, or the phase was reset by reopening an earlier phase. Stored as
    /// <c>pending</c>.
    /// </summary>
    Pending,

    /// <summary>
    /// The phase is being worked on. Stored as <c>in-progress</c>.
    /// </summary>
    InProgress,

    /// <summary>
    /// The phase requested a transition to another phase that waits for the approval of a human. Stored as
    /// <c>awaiting-approval</c>.
    /// </summary>
    AwaitingApproval,

    /// <summary>
    /// The phase is finished and the transition out of it was approved. Stored as <c>approved</c>.
    /// </summary>
    Approved
}
