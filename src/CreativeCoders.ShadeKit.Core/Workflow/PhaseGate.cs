using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents the gate of a transition together with the phase the transition leads into.
/// </summary>
/// <param name="Phase">The name of the target phase of the transition, for example <c>map</c>.</param>
/// <param name="Gate">The gate of the transition into <paramref name="Phase" />.</param>
public sealed record PhaseGate(string Phase, TransitionGate Gate);
