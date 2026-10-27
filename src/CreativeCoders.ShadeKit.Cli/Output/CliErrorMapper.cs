using System.Reflection;
using CreativeCoders.Cli.Core;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Git.Warnings;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Init.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Web.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;
using CreativeCoders.SysConsole.Cli.Parsing;
using CreativeCoders.SysConsole.Cli.Parsing.Exceptions;

namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Maps the exceptions and warnings of the SHADE kit core to the exit codes, error codes, and messages of the CLI.
/// </summary>
/// <remarks>
/// The only place that assigns exit codes, including those of option parser failures and of a canceled run.
/// </remarks>
/// <param name="engineVersionProvider">The provider of the running engine version, which messages mention.</param>
public sealed class CliErrorMapper(IEngineVersionProvider engineVersionProvider)
{
    private const string PhaseNextHint = "Run 'shade-kit phase next' to see the possible next steps.";

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
            KitRootPathNotFoundException e => CliError.Create(ShadeKitCliExitCodes.KitRootNotFound,
                $"No SHADE kit root directory '{KitStorePaths.RootPath}' found at the repository root " +
                $"'{e.GitRootPath}'. Run 'shade-kit init' to set up SHADE kit."),

            KitRootNotADirectoryException e => CliError.Create(ShadeKitCliExitCodes.KitRootNotADirectory,
                $"'{e.KitRootPath}' is a file, but SHADE kit needs a '{KitStorePaths.RootPath}' directory at the " +
                "repository root. Move or remove the file, then run 'shade-kit init'."),

            NotAGitRepositoryException e => CliError.Create(ShadeKitCliExitCodes.NotAGitRepository,
                $"'{e.StartPath}' is not inside a git repository. SHADE kit works only in a git repository; run " +
                "shade-kit inside one."),

            FormatTooNewException e => CliError.Create(ShadeKitCliExitCodes.FormatTooNew,
                CreateFormatTooNewMessage(e)),

            UpgradeNeededException e => CliError.Create(ShadeKitCliExitCodes.UpgradeNeeded,
                $"{CreateVersionMismatchText(e.FilePath, e.FileVersion, e.CurrentVersion)} Run 'shade-kit upgrade'."),

            InvalidKitFileException e => CliError.Create(ShadeKitCliExitCodes.InvalidFile,
                $"'{e.FilePath}' is invalid: {e.Reason}"),

            KitAlreadyInitializedException e => CliError.Create(ShadeKitCliExitCodes.KitAlreadyInitialized,
                $"SHADE kit is already initialised in this repository: '{e.KitRootPath}' exists. Run " +
                "'shade-kit upgrade' to update it."),

            InvalidManagedSectionException e => CliError.Create(ShadeKitCliExitCodes.InvalidManagedSection,
                $"The SHADE kit section markers in '{e.FilePath}' are invalid: {e.Reason} Fix the file so that " +
                $"every code block is closed and the file contains exactly one " +
                $"'{ManagedInstructionSection.BeginMarkerPrefix} ... -->' line followed by exactly one " +
                $"'{ManagedInstructionSection.EndMarker}' line outside code blocks, then run the command again."),

            UnsupportedFileEncodingException e => CliError.Create(ShadeKitCliExitCodes.UnsupportedFileEncoding,
                $"'{e.FilePath}' is encoded as {e.EncodingName}, but shade-kit can only change UTF-8 files. Convert " +
                "the file to UTF-8, then run the command again."),

            PathNotAFileException e => CliError.Create(ShadeKitCliExitCodes.PathNotAFile,
                $"'{e.Path}' is a directory, but shade-kit needs a file at this path. Move or remove the " +
                "directory, then run the command again."),

            GitUnavailableException e => CliError.Create(ShadeKitCliExitCodes.GitUnavailable,
                $"'{e.CommandText}' failed. Make sure git is installed, on the PATH, and works in this repository, " +
                $"then run the command again. Details: {e.Reason}"),

            GitIdentityNotConfiguredException e => CliError.Create(ShadeKitCliExitCodes.GitIdentityMissing,
                CreateGitIdentityMissingMessage(e)),

            FeatureAlreadyExistsException e => CliError.Create(ShadeKitCliExitCodes.FeatureAlreadyExists,
                $"The feature '{e.FeatureId}' already exists; it may have been created on another branch. Nothing " +
                "was changed. Run 'shade-kit feature list' to see the existing features, or pass another --title to " +
                "create a feature with a different id."),

            FeatureNotFoundException e => CliError.Create(ShadeKitCliExitCodes.FeatureNotFound,
                $"The feature '{e.FeatureId}' does not exist. Run 'shade-kit feature list' to see the ids of the " +
                "existing features."),

            InvalidFeatureIdException e => CliError.Create(ShadeKitCliExitCodes.InvalidFeatureId,
                $"'{e.FeatureId}' is not a valid feature id. A feature id consists of groups of lowercase letters " +
                "a-z and digits joined by single hyphens, for example 'gh-142-login-sso'. Run 'shade-kit feature " +
                "list' to see the ids of the existing features."),

            InvalidFeatureSourceException e => CliError.Create(ShadeKitCliExitCodes.InvalidFeatureSource,
                $"The source '{e.SourceText}' cannot be used: {e.Reason} {CreateSourceHint(e.Problem)}"),

            InvalidFeatureTitleException e => CliError.Create(ShadeKitCliExitCodes.InvalidFeatureTitle,
                $"The feature title cannot be used: {e.Reason}"),

            FeatureIoFailedException e => CliError.Create(ShadeKitCliExitCodes.FeatureIoFailed,
                $"Could not {e.Operation} the feature '{e.FeatureId}': the folder '{e.FolderPath}' cannot be " +
                $"changed. Cause: {CreateCauseSentence(e.InnerException)} Nothing was changed. Check that you may write to " +
                $"'{GetFeaturesDirectoryPath()}' and that no other program, such as an editor, a virus scanner, or " +
                "a search indexer, holds a file in the folder open, then run the command again."),

            NoCurrentFeatureException => CliError.Create(ShadeKitCliExitCodes.NoCurrentFeature,
                "No current feature is selected: neither the current branch nor the local override " +
                $"'{KitStorePaths.GetRepositoryPath(KitStorePaths.CurrentFeatureFilePath)}' selects an existing " +
                "feature. Run 'shade-kit feature list' to see the existing features, then 'shade-kit feature set " +
                "<id>' to select one, or switch to the branch of the feature."),

            UnknownProfileException e => CliError.Create(ShadeKitCliExitCodes.UnknownProfile,
                CreateUnknownProfileMessage(e)),

            PhaseNotInProfileException e => CliError.Create(ShadeKitCliExitCodes.PhaseNotInProfile,
                CreatePhaseNotInProfileMessage(e)),

            PhaseTransitionNotAllowedException e => CliError.Create(ShadeKitCliExitCodes.TransitionNotAllowed,
                $"The feature '{e.FeatureId}' cannot move to the phase '{e.Phase}': " +
                $"{CreateTransitionRefusalSentence(e)} The current phase is '{e.CurrentPhase}' " +
                $"({e.CurrentState.ToJsonName()}). Nothing was changed. {PhaseNextHint}"),

            NoPendingTransitionException e => CliError.Create(ShadeKitCliExitCodes.NoPendingTransition,
                $"The feature '{e.FeatureId}' has no pending transition to '{e.Phase}'; " +
                $"{CreatePendingTransitionText(e.PendingTransition)} Nothing was changed. {PhaseNextHint}"),

            FeatureDoneException e => CliError.Create(ShadeKitCliExitCodes.FeatureDone,
                $"The feature '{e.FeatureId}' is done, so its phases cannot change any more. Nothing was changed. " +
                "Run 'shade-kit feature list' to see the features, and 'shade-kit feature set <id>' to work on " +
                "another one."),

            PhaseCompleteNotAllowedException e => CliError.Create(ShadeKitCliExitCodes.CompleteNotAllowed,
                $"The feature '{e.FeatureId}' cannot be completed: only the last phase '{e.LastPhase}' can be " +
                $"completed while it is in progress, but the current phase is '{e.CurrentPhase}' " +
                $"({e.CurrentState.ToJsonName()}). Nothing was changed. {PhaseNextHint}"),

            InvalidPhaseStateException e => CliError.Create(ShadeKitCliExitCodes.PhaseStateInvalid,
                $"The phase state in '{e.FeatureFilePath}' is invalid: the field '{e.Field}' {e.Reason} Fix the " +
                "field or restore the file from git, then run the command again."),

            _ => null
        };
    }

    /// <summary>
    /// Maps the specified failure of the option parser to a CLI error with the exit code
    /// <see cref="ShadeKitCliExitCodes.InvalidArguments" />.
    /// </summary>
    /// <remarks>
    /// <see langword="static" />, because the failure happens in the CLI host before any service of the run exists.
    /// Exceptions wrapped in <see cref="TargetInvocationException" /> are unwrapped. An
    /// <see cref="CreativeCoders.SysConsole.Cli.Parsing.Exceptions.OptionCreationFailedException" /> is not mapped: it
    /// is a defect of the CLI, not of the arguments.
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
            : CliError.Create(ShadeKitCliExitCodes.InvalidArguments,
                $"Invalid arguments: {detail}. Run '{CreateHelpCommandText(args)}' for usage.");
    }

    /// <summary>
    /// Maps the specified cancellation of a run to a CLI error with the exit code
    /// <see cref="ShadeKitCliExitCodes.Canceled" />.
    /// </summary>
    /// <remarks>
    /// It counts as a cancellation if <paramref name="cancellationToken" /> or the token of the exception is canceled,
    /// also when the exception is wrapped in <see cref="TargetInvocationException" />.
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
            ? CliError.Create(ShadeKitCliExitCodes.Canceled, "The command was canceled.")
            : null;
    }

    /// <summary>
    /// Maps the specified warning of the SHADE kit core to the warning object of a JSON envelope.
    /// </summary>
    /// <param name="warning">The warning to map.</param>
    /// <returns>
    /// The warning with a message and its code: from <see cref="ShadeKitCliErrorCodes" /> for a
    /// <see cref="FormatWarning" />, otherwise from <see cref="ShadeKitCliWarningCodes" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="warning" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The type of <paramref name="warning" />, the kind of a <see cref="FormatWarning" />, or the type of the problem
    /// of a <see cref="CurrentFeatureInvalidWarning" /> is not supported.
    /// </exception>
    public static CliJsonMessage MapWarning(KitWarning warning)
    {
        Ensure.NotNull(warning);

        var currentFeaturePath = KitStorePaths.GetRepositoryPath(KitStorePaths.CurrentFeatureFilePath);

        return warning switch
        {
            FormatWarning { Kind: FormatWarningKind.UpgradeNeeded } w => new CliJsonMessage(
                ShadeKitCliErrorCodes.UpgradeNeeded,
                $"{CreateVersionMismatchText(w.FilePath, w.FileVersion, w.CurrentVersion)} " +
                "It was migrated in memory; run 'shade-kit upgrade' to update the file."),

            CurrentFeatureMismatchWarning w => new CliJsonMessage(ShadeKitCliWarningCodes.CurrentFeatureMismatch,
                CreateCurrentFeatureMismatchMessage(w.OverrideFeatureId, w.BranchFeatureId)),

            CurrentFeatureMissingWarning w => new CliJsonMessage(ShadeKitCliWarningCodes.CurrentFeatureMissing,
                $"The local override '{currentFeaturePath}' selects feature '{w.OverrideFeatureId}', which does not " +
                "exist; the override is ignored. Run 'shade-kit feature set <id>' with the id of an existing " +
                "feature to replace it."),

            FeatureFileMissingWarning w => new CliJsonMessage(ShadeKitCliWarningCodes.FeatureFileMissing,
                $"The feature folder '{KitStorePaths.GetRepositoryFeatureDirPath(w.FeatureId)}' has no " +
                $"'{KitStorePaths.FeatureFileName}' and was skipped."),

            FeatureFolderInvalidWarning w => new CliJsonMessage(ShadeKitCliWarningCodes.FeatureFolderInvalid,
                $"The folder '{KitStorePaths.GetRepositoryFeatureDirPath(w.FolderName)}' is not a valid " +
                "feature id and was skipped."),

            FeatureIdMismatchWarning w => new CliJsonMessage(ShadeKitCliWarningCodes.FeatureIdMismatch,
                CreateFeatureIdMismatchMessage(w.FolderFeatureId, w.FileFeatureId)),

            CurrentFeatureInvalidWarning w => new CliJsonMessage(ShadeKitCliWarningCodes.CurrentFeatureInvalid,
                $"The local override '{currentFeaturePath}' cannot be used and is ignored: " +
                $"{CreateOverrideProblemSentence(w.Problem)} Run 'shade-kit feature set <id>' to replace it."),

            EditorConfigUnreadableWarning w => new CliJsonMessage(ShadeKitCliWarningCodes.EditorConfigUnreadable,
                CreateEditorConfigMessage(w.FilePath, w.IsEditorConfigFilePath, "cannot be read", w.Reason)),

            EditorConfigInvalidWarning w => new CliJsonMessage(ShadeKitCliWarningCodes.EditorConfigInvalid,
                CreateEditorConfigMessage(w.FilePath, w.IsEditorConfigFilePath,
                    "cannot be evaluated because of its content", w.Reason)),

            GitAttributesUnavailableWarning w => new CliJsonMessage(ShadeKitCliWarningCodes.GitAttributesUnavailable,
                $"git cannot read the line ending settings of the repository '{w.RepositoryRootPath}', so the line " +
                $"endings come from .editorconfig or the operating system. '{w.Command}' failed: {w.Reason}"),

            _ => throw new ArgumentOutOfRangeException(nameof(warning), warning,
                "The warning is not supported.")
        };
    }

    /// <summary>
    /// Returns the exception that caused the specified exception, without the <see cref="TargetInvocationException" />
    /// wrappers around it.
    /// </summary>
    /// <remarks>
    /// The host calls commands with options through reflection, which wraps exceptions of synchronous commands.
    /// </remarks>
    /// <param name="exception">The exception to unwrap.</param>
    /// <returns>
    /// The innermost exception of a chain of <see cref="TargetInvocationException" /> wrappers, or
    /// <paramref name="exception" /> if it is no such wrapper or has no inner exception.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception" /> is <see langword="null" />.</exception>
    internal static Exception Unwrap(Exception exception)
    {
        Ensure.NotNull(exception);

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

    // Chooses the hint of an exit-33 message by the problem, so a valid URL whose content cannot be loaded points to
    // gh or the URL instead of to another kind of source.
    private static string CreateSourceHint(FeatureSourceProblem problem)
    {
        return problem switch
        {
            FeatureSourceProblem.GhUnavailable =>
                "Make sure the GitHub CLI gh is installed, on the PATH, and can be executed, then run the command " +
                "again.",

            FeatureSourceProblem.IssueReadFailed =>
                "Check the issue URL and that gh is signed in for its host ('gh auth status'), then run the command " +
                "again.",

            FeatureSourceProblem.UrlReadFailed =>
                "Check the URL and run the command again, or save the content to a file and pass the path of the " +
                "file with --source.",

            _ => "Pass an issue URL, the path of an existing UTF-8 text file, or the request text with --source."
        };
    }

    private static string GetFeaturesDirectoryPath()
    {
        return KitStorePaths.GetRepositoryPath($"{KitStorePaths.FeaturesDirName}/");
    }

    private static string CreateFeatureIdMismatchMessage(string folderFeatureId, string fileFeatureId)
    {
        return $"'{KitStorePaths.GetRepositoryPath(KitStorePaths.GetFeatureFilePath(folderFeatureId))}' has the id " +
               $"'{fileFeatureId}'; the folder name '{folderFeatureId}' is used.";
    }

    // Names the path exactly once: the reason, usually the message of an exception, often contains the quoted path
    // of the .editorconfig file already ("Access to the path '...' is denied."), so that path is only added when the
    // reason does not quote it. The quotes keep a reason that names another file, such as '<path>.bak', from hiding
    // the path. If the failing .editorconfig file is not known, the path is that of the written file, which the
    // message names as the file the .editorconfig files apply to. An empty reason gives a sentence without a reason.
    private static string CreateEditorConfigMessage(string filePath, bool isEditorConfigFilePath, string problem,
        string reason)
    {
        const string fallbackSentence =
            " The .editorconfig files were ignored and the line ending of the operating system was used instead.";

        var trimmedReason = reason.Trim().TrimEnd('.').TrimEnd();

        var subject = CreateEditorConfigSubject(filePath, isEditorConfigFilePath, trimmedReason);

        return trimmedReason.Length == 0
            ? $"{subject} {problem}.{fallbackSentence}"
            : $"{subject} {problem}: {trimmedReason}.{fallbackSentence}";
    }

    private static string CreateEditorConfigSubject(string filePath, bool isEditorConfigFilePath,
        string trimmedReason)
    {
        if (!isEditorConfigFilePath)
        {
            return $"An .editorconfig file that applies to '{filePath}'";
        }

        return trimmedReason.Contains($"'{filePath}'", StringComparison.OrdinalIgnoreCase)
            ? "An .editorconfig file"
            : $"The .editorconfig file '{filePath}'";
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

    private static string CreateOverrideProblemSentence(KitFileFormatException problem)
    {
        return problem switch
        {
            InvalidKitFileException e => $"{e.Reason.TrimEnd('.')}.",
            FormatTooNewException e =>
                $"It uses format version {e.FileVersion}, but this engine supports only up to version " +
                $"{e.SupportedVersion}.",
            UpgradeNeededException e =>
                $"It uses the older format version {e.FileVersion} (current version {e.CurrentVersion}), and " +
                "'outdatedFormatOnRead' is 'stop'.",
            _ => throw new ArgumentOutOfRangeException(nameof(problem), problem.GetType(),
                "The format problem is not supported.")
        };
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

    private static string CreateUnknownProfileMessage(UnknownProfileException exception)
    {
        var knownProfilesText = exception.KnownProfiles is { Count: > 0 } knownProfiles
            ? string.Join(", ", knownProfiles.Select(x => $"'{x}'"))
            : "the known profiles";

        // A profile read from feature.json names the file; only a new feature has --profile and defaultProfile.
        if (exception.FeatureFilePath is { } featureFilePath)
        {
            return $"The workflow profile '{exception.Profile}' in the field 'profile' of '{featureFilePath}' is " +
                   $"unknown. Set the field to one of {knownProfilesText} or restore the file from git, then run " +
                   "the command again.";
        }

        return $"The workflow profile '{exception.Profile}' is unknown. Pass one of {knownProfilesText} with " +
               $"--profile, or set 'defaultProfile' in '{KitStorePaths.GetRepositoryPath(KitStorePaths.ConfigFileName)}' " +
               "to one of them.";
    }

    private static string CreatePhaseNotInProfileMessage(PhaseNotInProfileException exception)
    {
        var phasesText = exception.ProfilePhases is { Count: > 0 } phases
            ? $" Its phases are {string.Join(", ", phases.Select(x => $"'{x}'"))}."
            : string.Empty;

        return $"The phase '{exception.Phase}' is not a phase of the workflow profile '{exception.Profile}' of the " +
               $"feature.{phasesText} Nothing was changed. {PhaseNextHint}";
    }

    private static string CreateTransitionRefusalSentence(PhaseTransitionNotAllowedException exception)
    {
        return exception.Reason switch
        {
            PhaseTransitionRefusalReason.OtherTransitionPending =>
                $"the transition to '{exception.PendingTransition}' is pending; approve or reject it first.",

            PhaseTransitionRefusalReason.ReopenWhilePending =>
                "an earlier phase can only be reopened while the current phase is in progress, but the transition " +
                $"to '{exception.PendingTransition}' is pending; approve or reject it first.",

            PhaseTransitionRefusalReason.PhaseCannotBeReopened =>
                $"the workflow profile does not allow to reopen the phase '{exception.Phase}'.",

            _ => "the transition would skip phases; only the phase after the current phase can be started."
        };
    }

    private static string CreatePendingTransitionText(string? pendingTransition)
    {
        return pendingTransition is null
            ? "no transition is pending."
            : $"the pending transition is to '{pendingTransition}'.";
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
