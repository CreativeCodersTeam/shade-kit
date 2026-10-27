namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents a possible next step into a phase that needs no gate: continuing the current phase or reopening an
/// earlier phase.
/// </summary>
/// <param name="Phase">The name of the phase.</param>
/// <param name="Skill">The name of the phase skill of the phase.</param>
public sealed record PhaseStep(string Phase, string Skill);
