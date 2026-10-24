namespace CreativeCoders.ShadeKit.Cli;

/// <summary>
/// Defines the stable kebab-case codes of the errors and warnings that SHADE kit CLI commands report in JSON mode.
/// </summary>
/// <remarks>
/// Skills branch on these codes instead of parsing messages. <see cref="ForExitCode" /> pairs every exit code with
/// its error code. <see cref="UpgradeNeeded" /> is also used as a warning code.
/// </remarks>
public static class ShadeKitCliErrorCodes
{
    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.InvalidArguments" />.
    /// </summary>
    public const string InvalidArguments = "invalid-arguments";

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
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.GitUnavailable" />.
    /// </summary>
    public const string GitUnavailable = "git-unavailable";

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
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.GitIdentityMissing" />.
    /// </summary>
    public const string GitIdentityMissing = "git-identity-missing";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.FeatureAlreadyExists" />.
    /// </summary>
    public const string FeatureAlreadyExists = "feature-already-exists";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.FeatureNotFound" />.
    /// </summary>
    public const string FeatureNotFound = "feature-not-found";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.InvalidFeatureId" />.
    /// </summary>
    public const string InvalidFeatureId = "invalid-feature-id";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.InvalidFeatureSource" />.
    /// </summary>
    public const string InvalidFeatureSource = "invalid-source";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.InvalidFeatureTitle" />.
    /// </summary>
    public const string InvalidFeatureTitle = "invalid-title";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.FeatureIoFailed" />.
    /// </summary>
    public const string FeatureIoFailed = "feature-io-failed";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.UnknownProfile" />.
    /// </summary>
    public const string UnknownProfile = "unknown-profile";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.Canceled" />.
    /// </summary>
    public const string Canceled = "canceled";

    /// <summary>
    /// Represents the error code that belongs to <see cref="ShadeKitCliExitCodes.HostError" />.
    /// </summary>
    public const string HostError = "host-error";

    /// <summary>
    /// Returns the error code that belongs to the specified exit code.
    /// </summary>
    /// <param name="exitCode">An exit code from <see cref="ShadeKitCliExitCodes" />.</param>
    /// <returns>The error code that belongs to <paramref name="exitCode" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exitCode" /> is not defined in <see cref="ShadeKitCliExitCodes" />.
    /// </exception>
    public static string ForExitCode(int exitCode)
    {
        return exitCode switch
        {
            ShadeKitCliExitCodes.InvalidArguments => InvalidArguments,
            ShadeKitCliExitCodes.KitRootNotFound => KitRootNotFound,
            ShadeKitCliExitCodes.NotAGitRepository => NotAGitRepository,
            ShadeKitCliExitCodes.KitAlreadyInitialized => KitAlreadyInitialized,
            ShadeKitCliExitCodes.PathNotAFile => PathNotAFile,
            ShadeKitCliExitCodes.KitRootNotADirectory => KitRootNotADirectory,
            ShadeKitCliExitCodes.GitUnavailable => GitUnavailable,
            ShadeKitCliExitCodes.FormatTooNew => FormatTooNew,
            ShadeKitCliExitCodes.UpgradeNeeded => UpgradeNeeded,
            ShadeKitCliExitCodes.InvalidFile => InvalidFile,
            ShadeKitCliExitCodes.InvalidManagedSection => InvalidManagedSection,
            ShadeKitCliExitCodes.UnsupportedFileEncoding => UnsupportedFileEncoding,
            ShadeKitCliExitCodes.FeatureAlreadyExists => FeatureAlreadyExists,
            ShadeKitCliExitCodes.FeatureNotFound => FeatureNotFound,
            ShadeKitCliExitCodes.InvalidFeatureId => InvalidFeatureId,
            ShadeKitCliExitCodes.InvalidFeatureSource => InvalidFeatureSource,
            ShadeKitCliExitCodes.InvalidFeatureTitle => InvalidFeatureTitle,
            ShadeKitCliExitCodes.FeatureIoFailed => FeatureIoFailed,
            ShadeKitCliExitCodes.GitIdentityMissing => GitIdentityMissing,
            ShadeKitCliExitCodes.UnknownProfile => UnknownProfile,
            ShadeKitCliExitCodes.Canceled => Canceled,
            ShadeKitCliExitCodes.HostError => HostError,
            _ => throw new ArgumentOutOfRangeException(nameof(exitCode), exitCode, "The exit code is not defined.")
        };
    }
}
