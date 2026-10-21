namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents a warning about the features of a SHADE kit store that did not stop the command.
/// </summary>
/// <remarks>
/// The meaning of <paramref name="FeatureId" /> and <paramref name="OtherFeatureId" /> depends on
/// <paramref name="Kind" />; see the members of <see cref="FeatureWarningKind" />. The ids are reported as they were
/// found and are not necessarily valid feature ids, for example when the local override contains an invalid id.
/// </remarks>
/// <param name="Kind">One of the enumeration values that specifies the kind of the warning.</param>
/// <param name="FeatureId">
/// The id of the feature the warning is about, for example the feature selected by the local override.
/// </param>
/// <param name="OtherFeatureId">
/// The id of a second feature involved in the warning, for example the feature selected by the current branch for a
/// <see cref="FeatureWarningKind.CurrentFeatureMismatch" /> warning; or <see langword="null" /> if the kind involves
/// only one feature.
/// </param>
/// <param name="Detail">
/// A description of the problem without a trailing period, for example why the local override cannot be used for a
/// <see cref="FeatureWarningKind.CurrentFeatureInvalid" /> warning; or <see langword="null" /> if the kind needs none.
/// </param>
public sealed record FeatureWarning(
    FeatureWarningKind Kind,
    string FeatureId,
    string? OtherFeatureId = null,
    string? Detail = null)
{
    /// <summary>
    /// Creates a warning that the current branch and the local override select different features.
    /// </summary>
    /// <param name="overrideFeatureId">The id of the feature that the local override selects.</param>
    /// <param name="branchFeatureId">The id of the feature that the current branch selects and that wins.</param>
    /// <returns>
    /// A <see cref="FeatureWarningKind.CurrentFeatureMismatch" /> warning with <paramref name="overrideFeatureId" />
    /// as <see cref="FeatureId" /> and <paramref name="branchFeatureId" /> as <see cref="OtherFeatureId" />.
    /// </returns>
    public static FeatureWarning CurrentFeatureMismatch(string overrideFeatureId, string branchFeatureId)
    {
        return new FeatureWarning(FeatureWarningKind.CurrentFeatureMismatch, overrideFeatureId, branchFeatureId);
    }

    /// <summary>
    /// Creates a warning that the local override selects a feature that does not exist.
    /// </summary>
    /// <param name="overrideFeatureId">The id stored in the local override, valid or not.</param>
    /// <returns>
    /// A <see cref="FeatureWarningKind.CurrentFeatureMissing" /> warning with <paramref name="overrideFeatureId" /> as
    /// <see cref="FeatureId" />.
    /// </returns>
    public static FeatureWarning CurrentFeatureMissing(string overrideFeatureId)
    {
        return new FeatureWarning(FeatureWarningKind.CurrentFeatureMissing, overrideFeatureId);
    }

    /// <summary>
    /// Creates a warning that a feature folder has no <c>feature.json</c> and was skipped.
    /// </summary>
    /// <param name="featureId">The name of the feature folder.</param>
    /// <returns>
    /// A <see cref="FeatureWarningKind.FeatureFileMissing" /> warning with <paramref name="featureId" /> as
    /// <see cref="FeatureId" />.
    /// </returns>
    public static FeatureWarning FeatureFileMissing(string featureId)
    {
        return new FeatureWarning(FeatureWarningKind.FeatureFileMissing, featureId);
    }

    /// <summary>
    /// Creates a warning that a folder in the <c>features</c> directory is not named with a valid feature id and was
    /// skipped.
    /// </summary>
    /// <param name="folderName">The name of the folder.</param>
    /// <returns>
    /// A <see cref="FeatureWarningKind.FeatureFolderInvalid" /> warning with <paramref name="folderName" /> as
    /// <see cref="FeatureId" />.
    /// </returns>
    public static FeatureWarning FeatureFolderInvalid(string folderName)
    {
        return new FeatureWarning(FeatureWarningKind.FeatureFolderInvalid, folderName);
    }

    /// <summary>
    /// Creates a warning that the <c>feature.json</c> of a feature records an id other than the name of its folder.
    /// </summary>
    /// <param name="folderFeatureId">The name of the feature folder, which is used as the id of the feature.</param>
    /// <param name="fileFeatureId">The id recorded in the <c>feature.json</c> of the feature.</param>
    /// <returns>
    /// A <see cref="FeatureWarningKind.FeatureIdMismatch" /> warning with <paramref name="folderFeatureId" /> as
    /// <see cref="FeatureId" /> and <paramref name="fileFeatureId" /> as <see cref="OtherFeatureId" />.
    /// </returns>
    public static FeatureWarning FeatureIdMismatch(string folderFeatureId, string fileFeatureId)
    {
        return new FeatureWarning(FeatureWarningKind.FeatureIdMismatch, folderFeatureId, fileFeatureId);
    }

    /// <summary>
    /// Creates a warning that the local override cannot be used and is ignored.
    /// </summary>
    /// <param name="reason">
    /// A description of the problem without a trailing period, for example
    /// <c>it uses format version 3, but this engine supports only up to version 1</c>.
    /// </param>
    /// <returns>
    /// A <see cref="FeatureWarningKind.CurrentFeatureInvalid" /> warning with an empty <see cref="FeatureId" /> and
    /// <paramref name="reason" /> as <see cref="Detail" />.
    /// </returns>
    public static FeatureWarning CurrentFeatureInvalid(string reason)
    {
        return new FeatureWarning(FeatureWarningKind.CurrentFeatureInvalid, string.Empty, Detail: reason);
    }
}
