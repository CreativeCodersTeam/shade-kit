namespace CreativeCoders.ShadeKit.Cli;

/// <summary>
/// Defines the stable kebab-case codes of the errors and warnings that SHADE kit CLI commands report in JSON mode.
/// </summary>
/// <remarks>
/// Skills branch on these codes instead of parsing messages. Every error code belongs to one exit code in
/// <see cref="ShadeKitCliExitCodes" />. <see cref="UpgradeNeeded" /> is also used as a warning code when a read
/// command migrates an outdated file in memory.
/// </remarks>
public static class ShadeKitCliErrorCodes
{
    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.KitRootNotFound" />.
    /// </summary>
    public const string KitRootNotFound = "kit-root-not-found";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.NotAGitRepository" />.
    /// </summary>
    public const string NotAGitRepository = "not-a-git-repository";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.KitAlreadyInitialized" />.
    /// </summary>
    public const string KitAlreadyInitialized = "already-initialized";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.PathNotAFile" />.
    /// </summary>
    public const string PathNotAFile = "path-not-a-file";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.KitRootNotADirectory" />.
    /// </summary>
    public const string KitRootNotADirectory = "kit-root-not-a-directory";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.FormatTooNew" />.
    /// </summary>
    public const string FormatTooNew = "format-too-new";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.UpgradeNeeded" />, and the warning
    /// code reported when an outdated file was migrated in memory.
    /// </summary>
    public const string UpgradeNeeded = "upgrade-needed";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.InvalidFile" />.
    /// </summary>
    public const string InvalidFile = "invalid-file";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.InvalidManagedSection" />.
    /// </summary>
    public const string InvalidManagedSection = "invalid-managed-section";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.UnsupportedFileEncoding" />.
    /// </summary>
    public const string UnsupportedFileEncoding = "unsupported-encoding";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.HostError" />.
    /// </summary>
    public const string HostError = "host-error";
}
