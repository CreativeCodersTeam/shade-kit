namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Specifies the state change that a <see cref="PhaseHistoryEntry" /> records.
/// </summary>
public enum PhaseHistoryAction
{
    /// <summary>
    /// A transition to the next phase was requested. Stored as <c>start</c>.
    /// </summary>
    Start,

    /// <summary>
    /// A pending transition was approved, or a transition was performed without an approval. Stored as
    /// <c>approve</c>.
    /// </summary>
    Approve,

    /// <summary>
    /// A pending transition was rejected. Stored as <c>reject</c>.
    /// </summary>
    Reject,

    /// <summary>
    /// A transition back to an earlier phase was requested. Stored as <c>reopen</c>.
    /// </summary>
    Reopen,

    /// <summary>
    /// The last phase was finished and the feature was completed. Stored as <c>complete</c>.
    /// </summary>
    Complete
}
