using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents the result of <see cref="IPhaseWorkflow.RejectAsync" />.
/// </summary>
/// <param name="FeatureId">The id of the current feature, which is the name of its folder.</param>
/// <param name="Rejected">The transition that was rejected.</param>
/// <param name="CurrentPhase">The current phase of the feature, which is in progress again.</param>
/// <param name="Note">
/// The trimmed note that was recorded with the rejection, or <see langword="null" /> if none was given.
/// </param>
/// <param name="Warnings">The format warnings first, then the other warnings, each only once.</param>
public sealed record PhaseRejectResult(
    string FeatureId,
    PhaseTransition Rejected,
    PhaseSnapshot CurrentPhase,
    string? Note,
    IReadOnlyList<KitWarning> Warnings);
