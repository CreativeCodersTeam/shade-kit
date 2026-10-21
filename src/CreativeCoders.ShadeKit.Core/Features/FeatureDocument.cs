using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the identity and status of a feature, stored in <c>.shadekit/features/&lt;id&gt;/feature.json</c>.
/// </summary>
/// <remarks>
/// This model holds the fields of format version <c>1</c> (format id <see cref="FormatIds.Feature" />).
/// <see cref="Id" />, <see cref="Title" />, <see cref="Profile" />, <see cref="Source" />, <see cref="CreatedAt" />,
/// and <see cref="CreatedBy" /> are required: a file in which one of them is missing, <see langword="null" />, or of
/// the wrong type cannot be read. Fields the engine does not know, for example those of later packages, are kept in
/// <see cref="VersionedDocument.ExtensionData" /> and written back unchanged. The fields are written in the order in
/// which the properties are declared, after <see cref="VersionedDocument.SchemaVersion" /> and
/// <see cref="VersionedDocument.MinEngineVersion" />.
/// </remarks>
public sealed class FeatureDocument : VersionedDocument
{
    /// <summary>
    /// Gets the id of the feature.
    /// </summary>
    /// <value>The feature id, which is also the name of the feature directory, for example <c>gh-142-login-sso</c>.</value>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the title of the feature.
    /// </summary>
    /// <value>The human-readable title from which the slug of the id was built.</value>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the workflow profile of the feature.
    /// </summary>
    /// <value>The name of a workflow profile, for example <see cref="WorkflowProfiles.Feature" />.</value>
    public required string Profile { get; init; }

    /// <summary>
    /// Gets the input from which the feature was created.
    /// </summary>
    /// <value>The kind of the input and the reference to it.</value>
    public required FeatureSource Source { get; init; }

    /// <summary>
    /// Gets the point in time at which the feature was created.
    /// </summary>
    /// <value>The local creation time with its offset from UTC, written in ISO 8601 format.</value>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the git user who created the feature.
    /// </summary>
    /// <value>The name and email address of the creator.</value>
    public required FeatureCreator CreatedBy { get; init; }

    /// <summary>
    /// Gets or sets whether work on the feature is still in progress.
    /// </summary>
    /// <value>
    /// One of the enumeration values. The default is <see cref="FeatureStatus.Active" />, which also applies when the
    /// field is missing.
    /// </value>
    public FeatureStatus Status { get; set; } = FeatureStatus.Active;
}
