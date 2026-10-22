namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents a warning that the current branch and the local override select different existing features; the
/// branch wins.
/// </summary>
/// <param name="OverrideFeatureId">The id of the feature that the local override selects.</param>
/// <param name="BranchFeatureId">The id of the feature that the current branch selects and that wins.</param>
public sealed record CurrentFeatureMismatchWarning(string OverrideFeatureId, string BranchFeatureId) : KitWarning;
