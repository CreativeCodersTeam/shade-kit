namespace CreativeCoders.ShadeKit.Cli;

/// <summary>
/// Defines the process exit codes that SHADE kit CLI commands return for expected failures.
/// </summary>
/// <remarks>
/// A successful run returns <c>0</c>. Failures detected by the CLI host itself, such as an unknown command,
/// use the exit codes defined by <c>CreativeCoders.Cli.Hosting.CliExitCodes</c>.
/// </remarks>
public static class ShadeKitCliExitCodes
{
    /// <summary>
    /// Represents the exit code returned when no SHADE kit root directory (<c>.shadekit</c>) is found in the
    /// current directory or any of its parent directories.
    /// </summary>
    public const int KitRootNotFound = 1;

    /// <summary>
    /// Represents the exit code returned when the SHADE kit root directory exists but contains no
    /// <c>features</c> directory.
    /// </summary>
    public const int FeaturesDirectoryNotFound = 2;

    /// <summary>
    /// Represents the exit code returned when the CLI host itself fails, for example because the command is unknown,
    /// cannot be created, or its options are invalid.
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
