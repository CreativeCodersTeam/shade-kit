namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.List;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit feature list --json</c> returns.
/// </summary>
/// <param name="Features">The names of all features in ordinal order; an empty list if the store has none.</param>
public sealed record FeatureListCommandData(IReadOnlyList<string> Features);
