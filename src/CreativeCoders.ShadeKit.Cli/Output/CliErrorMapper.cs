using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Maps the exceptions and warnings of the SHADE kit core to the exit codes, error codes, and messages of the CLI.
/// </summary>
/// <remarks>
/// This is the only place that knows which core exception belongs to which exit code. Commands catch the core
/// exceptions, map them here, and report the result as text or as a JSON envelope.
/// </remarks>
/// <param name="engineVersionProvider">The provider of the running engine version, which messages mention.</param>
public sealed class CliErrorMapper(IEngineVersionProvider engineVersionProvider)
{
    private readonly IEngineVersionProvider _engineVersionProvider = Ensure.NotNull(engineVersionProvider);

    /// <summary>
    /// Maps the specified exception to a CLI error.
    /// </summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>
    /// The CLI error for <paramref name="exception" />, or <see langword="null" /> if it is not an expected failure
    /// of the SHADE kit core.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception" /> is <see langword="null" />.</exception>
    public CliError? Map(Exception exception)
    {
        Ensure.NotNull(exception);

        return exception switch
        {
            KitRootPathNotFoundException e => new CliError(ShadeKitCliExitCodes.KitRootNotFound,
                ShadeKitCliErrorCodes.KitRootNotFound,
                $"No SHADE kit root directory '{KitStorePaths.RootPath}' found at the repository root " +
                $"'{e.GitRootPath}'. Run 'shade-kit init' to set up SHADE kit."),

            NotAGitRepositoryException e => new CliError(ShadeKitCliExitCodes.NotAGitRepository,
                ShadeKitCliErrorCodes.NotAGitRepository,
                $"'{e.StartPath}' is not inside a git repository. SHADE kit works only in a git repository; run " +
                "shade-kit inside one."),

            FeaturesDirectoryNotFoundException e => new CliError(ShadeKitCliExitCodes.FeaturesDirectoryNotFound,
                ShadeKitCliErrorCodes.FeaturesDirectoryNotFound,
                $"The SHADE kit root directory contains no '{KitStorePaths.FeaturesDirName}' directory " +
                $"('{e.FeaturesPath}')."),

            FormatTooNewException e => new CliError(ShadeKitCliExitCodes.FormatTooNew,
                ShadeKitCliErrorCodes.FormatTooNew, CreateFormatTooNewMessage(e)),

            UpgradeNeededException e => new CliError(ShadeKitCliExitCodes.UpgradeNeeded,
                ShadeKitCliErrorCodes.UpgradeNeeded,
                $"{CreateVersionMismatchText(e.FilePath, e.FileVersion, e.CurrentVersion)} Run 'shade-kit upgrade'."),

            InvalidKitFileException e => new CliError(ShadeKitCliExitCodes.InvalidFile,
                ShadeKitCliErrorCodes.InvalidFile, $"'{e.FilePath}' is invalid: {e.Reason}"),

            _ => null
        };
    }

    /// <summary>
    /// Maps the specified format warning to the warning object of a JSON envelope.
    /// </summary>
    /// <param name="warning">The warning to map.</param>
    /// <returns>The warning with its code from <see cref="ShadeKitCliErrorCodes" /> and a message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="warning" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The kind of <paramref name="warning" /> is not defined.</exception>
    public static CliJsonMessage MapWarning(FormatWarning warning)
    {
        Ensure.NotNull(warning);

        return warning.Kind switch
        {
            FormatWarningKind.UpgradeNeeded => new CliJsonMessage(ShadeKitCliErrorCodes.UpgradeNeeded,
                $"{CreateVersionMismatchText(warning.FilePath, warning.FileVersion, warning.CurrentVersion)} " +
                "It was migrated in memory; run 'shade-kit upgrade' to update the file."),

            _ => throw new ArgumentOutOfRangeException(nameof(warning), warning.Kind,
                "The warning kind is not supported.")
        };
    }

    private static string CreateVersionMismatchText(string filePath, int fileVersion, int currentVersion)
    {
        return $"'{filePath}' uses format version {fileVersion}; this shade-kit uses version {currentVersion}.";
    }

    private string CreateFormatTooNewMessage(FormatTooNewException exception)
    {
        var installedVersion = _engineVersionProvider.Version;

        return exception.MinEngineVersion is null
            ? $"'{exception.FilePath}' uses format version {exception.FileVersion}, which this shade-kit does not " +
              $"support; a newer shade-kit is required (installed: {installedVersion})."
            : $"'{exception.FilePath}' requires shade-kit >= {exception.MinEngineVersion} " +
              $"(installed: {installedVersion}).";
    }
}
