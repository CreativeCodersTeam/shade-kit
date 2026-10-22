namespace CreativeCoders.ShadeKit.Cli;

/// <summary>
/// Defines the stable kebab-case codes of the warnings that SHADE kit CLI commands report in JSON mode and that have
/// no matching error.
/// </summary>
/// <remarks>
/// Skills branch on these codes instead of parsing messages. A warning code never stops a command and therefore has
/// no exit code in <see cref="ShadeKitCliExitCodes" />. Warnings that also exist as errors, such as
/// <see cref="ShadeKitCliErrorCodes.UpgradeNeeded" />, use the code from <see cref="ShadeKitCliErrorCodes" />.
/// </remarks>
public static class ShadeKitCliWarningCodes
{
    /// <summary>
    /// Represents the warning code reported when the current branch and the local override select different features
    /// and the branch wins.
    /// </summary>
    public const string CurrentFeatureMismatch = "current-feature-mismatch";

    /// <summary>
    /// Represents the warning code reported when the local override selects a feature that does not exist and is
    /// ignored.
    /// </summary>
    public const string CurrentFeatureMissing = "current-feature-missing";

    /// <summary>
    /// Represents the warning code reported when a feature folder has no <c>feature.json</c> and is skipped.
    /// </summary>
    public const string FeatureFileMissing = "feature-file-missing";

    /// <summary>
    /// Represents the warning code reported when a folder in <c>.shadekit/features/</c> is not named with a valid
    /// feature id and is skipped.
    /// </summary>
    public const string FeatureFolderInvalid = "feature-folder-invalid";

    /// <summary>
    /// Represents the warning code reported when the <c>feature.json</c> of a feature records an id other than the name
    /// of its folder; the folder name is used.
    /// </summary>
    public const string FeatureIdMismatch = "feature-id-mismatch";

    /// <summary>
    /// Represents the warning code reported when the local override cannot be used because of its format and is
    /// ignored.
    /// </summary>
    public const string CurrentFeatureInvalid = "current-feature-invalid";
}
