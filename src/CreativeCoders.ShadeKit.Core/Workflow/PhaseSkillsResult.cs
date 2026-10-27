using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents the result of <see cref="IPhaseWorkflow.GetSkillsAsync" />.
/// </summary>
/// <param name="FeatureId">The id of the current feature, which is the name of its folder.</param>
/// <param name="Profile">The name of the workflow profile of the feature.</param>
/// <param name="Phase">The name of the requested phase.</param>
/// <param name="Skills">The phase skill and the general skills of <paramref name="Phase" />.</param>
/// <param name="Warnings">The format warnings first, then the other warnings, each only once.</param>
public sealed record PhaseSkillsResult(
    string FeatureId,
    string Profile,
    string Phase,
    PhaseSkills Skills,
    IReadOnlyList<KitWarning> Warnings);
