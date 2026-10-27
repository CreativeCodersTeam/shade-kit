namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the gate of a transition into a target phase: whether a human must approve the transition and which
/// checklist the agent checks before it asks.
/// </summary>
/// <param name="Approval">
/// <see langword="true" /> if a human must approve the transition; <see langword="false" /> if the transition is
/// performed at once.
/// </param>
/// <param name="Checklist">
/// The repository-relative path of the checklist of the gate, or <see langword="null" /> if the gate has none.
/// </param>
public sealed record TransitionGate(bool Approval, string? Checklist)
{
    /// <summary>
    /// Gets the gate that applies when no gate is configured for a target phase, and to every reopen.
    /// </summary>
    /// <value>A gate that requires an approval and has no checklist.</value>
    public static TransitionGate Default { get; } = new(true, null);
}
