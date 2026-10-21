namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Specifies the kind of a <see cref="FeatureWarning" />.
/// </summary>
public enum FeatureWarningKind
{
    /// <summary>
    /// The current branch and the local override select different existing features; the branch wins.
    /// <see cref="FeatureWarning.FeatureId" /> is the feature of the override and
    /// <see cref="FeatureWarning.OtherFeatureId" /> the feature of the branch.
    /// </summary>
    CurrentFeatureMismatch,

    /// <summary>
    /// The local override selects a feature that does not exist or whose id is not a valid feature id; the override
    /// is ignored and the file is left unchanged. <see cref="FeatureWarning.FeatureId" /> is the id stored in the
    /// override.
    /// </summary>
    CurrentFeatureMissing,

    /// <summary>
    /// A feature folder has no <c>feature.json</c> and was skipped. <see cref="FeatureWarning.FeatureId" /> is the
    /// name of the folder.
    /// </summary>
    FeatureFileMissing,

    /// <summary>
    /// A folder in the <c>features</c> directory has a name that is not a valid feature id and was skipped.
    /// <see cref="FeatureWarning.FeatureId" /> is the name of the folder.
    /// </summary>
    FeatureFolderInvalid,

    /// <summary>
    /// The <c>feature.json</c> of a feature records an <c>id</c> other than the name of its folder; the folder name is
    /// used and the file is left unchanged. <see cref="FeatureWarning.FeatureId" /> is the name of the folder and
    /// <see cref="FeatureWarning.OtherFeatureId" /> the id recorded in the file.
    /// </summary>
    FeatureIdMismatch,

    /// <summary>
    /// The local override cannot be used, because it is invalid, uses a format version newer than the engine
    /// supports, or uses an older format version that must not be migrated in memory; the override is ignored and the
    /// file is left unchanged. <see cref="FeatureWarning.FeatureId" /> is empty, because the file cannot be read, and
    /// <see cref="FeatureWarning.Detail" /> describes the problem.
    /// </summary>
    CurrentFeatureInvalid
}
