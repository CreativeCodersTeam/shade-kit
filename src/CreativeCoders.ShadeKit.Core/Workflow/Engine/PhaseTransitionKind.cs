namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Specifies the direction of a transition between two phases of a workflow profile.
/// </summary>
public enum PhaseTransitionKind
{
    /// <summary>
    /// The transition leads to the phase that follows the current phase. Written as <c>forward</c>.
    /// </summary>
    Forward,

    /// <summary>
    /// The transition leads back to an earlier phase, which is reopened. Written as <c>reopen</c>.
    /// </summary>
    Reopen
}
