namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the decision of <see cref="IPhaseEngine.Approve" />.
/// </summary>
/// <param name="Transition">The transition that was approved.</param>
/// <param name="Change">
/// The change to persist; its state has the target phase as the current phase, in progress.
/// </param>
public sealed record PhaseApproveDecision(PhaseTransition Transition, PhaseStateChange Change);
