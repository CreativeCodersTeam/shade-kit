namespace CreativeCoders.ShadeKit.Cli;

/// <summary>
/// Defines the process exit codes that SHADE kit CLI commands return for expected failures.
/// </summary>
/// <remarks>
/// <c>0</c> means success, also with warnings. Ranges per area: 10–19 store, 20–29 format, 30–39 features, 40–49
/// phases and gates, 255 host. <see cref="InvalidArguments" /> (<c>2</c>) and <see cref="Canceled" /> (<c>130</c>)
/// belong to no area. Every exit code has an error code in <see cref="ShadeKitCliErrorCodes" />.
/// </remarks>
public static class ShadeKitCliExitCodes
{
    /// <summary>
    /// Represents the exit code returned when the option parser rejects the command-line arguments, for example a
    /// missing required argument. The error code is <see cref="ShadeKitCliErrorCodes.InvalidArguments" />.
    /// </summary>
    public const int InvalidArguments = 2;

    /// <summary>
    /// Represents the exit code returned when the root of the git repository that contains the current directory
    /// contains no SHADE kit root directory (<c>.shadekit</c>). The error code is
    /// <see cref="ShadeKitCliErrorCodes.KitRootNotFound" />.
    /// </summary>
    public const int KitRootNotFound = 10;

    /// <summary>
    /// Represents the exit code returned when the current directory is not inside a git repository. The error code is
    /// <see cref="ShadeKitCliErrorCodes.NotAGitRepository" />.
    /// </summary>
    public const int NotAGitRepository = 12;

    /// <summary>
    /// Represents the exit code returned when <c>shade-kit init</c> runs in a git repository whose root already
    /// contains a SHADE kit root directory (<c>.shadekit</c>). The error code is
    /// <see cref="ShadeKitCliErrorCodes.KitAlreadyInitialized" />.
    /// </summary>
    public const int KitAlreadyInitialized = 13;

    /// <summary>
    /// Represents the exit code returned when a path at which a command needs a file, such as <c>.gitignore</c>,
    /// <c>CLAUDE.md</c>, or <c>AGENTS.md</c> at the repository root, is a directory. The error code is
    /// <see cref="ShadeKitCliErrorCodes.PathNotAFile" />.
    /// </summary>
    public const int PathNotAFile = 14;

    /// <summary>
    /// Represents the exit code returned when the SHADE kit root path (<c>.shadekit</c>) at the root of the git
    /// repository is a file instead of a directory. The error code is
    /// <see cref="ShadeKitCliErrorCodes.KitRootNotADirectory" />.
    /// </summary>
    public const int KitRootNotADirectory = 15;

    /// <summary>
    /// Represents the exit code returned when the <c>git</c> executable cannot be started or a <c>git</c> command
    /// fails with an unexpected exit code. The error code is <see cref="ShadeKitCliErrorCodes.GitUnavailable" />.
    /// </summary>
    public const int GitUnavailable = 16;

    /// <summary>
    /// Represents the exit code returned when a file uses a format version that is newer than the engine supports.
    /// The error code is <see cref="ShadeKitCliErrorCodes.FormatTooNew" />.
    /// </summary>
    public const int FormatTooNew = 20;

    /// <summary>
    /// Represents the exit code returned when a file uses an older format version and the command must not migrate
    /// it in memory, so <c>shade-kit upgrade</c> is needed. The error code is
    /// <see cref="ShadeKitCliErrorCodes.UpgradeNeeded" />.
    /// </summary>
    public const int UpgradeNeeded = 21;

    /// <summary>
    /// Represents the exit code returned when a file cannot be used, for example because it is not valid JSON or has
    /// no <c>schemaVersion</c>. The error code is <see cref="ShadeKitCliErrorCodes.InvalidFile" />.
    /// </summary>
    public const int InvalidFile = 22;

    /// <summary>
    /// Represents the exit code returned when the markers of the managed SHADE kit section in <c>CLAUDE.md</c> or
    /// <c>AGENTS.md</c> are invalid, so the section cannot be located safely. The error code is
    /// <see cref="ShadeKitCliErrorCodes.InvalidManagedSection" />.
    /// </summary>
    public const int InvalidManagedSection = 23;

    /// <summary>
    /// Represents the exit code returned when a file that a command changes in place, such as
    /// <c>.gitignore</c>, <c>CLAUDE.md</c>, or <c>AGENTS.md</c>, is not encoded as UTF-8 because it starts with a
    /// UTF-16 or UTF-32 byte order mark. The error code is <see cref="ShadeKitCliErrorCodes.UnsupportedFileEncoding" />.
    /// </summary>
    public const int UnsupportedFileEncoding = 24;

    /// <summary>
    /// Represents the exit code returned when <c>shade-kit feature new</c> builds an id whose feature folder already
    /// exists, usually because the feature was created on another branch; nothing is changed. The error code is
    /// <see cref="ShadeKitCliErrorCodes.FeatureAlreadyExists" />.
    /// </summary>
    public const int FeatureAlreadyExists = 30;

    /// <summary>
    /// Represents the exit code returned when a command names a feature whose feature folder does not exist. The
    /// error code is <see cref="ShadeKitCliErrorCodes.FeatureNotFound" />.
    /// </summary>
    public const int FeatureNotFound = 31;

    /// <summary>
    /// Represents the exit code returned when a feature id does not consist of groups of lowercase letters
    /// <c>a</c>–<c>z</c> and digits joined by single hyphens. The error code is
    /// <see cref="ShadeKitCliErrorCodes.InvalidFeatureId" />.
    /// </summary>
    public const int InvalidFeatureId = 32;

    /// <summary>
    /// Represents the exit code returned when the source of a new feature cannot be used, because it is empty; it is a
    /// URL of a tracker in <c>config.json</c> without an issue number; it names a file that cannot be read, is larger
    /// than 1 MiB, or is not UTF-8 text; the issue of a tracker URL cannot be read with <c>gh</c>, because <c>gh</c>
    /// cannot be found or started, or fails; or the content of another URL cannot be loaded. The error code is
    /// <see cref="ShadeKitCliErrorCodes.InvalidFeatureSource" />.
    /// </summary>
    public const int InvalidFeatureSource = 33;

    /// <summary>
    /// Represents the exit code returned when no usable title of a new feature is available, because a URL source has
    /// no <c>--title</c> or the title contains no letter or digit for the slug of the id. The error code is
    /// <see cref="ShadeKitCliErrorCodes.InvalidFeatureTitle" />.
    /// </summary>
    public const int InvalidFeatureTitle = 34;

    /// <summary>
    /// Represents the exit code returned when the folder of a feature cannot be created or renamed, for example
    /// because the process may not write to <c>.shadekit/features/</c> or another program holds a file in it open;
    /// nothing is changed. The error code is <see cref="ShadeKitCliErrorCodes.FeatureIoFailed" />.
    /// </summary>
    public const int FeatureIoFailed = 35;

    /// <summary>
    /// Represents the exit code returned when the git identity (<c>user.name</c>, <c>user.email</c>, or both) that a
    /// command records is not configured. The error code is <see cref="ShadeKitCliErrorCodes.GitIdentityMissing" />.
    /// </summary>
    public const int GitIdentityMissing = 36;

    /// <summary>
    /// Represents the exit code returned when a workflow profile is not <c>feature</c>, <c>bugfix</c>, or
    /// <c>quick</c>. The error code is <see cref="ShadeKitCliErrorCodes.UnknownProfile" />.
    /// </summary>
    public const int UnknownProfile = 40;

    /// <summary>
    /// Represents the exit code returned when the user cancels a running command, for example by pressing Ctrl+C. The
    /// error code is <see cref="ShadeKitCliErrorCodes.Canceled" />.
    /// </summary>
    public const int Canceled = 130;

    /// <summary>
    /// Represents the exit code returned when the CLI host itself fails, for example because the command is unknown,
    /// cannot be created, or its options are invalid. The error code is <see cref="ShadeKitCliErrorCodes.HostError" />.
    /// </summary>
    /// <remarks>
    /// The host reports such failures with negative exit codes, which become <c>0</c> on systems that keep only the
    /// lowest byte, so every negative exit code is mapped to this value.
    /// </remarks>
    public const int HostError = 255;

    /// <summary>
    /// Converts an exit code returned by the CLI host into the exit code of the process.
    /// </summary>
    /// <param name="exitCode">The exit code returned by the CLI host.</param>
    /// <returns>
    /// <see cref="HostError" /> if <paramref name="exitCode" /> is negative; otherwise, <paramref name="exitCode" />.
    /// </returns>
    public static int ToProcessExitCode(int exitCode)
    {
        return exitCode < 0
            ? HostError
            : exitCode;
    }
}
