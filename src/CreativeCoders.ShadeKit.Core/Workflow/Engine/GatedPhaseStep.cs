namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents a possible next step into a phase through its gate: the pending transition or the transition to the
/// next phase.
/// </summary>
/// <param name="Phase">The name of the target phase.</param>
/// <param name="Gate">The gate of the transition into the target phase.</param>
/// <param name="Skill">The name of the phase skill of the target phase.</param>
public sealed record GatedPhaseStep(string Phase, TransitionGate Gate, string Skill);
