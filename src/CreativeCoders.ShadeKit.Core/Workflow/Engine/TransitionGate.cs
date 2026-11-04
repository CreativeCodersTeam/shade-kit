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
/// The path of the checklist of the gate as configured in <c>gates.json</c>, or <see langword="null" /> if the gate
/// has none. The path is relative to <c>.shadekit/templates/</c> and equals the path of the feature's copy relative
/// to the feature folder, for example <c>checks/definition-of-ready.md</c>.
/// </param>
public sealed record TransitionGate(bool Approval, string? Checklist)
{
    /// <summary>
    /// Gets the gate that applies when no gate is configured for a target phase, and to every reopen.
    /// </summary>
    /// <value>A gate that requires an approval and has no checklist.</value>
    public static TransitionGate Default { get; } = new(true, null);
}
