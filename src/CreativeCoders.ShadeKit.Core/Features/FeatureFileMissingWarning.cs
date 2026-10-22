namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents a warning that a feature folder has no <c>feature.json</c> and was skipped.
/// </summary>
/// <param name="FeatureId">The name of the feature folder.</param>
public sealed record FeatureFileMissingWarning(string FeatureId) : KitWarning;
