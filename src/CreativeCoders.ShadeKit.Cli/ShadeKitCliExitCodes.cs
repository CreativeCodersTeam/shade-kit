namespace CreativeCoders.ShadeKit.Cli;

/// <summary>
/// Defines the process exit codes that SHADE kit CLI commands return for expected failures.
/// </summary>
/// <remarks>
/// A successful run returns <c>0</c>, also when warnings were reported. The codes are grouped in ranges per area:
/// 10–19 store and kit root, 20–29 format and version, 30–39 features, 40–49 phases and gates, and 255 for failures
/// of the CLI host itself. In JSON mode, every exit code comes with the matching error code from
/// <see cref="ShadeKitCliErrorCodes" />.
/// </remarks>
public static class ShadeKitCliExitCodes
{
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
    /// contains a SHADE kit root (<c>.shadekit</c> directory or file). The error code is
    /// <see cref="ShadeKitCliErrorCodes.KitAlreadyInitialized" />.
    /// </summary>
    public const int KitAlreadyInitialized = 13;

    /// <summary>
    /// Represents the exit code returned when a path at which <c>shade-kit init</c> must write a file, such as
    /// <c>.gitignore</c>, <c>CLAUDE.md</c>, or <c>AGENTS.md</c> at the repository root, is a directory. The error code
    /// is <see cref="ShadeKitCliErrorCodes.PathNotAFile" />.
    /// </summary>
    public const int PathNotAFile = 14;

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
    /// Represents the exit code returned when a file that <c>shade-kit init</c> changes in place, such as
    /// <c>.gitignore</c>, <c>CLAUDE.md</c>, or <c>AGENTS.md</c>, is not encoded as UTF-8 because it starts with a
    /// UTF-16 or UTF-32 byte order mark. The error code is <see cref="ShadeKitCliErrorCodes.UnsupportedFileEncoding" />.
    /// </summary>
    public const int UnsupportedFileEncoding = 24;

    /// <summary>
    /// Represents the exit code returned when the CLI host itself fails, for example because the command is unknown,
    /// cannot be created, or its options are invalid. The error code is <see cref="ShadeKitCliErrorCodes.HostError" />.
    /// </summary>
    /// <remarks>
    /// The CLI host reports such failures with negative exit codes based on <see cref="int.MinValue" />. Operating
    /// systems that keep only the lowest byte of an exit code, such as macOS and Linux, would turn these codes into
    /// <c>0</c>. The CLI therefore maps every negative exit code to this value.
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
