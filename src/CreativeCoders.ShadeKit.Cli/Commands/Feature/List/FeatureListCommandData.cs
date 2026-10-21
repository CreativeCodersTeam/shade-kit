using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.List;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit feature list --json</c> returns.
/// </summary>
/// <param name="Features">
/// The id, title, profile, and status of every feature in ordinal order of the ids; an empty list if the store has
/// none. Each status is written as <c>active</c> or <c>done</c>.
/// </param>
public sealed record FeatureListCommandData(IReadOnlyList<FeatureSummary> Features);
