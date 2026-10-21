using System.Reflection;
using CreativeCoders.Cli.Core;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.SysConsole.Cli.Parsing;
using CreativeCoders.SysConsole.Cli.Parsing.Exceptions;

namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Maps the exceptions and warnings of the SHADE kit core to the exit codes, error codes, and messages of the CLI.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place that knows which core exception belongs to which exit code. Commands catch the core
/// exceptions, map them here, and report the result as text or as a JSON envelope.
/// </para>
/// <para>
/// The failures of the option parser, which rejects command-line arguments before any command runs, are mapped here
/// too, by <see cref="MapArgumentError" />, and so is the cancellation of a run, by <see cref="MapCancellation" />, so
/// that every exit code of the CLI is assigned in one class.
/// </para>
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

            KitRootNotADirectoryException e => new CliError(ShadeKitCliExitCodes.KitRootNotADirectory,
                ShadeKitCliErrorCodes.KitRootNotADirectory,
                $"'{e.KitRootPath}' is a file, but SHADE kit needs a '{KitStorePaths.RootPath}' directory at the " +
                "repository root. Move or remove the file, then run 'shade-kit init'."),

            NotAGitRepositoryException e => new CliError(ShadeKitCliExitCodes.NotAGitRepository,
                ShadeKitCliErrorCodes.NotAGitRepository,
                $"'{e.StartPath}' is not inside a git repository. SHADE kit works only in a git repository; run " +
                "shade-kit inside one."),

            FormatTooNewException e => new CliError(ShadeKitCliExitCodes.FormatTooNew,
                ShadeKitCliErrorCodes.FormatTooNew, CreateFormatTooNewMessage(e)),

            UpgradeNeededException e => new CliError(ShadeKitCliExitCodes.UpgradeNeeded,
                ShadeKitCliErrorCodes.UpgradeNeeded,
                $"{CreateVersionMismatchText(e.FilePath, e.FileVersion, e.CurrentVersion)} Run 'shade-kit upgrade'."),

            InvalidKitFileException e => new CliError(ShadeKitCliExitCodes.InvalidFile,
                ShadeKitCliErrorCodes.InvalidFile, $"'{e.FilePath}' is invalid: {e.Reason}"),

            KitAlreadyInitializedException e => new CliError(ShadeKitCliExitCodes.KitAlreadyInitialized,
                ShadeKitCliErrorCodes.KitAlreadyInitialized,
                $"SHADE kit is already initialised in this repository: '{e.KitRootPath}' exists. Run " +
                "'shade-kit upgrade' to update it."),

            InvalidManagedSectionException e => new CliError(ShadeKitCliExitCodes.InvalidManagedSection,
                ShadeKitCliErrorCodes.InvalidManagedSection,
                $"The SHADE kit section markers in '{e.FilePath}' are invalid: {e.Reason} Fix the file so that " +
                $"every code block is closed and the file contains exactly one " +
                $"'{ManagedInstructionSection.BeginMarkerPrefix} ... -->' line followed by exactly one " +
                $"'{ManagedInstructionSection.EndMarker}' line outside code blocks, then run the command again."),

            UnsupportedFileEncodingException e => new CliError(ShadeKitCliExitCodes.UnsupportedFileEncoding,
                ShadeKitCliErrorCodes.UnsupportedFileEncoding,
                $"'{e.FilePath}' is encoded as {e.EncodingName}, but shade-kit can only change UTF-8 files. Convert " +
                "the file to UTF-8, then run the command again."),

            PathNotAFileException e => new CliError(ShadeKitCliExitCodes.PathNotAFile,
                ShadeKitCliErrorCodes.PathNotAFile,
                $"'{e.Path}' is a directory, but shade-kit must write a file at this path. Move or remove the " +
                "directory, then run the command again."),

            GitUnavailableException e => new CliError(ShadeKitCliExitCodes.GitUnavailable,
                ShadeKitCliErrorCodes.GitUnavailable,
                $"'{e.CommandText}' failed. Make sure git is installed, on the PATH, and works in this repository, " +
                $"then run the command again. Details: {e.Reason}"),

            GitIdentityNotConfiguredException e => new CliError(ShadeKitCliExitCodes.GitIdentityMissing,
                ShadeKitCliErrorCodes.GitIdentityMissing, CreateGitIdentityMissingMessage(e)),

            FeatureAlreadyExistsException e => new CliError(ShadeKitCliExitCodes.FeatureAlreadyExists,
                ShadeKitCliErrorCodes.FeatureAlreadyExists,
                $"The feature '{e.FeatureId}' already exists; it may have been created on another branch. Nothing " +
                "was changed. Run 'shade-kit feature list' to see the existing features, or pass another --title to " +
                "create a feature with a different id."),

            FeatureNotFoundException e => new CliError(ShadeKitCliExitCodes.FeatureNotFound,
                ShadeKitCliErrorCodes.FeatureNotFound,
                $"The feature '{e.FeatureId}' does not exist. Run 'shade-kit feature list' to see the ids of the " +
                "existing features."),

            InvalidFeatureIdException e => new CliError(ShadeKitCliExitCodes.InvalidFeatureId,
                ShadeKitCliErrorCodes.InvalidFeatureId,
                $"'{e.FeatureId}' is not a valid feature id. A feature id consists of groups of lowercase letters " +
                "a-z and digits joined by single hyphens, for example 'gh-142-login-sso'. Run 'shade-kit feature " +
                "list' to see the ids of the existing features."),

            InvalidFeatureSourceException e => new CliError(ShadeKitCliExitCodes.InvalidFeatureSource,
                ShadeKitCliErrorCodes.InvalidFeatureSource,
                $"The source '{e.SourceText}' cannot be used: {e.Reason} Pass an issue URL, the path of an existing " +
                "UTF-8 text file, or the request text with --source."),

            InvalidFeatureTitleException e => new CliError(ShadeKitCliExitCodes.InvalidFeatureTitle,
                ShadeKitCliErrorCodes.InvalidFeatureTitle,
                $"The feature title cannot be used: {e.Reason}"),

            FeatureIoFailedException e => new CliError(ShadeKitCliExitCodes.FeatureIoFailed,
                ShadeKitCliErrorCodes.FeatureIoFailed,
                $"Could not {e.Operation} the feature '{e.FeatureId}': the folder '{e.FolderPath}' cannot be " +
                $"changed. Cause: {CreateCauseSentence(e.InnerException)} Nothing was changed. Check that you may write to " +
                $"'{GetFeaturesDirectoryPath()}' and that no other program, such as an editor, a virus scanner, or " +
                "a search indexer, holds a file in the folder open, then run the command again."),

            UnknownProfileException e => new CliError(ShadeKitCliExitCodes.UnknownProfile,
                ShadeKitCliErrorCodes.UnknownProfile,
                $"The workflow profile '{e.Profile}' is unknown. Pass one of " +
                $"{string.Join(", ", WorkflowProfiles.All.Select(x => $"'{x}'"))} with --profile, or set " +
                $"'defaultProfile' in '{KitStorePaths.GetRepositoryPath(KitStorePaths.ConfigFileName)}' to one of them."),

            _ => null
        };
    }

    /// <summary>
    /// Maps the specified failure of the option parser to a CLI error with the exit code
    /// <see cref="ShadeKitCliExitCodes.InvalidArguments" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The option parser of <c>CreativeCoders.SysConsole.Cli.Parsing</c> reads the arguments of a command into its
    /// options before the command runs, and the CLI host lets its exceptions escape. They are not core exceptions, and
    /// no command can catch them, so <see cref="Program" /> maps them with this method. It is <see langword="static" />
    /// because the failure happens inside the CLI host, where no service of the run can be resolved.
    /// </para>
    /// <para>
    /// A <see cref="RequiredArgumentMissingException" /> names the missing option (<c>--&lt;long name&gt;</c>, else
    /// <c>-&lt;short name&gt;</c>) or the missing positional argument (<c>&lt;name&gt;</c>, as the help shows it). A
    /// <see cref="NotAllArgumentsMatchException" /> names the arguments that match no option. An
    /// <see cref="OptionParserException" />, whose message names only the type of the options, gets a generic text.
    /// Each of them is also mapped when it is wrapped in one or more <see cref="TargetInvocationException" />
    /// instances. Arguments without a name or a value are left out of the text.
    /// </para>
    /// <para>
    /// An <see cref="CreativeCoders.SysConsole.Cli.Parsing.Exceptions.OptionCreationFailedException" /> is not mapped:
    /// it means that the options type of a command cannot be created, which is a defect of the CLI and not of the
    /// arguments, so it remains an unexpected failure of the run.
    /// </para>
    /// <para>
    /// The message has the form <c>Invalid arguments: &lt;detail&gt;. Run 'shade-kit &lt;command&gt; --help' for
    /// usage.</c>. The command is the longest command path of the CLI that <paramref name="args" /> start with; if
    /// there is none, the hint names <c>shade-kit --help</c>.
    /// </para>
    /// </remarks>
    /// <param name="exception">The exception that ended the run.</param>
    /// <param name="args">The command-line arguments of the run.</param>
    /// <returns>
    /// The CLI error for <paramref name="exception" />, or <see langword="null" /> if it is not a failure of the
    /// option parser.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="exception" /> or <paramref name="args" /> is <see langword="null" />.
    /// </exception>
    public static CliError? MapArgumentError(Exception exception, IReadOnlyList<string> args)
    {
        Ensure.NotNull(exception);
        Ensure.NotNull(args);

        var detail = Unwrap(exception) switch
        {
            RequiredArgumentMissingException e => CreateRequiredArgumentMissingText(e),
            NotAllArgumentsMatchException e => CreateNotMatchedArgumentsText(e),
            OptionParserException => "the arguments could not be read",
            _ => null
        };

        return detail is null
            ? null
            : new CliError(ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments,
                $"Invalid arguments: {detail}. Run '{CreateHelpCommandText(args)}' for usage.");
    }

    /// <summary>
    /// Maps the specified cancellation of a run to a CLI error with the exit code
    /// <see cref="ShadeKitCliExitCodes.Canceled" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A command passes the token of <see cref="ICliCancellation" /> to the core, and a core call that stops because of
    /// it throws an <see cref="OperationCanceledException" />. That exception is not a core exception and escapes the
    /// command, so <see cref="Program" /> maps it with this method. It is <see langword="static" /> for the same reason
    /// as <see cref="MapArgumentError" />.
    /// </para>
    /// <para>
    /// An <see cref="OperationCanceledException" /> counts as a cancellation of the run if
    /// <paramref name="cancellationToken" /> is canceled, or if the token of the exception is canceled. The second
    /// case covers a replaced <see cref="ICliCancellation" /> and tokens that are linked to the token of the run. The
    /// CLI has no other source of cancellation, such as a timeout. An <see cref="OperationCanceledException" />
    /// without a canceled token is not mapped and remains an unexpected failure. The exception is also mapped when it
    /// is wrapped in one or more <see cref="TargetInvocationException" /> instances.
    /// </para>
    /// </remarks>
    /// <param name="exception">The exception that ended the run.</param>
    /// <param name="cancellationToken">The cancellation token of the run.</param>
    /// <returns>
    /// The CLI error with the message <c>The command was canceled.</c>, or <see langword="null" /> if
    /// <paramref name="exception" /> is not a cancellation of the run.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception" /> is <see langword="null" />.</exception>
    public static CliError? MapCancellation(Exception exception, CancellationToken cancellationToken)
    {
        Ensure.NotNull(exception);

        return Unwrap(exception) is OperationCanceledException canceled &&
               (cancellationToken.IsCancellationRequested || canceled.CancellationToken.IsCancellationRequested)
            ? new CliError(ShadeKitCliExitCodes.Canceled, ShadeKitCliErrorCodes.Canceled, "The command was canceled.")
            : null;
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

    /// <summary>
    /// Maps the specified feature warning to the warning object of a JSON envelope.
    /// </summary>
    /// <param name="warning">The warning to map.</param>
    /// <returns>The warning with its code from <see cref="ShadeKitCliWarningCodes" /> and a message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="warning" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="warning" /> is a <see cref="FeatureWarningKind.CurrentFeatureMismatch" /> or
    /// <see cref="FeatureWarningKind.FeatureIdMismatch" /> warning without
    /// <see cref="FeatureWarning.OtherFeatureId" />, or a <see cref="FeatureWarningKind.CurrentFeatureInvalid" />
    /// warning without a <see cref="FeatureWarning.Detail" /> that contains text.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The kind of <paramref name="warning" /> is not defined.</exception>
    public static CliJsonMessage MapWarning(FeatureWarning warning)
    {
        Ensure.NotNull(warning);

        var currentFeaturePath = KitStorePaths.GetRepositoryPath(KitStorePaths.CurrentFeatureFilePath);

        return warning.Kind switch
        {
            FeatureWarningKind.CurrentFeatureMismatch => new CliJsonMessage(
                ShadeKitCliWarningCodes.CurrentFeatureMismatch,
                CreateCurrentFeatureMismatchMessage(warning.FeatureId,
                    warning.OtherFeatureId ?? throw new ArgumentException(
                        "A current-feature-mismatch warning needs the feature of the branch.", nameof(warning)))),

            FeatureWarningKind.CurrentFeatureMissing => new CliJsonMessage(
                ShadeKitCliWarningCodes.CurrentFeatureMissing,
                $"The local override '{currentFeaturePath}' selects feature '{warning.FeatureId}', which does not " +
                "exist; the override is ignored. Run 'shade-kit feature set <id>' with the id of an existing " +
                "feature to replace it."),

            FeatureWarningKind.FeatureFileMissing => new CliJsonMessage(ShadeKitCliWarningCodes.FeatureFileMissing,
                $"The feature folder '{GetFeatureDirectoryPath(warning.FeatureId)}' has no " +
                $"'{KitStorePaths.FeatureFileName}' and was skipped."),

            FeatureWarningKind.FeatureFolderInvalid => new CliJsonMessage(
                ShadeKitCliWarningCodes.FeatureFolderInvalid,
                $"The folder '{GetFeatureDirectoryPath(warning.FeatureId)}' is not a valid feature id and was " +
                "skipped."),

            FeatureWarningKind.FeatureIdMismatch => new CliJsonMessage(ShadeKitCliWarningCodes.FeatureIdMismatch,
                CreateFeatureIdMismatchMessage(warning.FeatureId,
                    warning.OtherFeatureId ?? throw new ArgumentException(
                        "A feature-id-mismatch warning needs the id of the feature file.", nameof(warning)))),

            FeatureWarningKind.CurrentFeatureInvalid => new CliJsonMessage(
                ShadeKitCliWarningCodes.CurrentFeatureInvalid,
                $"The local override '{currentFeaturePath}' cannot be used and is ignored: " +
                CreateSentence(string.IsNullOrWhiteSpace(warning.Detail)
                    ? throw new ArgumentException("A current-feature-invalid warning needs the reason.",
                        nameof(warning))
                    : warning.Detail) +
                " Run 'shade-kit feature set <id>' to replace it."),

            _ => throw new ArgumentOutOfRangeException(nameof(warning), warning.Kind,
                "The warning kind is not supported.")
        };
    }

    // The host calls commands with options through reflection, which wraps exceptions of synchronous commands.
    private static Exception Unwrap(Exception exception)
    {
        var cause = exception;

        while (cause is TargetInvocationException { InnerException: { } innerException })
        {
            cause = innerException;
        }

        return cause;
    }

    private static string CreateRequiredArgumentMissingText(RequiredArgumentMissingException exception)
    {
        if (exception.ParameterAttribute is { } parameter)
        {
            var optionName = parameter switch
            {
                { LongName: { Length: > 0 } longName } => $"--{longName}",
                { ShortName: { Length: > 0 } shortName } => $"-{shortName}",
                _ => exception.MissingProperty.Name
            };

            return $"the required option '{optionName}' is missing";
        }

        var argumentName = exception.ValueAttribute?.Name is { Length: > 0 } name
            ? name
            : exception.MissingProperty.Name;

        return $"the required argument <{argumentName}> is missing";
    }

    private static string CreateNotMatchedArgumentsText(NotAllArgumentsMatchException exception)
    {
        var arguments = exception.NotMatchedArgs
            .Select(FormatOptionArgument)
            .Where(x => x.Length > 0)
            .Select(x => $"'{x}'")
            .ToArray();

        return arguments.Length switch
        {
            0 => "some arguments match no option",
            1 => $"the argument {arguments[0]} matches no option",
            _ => $"the arguments {string.Join(", ", arguments)} match no option"
        };
    }

    private static string FormatOptionArgument(OptionArgument argument)
    {
        var name = argument.Kind switch
        {
            OptionArgumentKind.LongName => $"--{argument.OptionName}",
            OptionArgumentKind.ShortName => $"-{argument.OptionName}",
            _ => null
        };

        if (name is null)
        {
            return argument.Value ?? string.Empty;
        }

        return argument.Value is null
            ? name
            : $"{name} {argument.Value}";
    }

    private static string CreateHelpCommandText(IReadOnlyList<string> args)
    {
        var commandPath = typeof(CliErrorMapper).Assembly.GetTypes()
            .Select(x => x.GetCustomAttribute<CliCommandAttribute>()?.Commands)
            .OfType<string[]>()
            .Where(x => x.Length > 0 && x.Length <= args.Count &&
                        x.Select((command, index) => string.Equals(command, args[index], StringComparison.Ordinal))
                            .All(isMatch => isMatch))
            .MaxBy(x => x.Length);

        return commandPath is null
            ? "shade-kit --help"
            : $"shade-kit {string.Join(' ', commandPath)} --help";
    }

    private static string GetFeaturesDirectoryPath()
    {
        return KitStorePaths.GetRepositoryPath($"{KitStorePaths.FeaturesDirName}/");
    }

    private static string GetFeatureDirectoryPath(string featureId)
    {
        return KitStorePaths.GetRepositoryPath($"{KitStorePaths.GetFeatureDirPath(featureId)}/");
    }

    private static string CreateFeatureIdMismatchMessage(string folderFeatureId, string fileFeatureId)
    {
        return $"'{GetFeatureDirectoryPath(folderFeatureId)}{KitStorePaths.FeatureFileName}' has the id " +
               $"'{fileFeatureId}'; the folder name '{folderFeatureId}' is used.";
    }

    private static string CreateSentence(string text)
    {
        return text.EndsWith('.') ? text : text + ".";
    }

    private static string CreateCauseSentence(Exception? cause)
    {
        return string.IsNullOrWhiteSpace(cause?.Message) ? "unknown." : CreateSentence(cause.Message);
    }

    private static string CreateCurrentFeatureMismatchMessage(string overrideFeatureId, string branchFeatureId)
    {
        return $"The current branch selects feature '{branchFeatureId}', but the local override selects " +
               $"'{overrideFeatureId}'. The branch wins. Run 'shade-kit feature set {branchFeatureId}' to make the " +
               $"override match the branch, or switch to the branch of '{overrideFeatureId}' to work on it.";
    }

    private static string CreateVersionMismatchText(string filePath, int fileVersion, int currentVersion)
    {
        return $"'{filePath}' uses format version {fileVersion}; this shade-kit uses version {currentVersion}.";
    }

    private static string CreateGitIdentityMissingMessage(GitIdentityNotConfiguredException exception)
    {
        var missingKeys = exception.MissingKeys is { Count: > 0 } keys ? keys : ["user.name or user.email"];
        var verb = missingKeys.Count == 1 ? "is" : "are";
        var missingKeysText = $"{string.Join(" and ", missingKeys)} {verb}";

        return $"The git identity is not configured: {missingKeysText} not set for the repository " +
               $"'{exception.RepositoryRootPath}'. Set each missing value with 'git config --global <key> <value>', " +
               "then run the command again.";
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
