using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Current;

/// <summary>
/// Represents the <c>feature</c> object in the <c>data</c> of <c>shade-kit feature current --json</c>.
/// </summary>
/// <param name="Id">The id of the current feature.</param>
/// <param name="Title">The title of the current feature.</param>
/// <param name="Profile">The workflow profile of the current feature.</param>
/// <param name="Status">The status of the current feature, written as <c>active</c> or <c>done</c>.</param>
/// <param name="Paths">The repository-relative paths of the feature folder and its files.</param>
public sealed record FeatureCurrentCommandFeatureData(
    string Id,
    string Title,
    string Profile,
    FeatureStatus Status,
    FeaturePaths Paths);
