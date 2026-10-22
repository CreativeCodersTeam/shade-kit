using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the local override of the current feature, stored in
/// <c>.shadekit/features/current-feature.json</c>.
/// </summary>
/// <remarks>
/// This model holds the fields of format version <c>1</c> (format id <see cref="FormatIds.CurrentFeature" />). The
/// file is local-only and not committed. <see cref="FeatureId" /> is required: a file in which it is missing or
/// <see langword="null" /> cannot be read. Fields the engine does not know are kept in
/// <see cref="VersionedDocument.ExtensionData" /> and written back unchanged.
/// </remarks>
public sealed class CurrentFeatureDocument : VersionedDocument
{
    /// <summary>
    /// Gets the id of the feature that is current in this local clone.
    /// </summary>
    /// <value>The feature id, for example <c>gh-142-login-sso</c>.</value>
    public required string FeatureId { get; init; }
}
