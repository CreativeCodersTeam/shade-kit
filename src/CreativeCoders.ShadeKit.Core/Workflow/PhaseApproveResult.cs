using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents the result of <see cref="IPhaseWorkflow.ApproveAsync" />.
/// </summary>
/// <param name="FeatureId">The id of the current feature, which is the name of its folder.</param>
/// <param name="Transition">The transition that was approved.</param>
/// <param name="CurrentPhase">The target phase of the transition, which is now the current phase, in progress.</param>
/// <param name="Warnings">The format warnings first, then the other warnings, each only once.</param>
public sealed record PhaseApproveResult(
    string FeatureId,
    PhaseTransition Transition,
    PhaseSnapshot CurrentPhase,
    IReadOnlyList<KitWarning> Warnings);
