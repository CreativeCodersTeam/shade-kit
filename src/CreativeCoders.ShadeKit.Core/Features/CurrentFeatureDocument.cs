using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the local override of the current feature, stored in
/// <c>.shadekit/features/current-feature.json</c>.
/// </summary>
/// <remarks>
/// Local-only and not committed. <see cref="FeatureId" /> is required. Unknown fields are kept in
/// <see cref="VersionedDocument.ExtensionData" />.
/// </remarks>
public sealed class CurrentFeatureDocument : VersionedDocument
{
    /// <summary>
    /// Gets the id of the feature that is current in this local clone.
    /// </summary>
    /// <value>The feature id, for example <c>gh-142-login-sso</c>.</value>
    public required string FeatureId { get; init; }
}
