using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Current;

/// <summary>
/// Represents the <c>feature</c> object in the <c>data</c> of <c>shade-kit feature current --json</c>.
/// </summary>
/// <param name="Id">The id of the current feature.</param>
/// <param name="Title">The title of the current feature.</param>
/// <param name="Profile">The workflow profile of the current feature.</param>
/// <param name="Status">The status of the current feature, written as <c>active</c> or <c>done</c>.</param>
/// <param name="CurrentPhase">
/// The current phase of the feature, normalized against its workflow profile; the first phase of the profile for a
/// feature without phase fields.
/// </param>
/// <param name="PhaseState">
/// The state of the current phase, written as <c>pending</c>, <c>in-progress</c>, <c>awaiting-approval</c>, or
/// <c>approved</c>.
/// </param>
/// <param name="Paths">The repository-relative paths of the feature folder and its files.</param>
public sealed record FeatureCurrentCommandFeatureData(
    string Id,
    string Title,
    string Profile,
    FeatureStatus Status,
    string CurrentPhase,
    PhaseState PhaseState,
    FeaturePaths Paths);
