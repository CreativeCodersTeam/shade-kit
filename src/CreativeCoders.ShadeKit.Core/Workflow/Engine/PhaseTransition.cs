namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents a transition from one phase of a feature to another.
/// </summary>
/// <param name="Kind">One of the enumeration values that specifies the direction of the transition.</param>
/// <param name="From">The name of the phase the transition starts in, the current phase.</param>
/// <param name="To">The name of the target phase of the transition.</param>
public sealed record PhaseTransition(PhaseTransitionKind Kind, string From, string To);
