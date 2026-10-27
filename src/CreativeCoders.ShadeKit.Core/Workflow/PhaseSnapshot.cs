using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents a phase together with its state at the end of a phase command.
/// </summary>
/// <param name="Name">The name of the phase, for example <c>refine</c>.</param>
/// <param name="State">One of the enumeration values that specifies the state of the phase.</param>
public sealed record PhaseSnapshot(string Name, PhaseState State);
