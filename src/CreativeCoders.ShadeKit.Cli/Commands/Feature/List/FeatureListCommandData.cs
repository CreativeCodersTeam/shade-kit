using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.List;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit feature list --json</c> returns.
/// </summary>
/// <param name="Features">The features in ordinal order of their ids; empty if the store has none.</param>
public sealed record FeatureListCommandData(IReadOnlyList<FeatureSummary> Features);
