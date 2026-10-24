using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Features.Warnings;

/// <summary>
/// Represents a warning that the local override selects a feature that does not exist or whose id is not a valid
/// feature id; the override is ignored and the file is left unchanged.
/// </summary>
/// <param name="OverrideFeatureId">
/// The id stored in the local override, as it was found; it is not necessarily a valid feature id.
/// </param>
public sealed record CurrentFeatureMissingWarning(string OverrideFeatureId) : KitWarning;
