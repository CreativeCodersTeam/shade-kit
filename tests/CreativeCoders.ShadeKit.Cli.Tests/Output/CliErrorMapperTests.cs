using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.New;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Git.Warnings;
using CreativeCoders.ShadeKit.Core.Init.Exceptions;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Web.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;
using CreativeCoders.SysConsole.Cli.Parsing;
using CreativeCoders.SysConsole.Cli.Parsing.Exceptions;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

public class CliErrorMapperTests
{
    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Map_KitRootNotFoundInRepository_ReturnsExitCode10WithInitHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new KitRootPathNotFoundException("/repo/src", "/repo"));

        // Assert
        error
            .Should().NotBeNull();

        error.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        error.Code
            .Should().Be(ShadeKitCliErrorCodes.KitRootNotFound);

        error.Message
            .Should().Contain("'/repo'")
            .And.Contain("shade-kit init");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Map_NotAGitRepository_ReturnsExitCode12NamingStartPathWithoutInitHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new NotAGitRepositoryException("/tmp/work"));

        // Assert
        error
            .Should().NotBeNull();

        error.ExitCode
            .Should().Be(ShadeKitCliExitCodes.NotAGitRepository);

        error.Code
            .Should().Be(ShadeKitCliErrorCodes.NotAGitRepository);

        error.Message
            .Should().Contain("'/tmp/work'")
            .And.Contain("not inside a git repository")
            .And.NotContain("shade-kit init", "init refuses to work outside a git repository");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC4")]
    public void Map_FormatTooNewWithMinEngineVersion_ReturnsExitCode20WithRequiredAndInstalledVersion()
    {
        // Arrange
        var mapper = CreateMapper("1.0.0");

        // Act
        var error = mapper.Map(new FormatTooNewException(".shadekit/config.json", 2, 1, "1.4.0"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.FormatTooNew, ShadeKitCliErrorCodes.FormatTooNew,
                "'.shadekit/config.json' requires shade-kit >= 1.4.0 (installed: 1.0.0)."));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC4")]
    public void Map_FormatTooNewWithoutMinEngineVersion_ReturnsExitCode20AskingForNewerEngine()
    {
        // Arrange
        var mapper = CreateMapper("1.0.0");

        // Act
        var error = mapper.Map(new FormatTooNewException(".shadekit/config.json", 2, 1, null));

        // Assert
        error
            .Should().NotBeNull();

        error.ExitCode
            .Should().Be(ShadeKitCliExitCodes.FormatTooNew);

        error.Message
            .Should().Contain("'.shadekit/config.json'")
            .And.Contain("format version 2")
            .And.Contain("newer shade-kit")
            .And.Contain("installed: 1.0.0");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC5")]
    public void Map_UpgradeNeeded_ReturnsExitCode21WithUpgradeHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new UpgradeNeededException(".shadekit/config.json", 1, 2));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.UpgradeNeeded, ShadeKitCliErrorCodes.UpgradeNeeded,
                "'.shadekit/config.json' uses format version 1; this shade-kit uses version 2. " +
                "Run 'shade-kit upgrade'."));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC11")]
    public void Map_InvalidKitFile_ReturnsExitCode22WithReason()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidKitFileException(".shadekit/config.json",
            "The 'schemaVersion' field is missing."));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidFile, ShadeKitCliErrorCodes.InvalidFile,
                "'.shadekit/config.json' is invalid: The 'schemaVersion' field is missing."));
    }

    [Fact]
    [Trait("Requirement", "FP02-AC6")]
    public void Map_KitAlreadyInitialized_ReturnsExitCode13NamingKitRootWithUpgradeHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new KitAlreadyInitializedException("/repo/.shadekit"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.KitAlreadyInitialized,
                ShadeKitCliErrorCodes.KitAlreadyInitialized,
                "SHADE kit is already initialised in this repository: '/repo/.shadekit' exists. Run " +
                "'shade-kit upgrade' to update it."));
    }

    [Fact]
    public void Map_InvalidManagedSection_ReturnsExitCode23NamingFileReasonAndMarkerFix()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidManagedSectionException("/repo/CLAUDE.md",
            "The begin marker in line 3 has no end marker."));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidManagedSection,
                ShadeKitCliErrorCodes.InvalidManagedSection,
                "The SHADE kit section markers in '/repo/CLAUDE.md' are invalid: The begin marker in line 3 has no " +
                "end marker. Fix the file so that every code block is closed and the file contains exactly one " +
                "'<!-- shade-kit:begin ... -->' line followed by exactly one '<!-- shade-kit:end -->' line outside " +
                "code blocks, then run the command again."));
    }

    [Fact]
    public void Map_UnsupportedFileEncoding_ReturnsExitCode24NamingFileEncodingAndConversionHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new UnsupportedFileEncodingException("/repo/.gitignore", "UTF-16 LE"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.UnsupportedFileEncoding,
                ShadeKitCliErrorCodes.UnsupportedFileEncoding,
                "'/repo/.gitignore' is encoded as UTF-16 LE, but shade-kit can only change UTF-8 files. Convert " +
                "the file to UTF-8, then run the command again."));
    }

    [Fact]
    public void Map_PathNotAFile_ReturnsExitCode14NamingPath()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new PathNotAFileException("/repo/CLAUDE.md"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.PathNotAFile, ShadeKitCliErrorCodes.PathNotAFile,
                "'/repo/CLAUDE.md' is a directory, but shade-kit needs a file at this path. Move or remove " +
                "the directory, then run the command again."));
    }

    [Fact]
    public void Map_KitRootNotADirectory_ReturnsExitCode15NamingPathWithInitHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new KitRootNotADirectoryException("/repo/.shadekit"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.KitRootNotADirectory,
                ShadeKitCliErrorCodes.KitRootNotADirectory,
                "'/repo/.shadekit' is a file, but SHADE kit needs a '.shadekit' directory at the repository root. " +
                "Move or remove the file, then run 'shade-kit init'."));
    }

    [Fact]
    public void Map_GitUnavailable_ReturnsExitCode16NamingCommandAndReason()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new GitUnavailableException(["config", "user.name"],
            "git exited with the unexpected exit code 3: error: invalid config file"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.GitUnavailable, ShadeKitCliErrorCodes.GitUnavailable,
                "'git config user.name' failed. Make sure git is installed, on the PATH, and works in this " +
                "repository, then run the command again. Details: git exited with the unexpected exit code 3: " +
                "error: invalid config file"));
    }

    [Fact]
    public void Map_GitIdentityNotConfiguredWithOneKey_ReturnsExitCode36NamingKeyAndRepository()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new GitIdentityNotConfiguredException("/repo", ["user.email"]));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.GitIdentityMissing,
                ShadeKitCliErrorCodes.GitIdentityMissing,
                "The git identity is not configured: user.email is not set for the repository '/repo'. Set each " +
                "missing value with 'git config --global <key> <value>', then run the command again."));
    }

    [Fact]
    public void Map_GitIdentityNotConfiguredWithTwoKeys_ReturnsExitCode36NamingBothKeys()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new GitIdentityNotConfiguredException("/repo", ["user.name", "user.email"]));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.GitIdentityMissing,
                ShadeKitCliErrorCodes.GitIdentityMissing,
                "The git identity is not configured: user.name and user.email are not set for the repository " +
                "'/repo'. Set each missing value with 'git config --global <key> <value>', then run the command " +
                "again."));
    }

    [Fact]
    public void Map_GitIdentityNotConfiguredWithoutKeys_ReturnsExitCode36NamingBothPossibleKeys()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new GitIdentityNotConfiguredException("/repo", []));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.GitIdentityMissing,
                ShadeKitCliErrorCodes.GitIdentityMissing,
                "The git identity is not configured: user.name or user.email is not set for the repository " +
                "'/repo'. Set each missing value with 'git config --global <key> <value>', then run the command " +
                "again."));
    }

    [Fact]
    public void Map_InvalidFeatureId_ReturnsExitCode32NamingIdRulesAndListHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidFeatureIdException("../GH-142"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidFeatureId, ShadeKitCliErrorCodes.InvalidFeatureId,
                "'../GH-142' is not a valid feature id. A feature id consists of groups of lowercase letters a-z and " +
                "digits joined by single hyphens, for example 'gh-142-login-sso'. Run 'shade-kit feature list' to " +
                "see the ids of the existing features."));
    }

    [Fact]
    public void Map_InvalidFeatureSource_ReturnsExitCode33NamingSourceReasonAndSourceHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidFeatureSourceException("https://github.com/acme/shop",
            "The URL has no issue number."));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidFeatureSource,
                ShadeKitCliErrorCodes.InvalidFeatureSource,
                "The source 'https://github.com/acme/shop' cannot be used: The URL has no issue number. Pass an " +
                "issue URL, the path of an existing UTF-8 text file, or the request text with --source."));
    }

    [Fact]
    public void Map_InvalidFeatureSourceWithGhUnavailableProblem_ReturnsExitCode33WithGhAvailabilityHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidFeatureSourceException("https://github.com/octo/repo/issues/1",
            "The issue cannot be read: The GitHub CLI gh was not found in any absolute directory of the PATH " +
            "environment variable.", problem: FeatureSourceProblem.GhUnavailable));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidFeatureSource,
                ShadeKitCliErrorCodes.InvalidFeatureSource,
                "The source 'https://github.com/octo/repo/issues/1' cannot be used: The issue cannot be read: The " +
                "GitHub CLI gh was not found in any absolute directory of the PATH environment variable. Make sure " +
                "the GitHub CLI gh is installed, on the PATH, and can be executed, then run the command again."));
    }

    [Fact]
    public void Map_InvalidFeatureSourceWithIssueReadFailedProblem_ReturnsExitCode33WithGhSignInHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidFeatureSourceException("https://github.com/octo/repo/issues/1",
            "The issue cannot be read: gh exited with the unexpected exit code 1: Could not resolve to an issue.",
            problem: FeatureSourceProblem.IssueReadFailed));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidFeatureSource,
                ShadeKitCliErrorCodes.InvalidFeatureSource,
                "The source 'https://github.com/octo/repo/issues/1' cannot be used: The issue cannot be read: gh " +
                "exited with the unexpected exit code 1: Could not resolve to an issue. Check the issue URL and " +
                "that gh is signed in for its host ('gh auth status'), then run the command again."));
    }

    [Fact]
    public void Map_InvalidFeatureSourceWithUrlReadFailedProblem_ReturnsExitCode33WithUrlHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidFeatureSourceException("https://example.com/docs/request.md",
            "The content of the URL cannot be read: The server answered with the status code 404 (Not Found), not " +
            "with a success status code (2xx).", problem: FeatureSourceProblem.UrlReadFailed));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidFeatureSource,
                ShadeKitCliErrorCodes.InvalidFeatureSource,
                "The source 'https://example.com/docs/request.md' cannot be used: The content of the URL cannot be " +
                "read: The server answered with the status code 404 (Not Found), not with a success status code " +
                "(2xx). Check the URL and run the command again, or save the content to a file and pass the path " +
                "of the file with --source."));
    }

    [Fact]
    public void Map_InvalidFeatureSourceWithInnerExceptionAndDefaultProblem_ReturnsExitCode33WithSourceHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidFeatureSourceException("request.md",
            "The file cannot be read: Access denied.", new IOException("Access denied.")));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidFeatureSource,
                ShadeKitCliErrorCodes.InvalidFeatureSource,
                "The source 'request.md' cannot be used: The file cannot be read: Access denied. Pass an issue URL, " +
                "the path of an existing UTF-8 text file, or the request text with --source."));
    }

    [Fact]
    public void Map_InvalidFeatureTitle_ReturnsExitCode34WithReason()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidFeatureTitleException(
            "A URL source has no title from which the feature id can be built. Pass the title with --title."));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidFeatureTitle,
                ShadeKitCliErrorCodes.InvalidFeatureTitle,
                "The feature title cannot be used: A URL source has no title from which the feature id can be " +
                "built. Pass the title with --title."));
    }

    [Theory]
    [InlineData(FeatureIoFailedException.DropOperation)]
    [InlineData(FeatureIoFailedException.CreateOperation)]
    public void Map_FeatureIoFailed_ReturnsExitCode35NamingFolderOperationCauseAndHint(string operation)
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new FeatureIoFailedException("gh-142-login-sso", operation,
            new IOException("Access to the path '/repo/.shadekit/features/gh-142-login-sso' is denied.")));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.FeatureIoFailed, ShadeKitCliErrorCodes.FeatureIoFailed,
                $"Could not {operation} the feature 'gh-142-login-sso': the folder " +
                "'.shadekit/features/gh-142-login-sso/' cannot be changed. Cause: Access to the path " +
                "'/repo/.shadekit/features/gh-142-login-sso' is denied. Nothing was changed. Check that you may " +
                "write to '.shadekit/features/' and that no other program, such as an editor, a virus scanner, or " +
                "a search indexer, holds a file in the folder open, then run the command again."));
    }

    [Fact]
    public void Map_FeatureIoFailedWithEmptyCauseMessage_ReportsUnknownCause()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new FeatureIoFailedException("gh-142-login-sso",
            FeatureIoFailedException.CreateOperation, new IOException(string.Empty)));

        // Assert
        error
            .Should().NotBeNull();

        error.Message
            .Should().Contain("Cause: unknown. Nothing was changed.");
    }

    [Fact]
    public void Map_FeatureIoFailedWithCauseWithoutPeriod_EndsCauseWithPeriod()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new FeatureIoFailedException("gh-142-login-sso",
            FeatureIoFailedException.DropOperation, new UnauthorizedAccessException("Permission denied")));

        // Assert
        error
            .Should().NotBeNull();

        error.Message
            .Should().Contain("Cause: Permission denied. Nothing was changed.");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC3")]
    public void Map_FeatureAlreadyExists_ReturnsExitCode30WithOtherBranchAndListHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new FeatureAlreadyExistsException("gh-142-login-sso"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.FeatureAlreadyExists,
                ShadeKitCliErrorCodes.FeatureAlreadyExists,
                "The feature 'gh-142-login-sso' already exists; it may have been created on another branch. " +
                "Nothing was changed. Run 'shade-kit feature list' to see the existing features, or pass another " +
                "--title to create a feature with a different id."));
    }

    [Fact]
    [Trait("Requirement", "FP03-AC7")]
    public void Map_FeatureNotFound_ReturnsExitCode31WithListHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new FeatureNotFoundException("gh-7-checkout"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.FeatureNotFound, ShadeKitCliErrorCodes.FeatureNotFound,
                "The feature 'gh-7-checkout' does not exist. Run 'shade-kit feature list' to see the ids of the " +
                "existing features."));
    }

    [Fact]
    public void Map_UnknownProfile_ReturnsExitCode40NamingProfileKnownProfilesAndConfigHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new UnknownProfileException("hotfix", ["feature", "bugfix", "quick"]));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.UnknownProfile, ShadeKitCliErrorCodes.UnknownProfile,
                "The workflow profile 'hotfix' is unknown. Pass one of 'feature', 'bugfix', 'quick' with " +
                "--profile, or set 'defaultProfile' in '.shadekit/config.json' to one of them."));
    }

    [Fact]
    public void Map_UnknownProfileWithOtherKnownProfiles_ListsKnownProfilesOfException()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new UnknownProfileException("hotfix", ["alpha", "beta"]));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.UnknownProfile, ShadeKitCliErrorCodes.UnknownProfile,
                "The workflow profile 'hotfix' is unknown. Pass one of 'alpha', 'beta' with " +
                "--profile, or set 'defaultProfile' in '.shadekit/config.json' to one of them."));
    }

    [Fact]
    public void Map_UnknownProfileWithoutKnownProfiles_NamesKnownProfilesGenerically()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new UnknownProfileException("hotfix", []));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.UnknownProfile, ShadeKitCliErrorCodes.UnknownProfile,
                "The workflow profile 'hotfix' is unknown. Pass one of the known profiles with " +
                "--profile, or set 'defaultProfile' in '.shadekit/config.json' to one of them."));
    }

    [Fact]
    public void Map_UnknownProfileOfFeatureFile_ReturnsExitCode40NamingFileAndFieldInsteadOfProfileOption()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new UnknownProfileException("hotfix", ["feature", "bugfix", "quick"],
            ".shadekit/features/gh-142-login-sso/feature.json"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.UnknownProfile, ShadeKitCliErrorCodes.UnknownProfile,
                "The workflow profile 'hotfix' in the field 'profile' of " +
                "'.shadekit/features/gh-142-login-sso/feature.json' is unknown. Set the field to one of 'feature', " +
                "'bugfix', 'quick' or restore the file from git, then run the command again."));
    }

    [Fact]
    public void Map_UnknownProfileOfFeatureFileWithoutKnownProfiles_NamesKnownProfilesGenerically()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new UnknownProfileException("hotfix", [], ".shadekit/features/a/feature.json"));

        // Assert
        error!.Message
            .Should().Be("The workflow profile 'hotfix' in the field 'profile' of '.shadekit/features/a/feature.json' " +
                         "is unknown. Set the field to one of the known profiles or restore the file from git, then " +
                         "run the command again.");
    }

    [Fact]
    public void Map_NoCurrentFeature_ReturnsExitCode37WithFeatureListAndSetHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new NoCurrentFeatureException());

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.NoCurrentFeature, ShadeKitCliErrorCodes.NoCurrentFeature,
                "No current feature is selected: neither the current branch nor the local override " +
                "'.shadekit/features/current-feature.json' selects an existing feature. Run 'shade-kit feature " +
                "list' to see the existing features, then 'shade-kit feature set <id>' to select one, or switch to " +
                "the branch of the feature."));
    }

    [Fact]
    [Trait("Requirement", "FP04-AC3")]
    public void Map_PhaseNotInProfile_ReturnsExitCode41ListingPhasesOfProfile()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new PhaseNotInProfileException("refine", "quick", ["new", "quick", "finish"]));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.PhaseNotInProfile, ShadeKitCliErrorCodes.PhaseNotInProfile,
                "The phase 'refine' is not a phase of the workflow profile 'quick' of the feature. Its phases are " +
                "'new', 'quick', 'finish'. Nothing was changed. Run 'shade-kit phase next' to see the possible " +
                "next steps."));
    }

    [Fact]
    public void Map_PhaseNotInProfileWithoutPhases_OmitsPhaseList()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new PhaseNotInProfileException("refine", "quick", []));

        // Assert
        error!.Message
            .Should().Be("The phase 'refine' is not a phase of the workflow profile 'quick' of the feature. Nothing " +
                         "was changed. Run 'shade-kit phase next' to see the possible next steps.");
    }

    [Theory]
    [Trait("Requirement", "FP04-AC3")]
    [InlineData(PhaseTransitionRefusalReason.SkipsPhases, PhaseState.InProgress, null,
        "the transition would skip phases; only the phase after the current phase can be started. The current " +
        "phase is 'refine' (in-progress).")]
    [InlineData(PhaseTransitionRefusalReason.OtherTransitionPending, PhaseState.AwaitingApproval, "map",
        "the transition to 'map' is pending; approve or reject it first. The current phase is 'refine' " +
        "(awaiting-approval).")]
    [InlineData(PhaseTransitionRefusalReason.ReopenWhilePending, PhaseState.AwaitingApproval, "map",
        "an earlier phase can only be reopened while the current phase is in progress, but the transition to " +
        "'map' is pending; approve or reject it first. The current phase is 'refine' (awaiting-approval).")]
    [InlineData(PhaseTransitionRefusalReason.PhaseCannotBeReopened, PhaseState.InProgress, null,
        "the workflow profile does not allow to reopen the phase 'plan'. The current phase is 'refine' " +
        "(in-progress).")]
    public void Map_PhaseTransitionNotAllowed_ReturnsExitCode42WithReasonCurrentStateAndNextHint(
        PhaseTransitionRefusalReason reason, PhaseState currentState, string? pendingTransition,
        string expectedReasonText)
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new PhaseTransitionNotAllowedException("gh-142-login-sso", "plan", "refine",
            currentState, pendingTransition, reason));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.TransitionNotAllowed,
                ShadeKitCliErrorCodes.TransitionNotAllowed,
                $"The feature 'gh-142-login-sso' cannot move to the phase 'plan': {expectedReasonText} Nothing was " +
                "changed. Run 'shade-kit phase next' to see the possible next steps."));
    }

    [Theory]
    [Trait("Requirement", "FP04-AC4")]
    [Trait("Requirement", "FP04-AC6")]
    [InlineData(null, "no transition is pending.")]
    [InlineData("plan", "the pending transition is to 'plan'.")]
    public void Map_NoPendingTransition_ReturnsExitCode43NamingActualPendingTransitionAndNextHint(
        string? pendingTransition, string expectedPendingText)
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new NoPendingTransitionException("gh-142-login-sso", "map", pendingTransition));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.NoPendingTransition,
                ShadeKitCliErrorCodes.NoPendingTransition,
                $"The feature 'gh-142-login-sso' has no pending transition to 'map'; {expectedPendingText} Nothing " +
                "was changed. Run 'shade-kit phase next' to see the possible next steps."));
    }

    [Fact]
    public void Map_FeatureDone_ReturnsExitCode44WithFeatureListAndSetHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new FeatureDoneException("gh-142-login-sso"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.FeatureDone, ShadeKitCliErrorCodes.FeatureDone,
                "The feature 'gh-142-login-sso' is done, so its phases cannot change any more. Nothing was changed. " +
                "Run 'shade-kit feature list' to see the features, and 'shade-kit feature set <id>' to work on " +
                "another one."));
    }

    [Fact]
    [Trait("Requirement", "FP04-AC8")]
    public void Map_PhaseCompleteNotAllowed_ReturnsExitCode45NamingLastAndCurrentPhase()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new PhaseCompleteNotAllowedException("gh-142-login-sso", "verify",
            PhaseState.InProgress, "finish"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.CompleteNotAllowed, ShadeKitCliErrorCodes.CompleteNotAllowed,
                "The feature 'gh-142-login-sso' cannot be completed: only the last phase 'finish' can be completed " +
                "while it is in progress, but the current phase is 'verify' (in-progress). Nothing was changed. Run " +
                "'shade-kit phase next' to see the possible next steps."));
    }

    [Fact]
    public void Map_InvalidPhaseState_ReturnsExitCode46NamingFileFieldAndRestoreHint()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidPhaseStateException(".shadekit/features/gh-142-login-sso/feature.json",
            "currentPhase", "names the phase 'refine', which is not a phase of the workflow profile 'bugfix'."));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.PhaseStateInvalid, ShadeKitCliErrorCodes.PhaseStateInvalid,
                "The phase state in '.shadekit/features/gh-142-login-sso/feature.json' is invalid: the field " +
                "'currentPhase' names the phase 'refine', which is not a phase of the workflow profile 'bugfix'. " +
                "Fix the field or restore the file from git, then run the command again."));
    }

    [Fact]
    public void Map_UnexpectedException_ReturnsNull()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidOperationException("unexpected"));

        // Assert
        error
            .Should().BeNull();
    }

    [Fact]
    public void Map_NullException_ThrowsArgumentNullException()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var act = () => mapper.Map(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("exception");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC10")]
    public void MapWarning_UpgradeNeeded_ReturnsUpgradeNeededWarningWithUpgradeHint()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(
            new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/config.json", 1, 2));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliErrorCodes.UpgradeNeeded);

        warning.Message
            .Should().Contain("'.shadekit/config.json'")
            .And.Contain("format version 1")
            .And.Contain("version 2")
            .And.Contain("shade-kit upgrade");
    }

    [Fact]
    public void MapWarning_UndefinedKind_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => CliErrorMapper.MapWarning(new FormatWarning((FormatWarningKind)99, "file.json", 1, 2));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("warning");
    }

    [Fact]
    public void MapWarning_NullWarning_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliErrorMapper.MapWarning(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("warning");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC5")]
    public void MapWarning_CurrentFeatureMismatch_ReturnsMismatchWarningNamingBothFeatures()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new CurrentFeatureMismatchWarning("override-id", "branch-id"));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.CurrentFeatureMismatch);

        warning.Message
            .Should().Be("The current branch selects feature 'branch-id', but the local override selects " +
                         "'override-id'. The branch wins. Run 'shade-kit feature set branch-id' to make the " +
                         "override match the branch, or switch to the branch of 'override-id' to work on it.");
    }

    [Fact]
    public void MapWarning_CurrentFeatureMissing_ReturnsMissingWarningWithSetHint()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new CurrentFeatureMissingWarning("gone"));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.CurrentFeatureMissing);

        warning.Message
            .Should().Be("The local override '.shadekit/features/current-feature.json' selects feature 'gone', " +
                         "which does not exist; the override is ignored. Run 'shade-kit feature set <id>' with the " +
                         "id of an existing feature to replace it.");
    }

    [Fact]
    public void MapWarning_FeatureFileMissing_ReturnsFileMissingWarningNamingTheFolder()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new FeatureFileMissingWarning("login"));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.FeatureFileMissing);

        warning.Message
            .Should().Be("The feature folder '.shadekit/features/login/' has no 'feature.json' and was skipped.");
    }

    [Fact]
    public void MapWarning_FeatureFolderInvalid_ReturnsFolderInvalidWarningNamingTheFolder()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new FeatureFolderInvalidWarning("Draft Folder"));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.FeatureFolderInvalid);

        warning.Message
            .Should().Be("The folder '.shadekit/features/Draft Folder/' is not a valid feature id and was skipped.");
    }

    [Fact]
    public void MapWarning_FeatureIdMismatch_ReturnsIdMismatchWarningNamingFileIdAndFolder()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new FeatureIdMismatchWarning("copy-of-login", "login"));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.FeatureIdMismatch);

        warning.Message
            .Should().Be("'.shadekit/features/copy-of-login/feature.json' has the id 'login'; the folder name " +
                         "'copy-of-login' is used.");
    }

    [Fact]
    public void MapWarning_CurrentFeatureInvalidWithFormatTooNew_ReturnsInvalidWarningWithVersionsAndSetHint()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new CurrentFeatureInvalidWarning(
            new FormatTooNewException(".shadekit/features/current-feature.json", 99, 1, null)));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.CurrentFeatureInvalid);

        warning.Message
            .Should().Be("The local override '.shadekit/features/current-feature.json' cannot be used and is " +
                         "ignored: It uses format version 99, but this engine supports only up to version 1. Run " +
                         "'shade-kit feature set <id>' to replace it.");
    }

    [Fact]
    public void MapWarning_CurrentFeatureInvalidWithUpgradeNeeded_ReturnsInvalidWarningWithStopPolicyAndSetHint()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new CurrentFeatureInvalidWarning(
            new UpgradeNeededException(".shadekit/features/current-feature.json", 1, 2)));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.CurrentFeatureInvalid);

        warning.Message
            .Should().Be("The local override '.shadekit/features/current-feature.json' cannot be used and is " +
                         "ignored: It uses the older format version 1 (current version 2), and " +
                         "'outdatedFormatOnRead' is 'stop'. Run 'shade-kit feature set <id>' to replace it.");
    }

    [Theory]
    [InlineData("The file is not valid JSON")]
    [InlineData("The file is not valid JSON.")]
    [InlineData("The file is not valid JSON..")]
    public void MapWarning_CurrentFeatureInvalidWithInvalidFile_ReturnsInvalidWarningWithReasonAsOneSentence(
        string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new CurrentFeatureInvalidWarning(
            new InvalidKitFileException(".shadekit/features/current-feature.json", reason)));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.CurrentFeatureInvalid);

        warning.Message
            .Should().Be("The local override '.shadekit/features/current-feature.json' cannot be used and is " +
                         "ignored: The file is not valid JSON. Run 'shade-kit feature set <id>' to replace it.");
    }

    [Fact]
    public void MapWarning_CurrentFeatureInvalidWithUnsupportedProblem_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => CliErrorMapper.MapWarning(
            new CurrentFeatureInvalidWarning(new UnsupportedFormatException("file.json")));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("problem");
    }

    [Theory]
    [InlineData("Access to the path '/repo/.editorconfig' is denied.")]
    [InlineData("Access to the path '/repo/.editorconfig' is denied")]
    [InlineData("Access to the path '/repo/.editorconfig' is denied..")]
    [InlineData(" Access to the path '/repo/.editorconfig' is denied. ")]
    [InlineData("Access to the path '/REPO/.editorconfig' is denied.")]
    public void MapWarning_EditorConfigUnreadableWithPathInReason_NamesFileOnlyThroughReason(string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new EditorConfigUnreadableWarning("/repo/.editorconfig", reason));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.EditorConfigUnreadable);

        warning.Message
            .Should().Be($"An .editorconfig file cannot be read: {reason.Trim().TrimEnd('.')}. The .editorconfig " +
                         "files were ignored and the line ending of the operating system was used instead.");
    }

    [Theory]
    [InlineData("Permission denied")]
    [InlineData("Permission denied.")]
    public void MapWarning_EditorConfigUnreadableWithReasonWithoutPath_NamesFileAndReason(string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new EditorConfigUnreadableWarning("/repo/.editorconfig", reason));

        // Assert
        warning.Message
            .Should().Be("The .editorconfig file '/repo/.editorconfig' cannot be read: Permission denied. The " +
                         ".editorconfig files were ignored and the line ending of the operating system was used " +
                         "instead.");
    }

    [Theory]
    [InlineData("Access to the path '/repo/.editorconfig.bak' is denied.")]
    [InlineData("Access to the path '/x/repo/.editorconfig' is denied.")]
    public void MapWarning_EditorConfigUnreadableWithOtherPathInReason_NamesFileAndReason(string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new EditorConfigUnreadableWarning("/repo/.editorconfig", reason));

        // Assert
        warning.Message
            .Should().Be($"The .editorconfig file '/repo/.editorconfig' cannot be read: {reason.TrimEnd('.')}. The " +
                         ".editorconfig files were ignored and the line ending of the operating system was used " +
                         "instead.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData(" . ")]
    public void MapWarning_EditorConfigUnreadableWithEmptyReason_ReturnsSentenceWithoutReason(string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new EditorConfigUnreadableWarning("/repo/.editorconfig", reason));

        // Assert
        warning.Message
            .Should().Be("The .editorconfig file '/repo/.editorconfig' cannot be read. The .editorconfig files were " +
                         "ignored and the line ending of the operating system was used instead.");
    }

    [Fact]
    public void MapWarning_GitAttributesUnavailable_ReturnsWarningNamingRepositoryCommandReasonAndFallback()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new GitAttributesUnavailableWarning("/repo", "git check-attr",
            "git exited with the unexpected exit code 128: fatal: detected dubious ownership in repository at '/repo'"));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.GitAttributesUnavailable);

        warning.Message
            .Should().Be("git cannot read the line ending settings of the repository '/repo', so the line endings " +
                         "come from .editorconfig or the operating system. 'git check-attr' failed: git exited with " +
                         "the unexpected exit code 128: fatal: detected dubious ownership in repository at '/repo'");
    }

    [Theory]
    [InlineData("Value was either too large or too small for an Int32.")]
    [InlineData("Value was either too large or too small for an Int32")]
    public void MapWarning_EditorConfigInvalid_ReturnsInvalidWarningNamingFileReasonAndFallback(string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new EditorConfigInvalidWarning("/repo/.editorconfig", reason));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.EditorConfigInvalid);

        warning.Message
            .Should().Be("The .editorconfig file '/repo/.editorconfig' cannot be evaluated because of its content: " +
                         "Value was either too large or too small for an Int32. The .editorconfig files were ignored " +
                         "and the line ending of the operating system was used instead.");
    }

    [Fact]
    public void MapWarning_EditorConfigInvalidWithPathInReason_NamesFileOnlyThroughReason()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new EditorConfigInvalidWarning("/repo/.editorconfig",
            "The section [{1..99999999999}] of '/repo/.editorconfig' is too large."));

        // Assert
        warning.Message
            .Should().Be("An .editorconfig file cannot be evaluated because of its content: The section " +
                         "[{1..99999999999}] of '/repo/.editorconfig' is too large. The .editorconfig files were " +
                         "ignored and the line ending of the operating system was used instead.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(".")]
    [InlineData(" . ")]
    public void MapWarning_EditorConfigInvalidWithEmptyReason_ReturnsSentenceWithoutReason(string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new EditorConfigInvalidWarning("/repo/.editorconfig", reason));

        // Assert
        warning.Message
            .Should().Be("The .editorconfig file '/repo/.editorconfig' cannot be evaluated because of its content. " +
                         "The .editorconfig files were ignored and the line ending of the operating system was used " +
                         "instead.");
    }

    [Theory]
    [InlineData("Permission denied.")]
    [InlineData("Access to the path '/repo/.shadekit/spec.md' is denied.")]
    public void MapWarning_EditorConfigUnreadableWithUnknownEditorConfigFile_NamesWrittenFileAsFileItAppliesTo(
        string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(
            new EditorConfigUnreadableWarning("/repo/.shadekit/spec.md", reason, IsEditorConfigFilePath: false));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.EditorConfigUnreadable);

        warning.Message
            .Should().Be($"An .editorconfig file that applies to '/repo/.shadekit/spec.md' cannot be read: " +
                         $"{reason.TrimEnd('.')}. The .editorconfig files were ignored and the line ending of the " +
                         "operating system was used instead.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" . ")]
    public void MapWarning_EditorConfigUnreadableWithUnknownEditorConfigFileAndEmptyReason_ReturnsSentenceWithoutReason(
        string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(
            new EditorConfigUnreadableWarning("/repo/.shadekit/spec.md", reason, IsEditorConfigFilePath: false));

        // Assert
        warning.Message
            .Should().Be("An .editorconfig file that applies to '/repo/.shadekit/spec.md' cannot be read. The " +
                         ".editorconfig files were ignored and the line ending of the operating system was used " +
                         "instead.");
    }

    [Theory]
    [InlineData("Value was either too large or too small for an Int32.")]
    [InlineData("The section of '/repo/.shadekit/spec.md' is too large.")]
    public void MapWarning_EditorConfigInvalidWithUnknownEditorConfigFile_NamesWrittenFileAsFileItAppliesTo(
        string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(
            new EditorConfigInvalidWarning("/repo/.shadekit/spec.md", reason, IsEditorConfigFilePath: false));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.EditorConfigInvalid);

        warning.Message
            .Should().Be($"An .editorconfig file that applies to '/repo/.shadekit/spec.md' cannot be evaluated " +
                         $"because of its content: {reason.TrimEnd('.')}. The .editorconfig files were ignored and " +
                         "the line ending of the operating system was used instead.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" . ")]
    public void MapWarning_EditorConfigInvalidWithUnknownEditorConfigFileAndEmptyReason_ReturnsSentenceWithoutReason(
        string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(
            new EditorConfigInvalidWarning("/repo/.shadekit/spec.md", reason, IsEditorConfigFilePath: false));

        // Assert
        warning.Message
            .Should().Be("An .editorconfig file that applies to '/repo/.shadekit/spec.md' cannot be evaluated " +
                         "because of its content. The .editorconfig files were ignored and the line ending of the " +
                         "operating system was used instead.");
    }

    [Fact]
    public void MapWarning_EveryConcreteCoreWarning_ReturnsCodeAndEveryWarningCodeOnce()
    {
        // Arrange
        var warningTypes = typeof(KitWarning).Assembly.GetTypes()
            .Where(x => x is { IsAbstract: false, IsPublic: true } && x.IsAssignableTo(typeof(KitWarning)))
            .ToArray();

        // Act
        var warnings = warningTypes
            .Select(x => (Type: x, Warning: CliErrorMapper.MapWarning(CreateWarning(x))))
            .ToArray();

        // Assert
        warningTypes
            .Should().Contain(typeof(FormatWarning));

        warnings.Single(x => x.Type == typeof(FormatWarning)).Warning.Code
            .Should().Be(ShadeKitCliErrorCodes.UpgradeNeeded);

        warnings.Where(x => x.Type != typeof(FormatWarning)).Select(x => x.Warning.Code)
            .Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(ShadeKitCliExitCodesTests.GetConstants<string>(typeof(ShadeKitCliWarningCodes)).Values,
                "every feature warning has its own warning code");
    }

    [Fact]
    public void MapWarning_CurrentFeatureMissingWithInvalidId_ContainsIdVerbatim()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(new CurrentFeatureMissingWarning("../x"));

        // Assert
        warning.Message
            .Should().Contain("selects feature '../x', which does not exist");
    }

    [Fact]
    public void MapWarning_UnsupportedWarningType_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => CliErrorMapper.MapWarning(new UnsupportedWarning());

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("warning");
    }

    [Fact]
    public void ToJsonMessage_Error_ReturnsCodeAndMessage()
    {
        // Arrange
        var error = new CliError(ShadeKitCliExitCodes.InvalidFile, ShadeKitCliErrorCodes.InvalidFile, "message");

        // Act
        var message = error.ToJsonMessage();

        // Assert
        message
            .Should().Be(new CliJsonMessage(ShadeKitCliErrorCodes.InvalidFile, "message"));
    }

    [Fact]
    public void Ctor_NullEngineVersionProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CliErrorMapper(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("engineVersionProvider");
    }

    internal static CliErrorMapper CreateMapper(string engineVersion = "1.0.0")
    {
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).Returns(engineVersion);

        return new CliErrorMapper(engineVersionProvider);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    public void Map_EveryConcreteCoreException_ReturnsCliError()
    {
        // Arrange
        var mapper = CreateMapper();

        // FeatureSourceReader wraps the exceptions of the source readers into an InvalidFeatureSourceException, so
        // they never reach the mapper.
        Type[] wrappedTypes =
        [
            typeof(GhUnavailableException), typeof(GhNotFoundException), typeof(TrackerIssueReadFailedException),
            typeof(UrlContentReadFailedException)
        ];

        var exceptionTypes = typeof(KitFileFormatException).Assembly.GetTypes()
            .Where(x => x is { IsAbstract: false, IsPublic: true } && x.IsAssignableTo(typeof(Exception)))
            .Except(wrappedTypes)
            .ToArray();

        // Act
        var unmappedTypes = exceptionTypes
            .Where(x => mapper.Map((Exception)RuntimeHelpers.GetUninitializedObject(x)) is null)
            .Select(x => x.Name);

        // Assert
        exceptionTypes
            .Should().NotBeEmpty();

        unmappedTypes
            .Should().BeEmpty("every public core exception is an expected failure with its own exit code");
    }

    [Fact]
    public void Map_InvalidKitFileWithInnerException_ReturnsExitCode22()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new InvalidKitFileException(".shadekit/config.json", "Broken.",
            new System.Text.Json.JsonException("inner")));

        // Assert
        error
            .Should().NotBeNull();

        error.ExitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidFile);
    }

    [Fact]
    public void MapArgumentError_RequiredOptionMissing_ReturnsInvalidArgumentsNamingLongNameAndCommandHelp()
    {
        // Arrange
        var exception = CreateRequiredArgumentMissingException(typeof(FeatureNewCommandOptions),
            nameof(FeatureNewCommandOptions.Source));

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "new", "--title", "Login"]);

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments,
                "Invalid arguments: the required option '--source' is missing. Run 'shade-kit feature new --help' " +
                "for usage."));
    }

    [Fact]
    public void MapArgumentError_RequiredPositionalArgumentMissing_ReturnsInvalidArgumentsNamingArgument()
    {
        // Arrange
        var exception = CreateRequiredArgumentMissingException(typeof(FeatureShowCommandOptions),
            nameof(FeatureShowCommandOptions.FeatureId));

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "show", "--json"]);

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments,
                "Invalid arguments: the required argument <FeatureId> is missing. Run 'shade-kit feature show " +
                "--help' for usage."));
    }

    [Fact]
    public void MapArgumentError_RequiredPositionalArgumentWithName_NamesArgumentByAttributeName()
    {
        // Arrange
        var exception = new RequiredArgumentMissingException(
            typeof(FeatureShowCommandOptions).GetProperty(nameof(FeatureShowCommandOptions.FeatureId))!,
            new OptionValueAttribute(0) { Name = "id", IsRequired = true });

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "show"]);

        // Assert
        error!.Message
            .Should().StartWith("Invalid arguments: the required argument <id> is missing.");
    }

    [Fact]
    public void MapArgumentError_RequiredOptionWithShortNameOnly_NamesShortName()
    {
        // Arrange
        var exception = new RequiredArgumentMissingException(
            typeof(FeatureNewCommandOptions).GetProperty(nameof(FeatureNewCommandOptions.Source))!,
            new OptionParameterAttribute('s') { IsRequired = true });

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "new"]);

        // Assert
        error!.Message
            .Should().StartWith("Invalid arguments: the required option '-s' is missing.");
    }

    [Fact]
    public void MapArgumentError_WrappedInTargetInvocationException_MapsInnerException()
    {
        // Arrange
        var exception = new TargetInvocationException(CreateRequiredArgumentMissingException(
            typeof(FeatureShowCommandOptions), nameof(FeatureShowCommandOptions.FeatureId)));

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "show"]);

        // Assert
        error
            .Should().NotBeNull();

        error.Code
            .Should().Be(ShadeKitCliErrorCodes.InvalidArguments);

        error.Message
            .Should().Contain("<FeatureId>");
    }

    [Fact]
    public void MapArgumentError_OneArgumentMatchesNoOption_NamesArgument()
    {
        // Arrange
        var exception = new NotAllArgumentsMatchException(
            [new OptionArgument { Kind = OptionArgumentKind.LongName, OptionName = "bogus" }]);

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "list", "--bogus"]);

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments,
                "Invalid arguments: the argument '--bogus' matches no option. Run 'shade-kit feature list --help' " +
                "for usage."));
    }

    [Fact]
    public void MapArgumentError_SeveralArgumentsMatchNoOption_NamesEveryArgumentWithItsValue()
    {
        // Arrange
        var exception = new NotAllArgumentsMatchException(
        [
            new OptionArgument { Kind = OptionArgumentKind.LongName, OptionName = "bogus", Value = "1" },
            new OptionArgument { Kind = OptionArgumentKind.ShortName, OptionName = "x" },
            new OptionArgument { Kind = OptionArgumentKind.Value, Value = "extra" }
        ]);

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["version"]);

        // Assert
        error!.Message
            .Should().Be("Invalid arguments: the arguments '--bogus 1', '-x', 'extra' match no option. Run " +
                         "'shade-kit version --help' for usage.");
    }

    [Fact]
    public void MapArgumentError_NoArgumentListedAsNotMatching_ReturnsGenericText()
    {
        // Arrange
        var exception = new NotAllArgumentsMatchException([]);

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["version"]);

        // Assert
        error!.Message
            .Should().StartWith("Invalid arguments: some arguments match no option.");
    }

    [Fact]
    public void MapArgumentError_OptionParserException_ReturnsInvalidArgumentsWithGenericText()
    {
        // Arrange
        var exception = (OptionParserException)RuntimeHelpers.GetUninitializedObject(typeof(OptionParserException));

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "show"]);

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments,
                "Invalid arguments: the arguments could not be read. Run 'shade-kit feature show --help' for usage."));
    }

    [Theory]
    [InlineData]
    [InlineData("unknown-command")]
    [InlineData("feature")]
    [InlineData("show", "feature")]
    public void MapArgumentError_ArgumentsStartWithNoCommandPath_HintsAtRootHelp(params string[] args)
    {
        // Arrange
        var exception = CreateRequiredArgumentMissingException(typeof(FeatureShowCommandOptions),
            nameof(FeatureShowCommandOptions.FeatureId));

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, args);

        // Assert
        error!.Message
            .Should().EndWith(" Run 'shade-kit --help' for usage.");
    }

    [Fact]
    public void MapArgumentError_CommandPathInDifferentCase_HintsAtRootHelp()
    {
        // Arrange
        var exception = CreateRequiredArgumentMissingException(typeof(FeatureShowCommandOptions),
            nameof(FeatureShowCommandOptions.FeatureId));

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["Feature", "Show"]);

        // Assert
        error!.Message
            .Should().EndWith(" Run 'shade-kit --help' for usage.");
    }

    [Fact]
    public void MapArgumentError_OptionCreationFailed_ReturnsNull()
    {
        // Arrange
        var exception = new OptionCreationFailedException(typeof(FeatureShowCommandOptions));

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "show"]);

        // Assert
        error
            .Should().BeNull();
    }

    [Fact]
    public void MapCancellation_RunTokenCanceled_ReturnsCanceledError()
    {
        // Arrange
        var token = new CancellationToken(true);

        // Act
        var error = CliErrorMapper.MapCancellation(new OperationCanceledException(token), token);

        // Assert
        error
            .Should().BeEquivalentTo(new CliError(ShadeKitCliExitCodes.Canceled, ShadeKitCliErrorCodes.Canceled,
                "The command was canceled."));
    }

    [Fact]
    public void MapCancellation_RunTokenCanceledAndExceptionWithoutToken_ReturnsCanceledError()
    {
        // Act
        var error = CliErrorMapper.MapCancellation(new OperationCanceledException(), new CancellationToken(true));

        // Assert
        error!.ExitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);
    }

    [Fact]
    public void MapCancellation_ExceptionTokenCanceledAndRunTokenNot_ReturnsCanceledError()
    {
        // Act
        var error = CliErrorMapper.MapCancellation(new TaskCanceledException(null, null, new CancellationToken(true)),
            CancellationToken.None);

        // Assert
        error!.Code
            .Should().Be(ShadeKitCliErrorCodes.Canceled);
    }

    [Fact]
    public void MapCancellation_WrappedInTargetInvocationExceptions_MapsInnermostException()
    {
        // Arrange
        var token = new CancellationToken(true);
        var exception = new TargetInvocationException(
            new TargetInvocationException(new OperationCanceledException(token)));

        // Act
        var error = CliErrorMapper.MapCancellation(exception, token);

        // Assert
        error!.ExitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);
    }

    [Fact]
    public void MapCancellation_NoTokenCanceled_ReturnsNull()
    {
        // Act
        var error = CliErrorMapper.MapCancellation(new OperationCanceledException(), CancellationToken.None);

        // Assert
        error
            .Should().BeNull();
    }

    [Fact]
    public void MapCancellation_OtherExceptionWithRunTokenCanceled_ReturnsNull()
    {
        // Act
        var error = CliErrorMapper.MapCancellation(new InvalidOperationException("failed"),
            new CancellationToken(true));

        // Assert
        error
            .Should().BeNull();
    }

    [Fact]
    public void MapCancellation_WrappedExceptionWithCanceledTokenAndRunTokenNotCanceled_ReturnsCanceledError()
    {
        // Arrange
        var exception = new TargetInvocationException(new OperationCanceledException(new CancellationToken(true)));

        // Act
        var error = CliErrorMapper.MapCancellation(exception, CancellationToken.None);

        // Assert
        error!.ExitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);
    }

    [Fact]
    public void MapCancellation_WrappedOtherExceptionWithRunTokenCanceled_ReturnsNull()
    {
        // Arrange
        var exception = new TargetInvocationException(new InvalidOperationException("failed"));

        // Act
        var error = CliErrorMapper.MapCancellation(exception, new CancellationToken(true));

        // Assert
        error
            .Should().BeNull();
    }

    [Fact]
    public void MapCancellation_NullException_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliErrorMapper.MapCancellation(null!, CancellationToken.None);

        // Assert
        act
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Unwrap_NestedTargetInvocationExceptions_ReturnsInnermostException()
    {
        // Arrange
        var cause = new InvalidOperationException("failed");
        var exception = new TargetInvocationException(new TargetInvocationException(cause));

        // Act
        var result = CliErrorMapper.Unwrap(exception);

        // Assert
        result
            .Should().BeSameAs(cause);
    }

    [Fact]
    public void Unwrap_TargetInvocationExceptionWithoutInnerException_ReturnsSameException()
    {
        // Arrange
        var exception = new TargetInvocationException(null);

        // Act
        var result = CliErrorMapper.Unwrap(exception);

        // Assert
        result
            .Should().BeSameAs(exception);
    }

    [Fact]
    public void Unwrap_OtherException_ReturnsSameException()
    {
        // Arrange
        var exception = new InvalidOperationException("failed");

        // Act
        var result = CliErrorMapper.Unwrap(exception);

        // Assert
        result
            .Should().BeSameAs(exception);
    }

    [Fact]
    public void Unwrap_NullException_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliErrorMapper.Unwrap(null!);

        // Assert
        act
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Map_OperationCanceledException_ReturnsNull()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var error = mapper.Map(new OperationCanceledException(new CancellationToken(true)));

        // Assert
        error
            .Should().BeNull("a cancellation is not a core failure; Program maps it with MapCancellation");
    }

    [Fact]
    public void MapArgumentError_OtherException_ReturnsNull()
    {
        // Act
        var error = CliErrorMapper.MapArgumentError(new InvalidOperationException("failed"), ["feature", "show"]);

        // Assert
        error
            .Should().BeNull();
    }

    [Fact]
    public void MapArgumentError_TargetInvocationWithOtherInnerException_ReturnsNull()
    {
        // Arrange
        var exception = new TargetInvocationException(new InvalidOperationException("failed"));

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "show"]);

        // Assert
        error
            .Should().BeNull();
    }

    [Fact]
    public void MapArgumentError_NullException_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliErrorMapper.MapArgumentError(null!, ["version"]);

        // Assert
        act
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapArgumentError_NullArgs_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliErrorMapper.MapArgumentError(new InvalidOperationException("failed"), null!);

        // Assert
        act
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapArgumentError_WrappedInTwoTargetInvocationExceptions_MapsInnermostException()
    {
        // Arrange
        var exception = new TargetInvocationException(new TargetInvocationException(
            CreateRequiredArgumentMissingException(typeof(FeatureShowCommandOptions),
                nameof(FeatureShowCommandOptions.FeatureId))));

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "show"]);

        // Assert
        error!.Code
            .Should().Be(ShadeKitCliErrorCodes.InvalidArguments);
    }

    [Fact]
    public void MapArgumentError_NotMatchedArgumentsWrappedInTargetInvocationException_MapsInnerException()
    {
        // Arrange
        var exception = new TargetInvocationException(new NotAllArgumentsMatchException(
            [new OptionArgument { Kind = OptionArgumentKind.Value, Value = "extra" }]));

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "list", "extra"]);

        // Assert
        error!.Message
            .Should().StartWith("Invalid arguments: the argument 'extra' matches no option.");
    }

    [Fact]
    public void MapArgumentError_TargetInvocationWithoutInnerException_ReturnsNull()
    {
        // Act
        var error = CliErrorMapper.MapArgumentError(new TargetInvocationException(null), ["feature", "show"]);

        // Assert
        error
            .Should().BeNull();
    }

    [Fact]
    public void MapArgumentError_RequiredPositionalArgumentWithEmptyName_NamesProperty()
    {
        // Arrange
        var exception = new RequiredArgumentMissingException(
            typeof(FeatureShowCommandOptions).GetProperty(nameof(FeatureShowCommandOptions.FeatureId))!,
            new OptionValueAttribute(0) { Name = string.Empty, IsRequired = true });

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["feature", "show"]);

        // Assert
        error!.Message
            .Should().StartWith("Invalid arguments: the required argument <FeatureId> is missing.");
    }

    [Fact]
    public void MapArgumentError_NotMatchedArgumentsWithoutNameOrValue_LeavesThemOut()
    {
        // Arrange
        var exception = new NotAllArgumentsMatchException(
        [
            new OptionArgument { Kind = OptionArgumentKind.Value },
            new OptionArgument { Kind = OptionArgumentKind.ShortName, OptionName = "x", Value = "1" }
        ]);

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["init"]);

        // Assert
        error!.Message
            .Should().Be("Invalid arguments: the argument '-x 1' matches no option. Run 'shade-kit init --help' " +
                         "for usage.");
    }

    [Fact]
    public void MapArgumentError_OnlyNotMatchedArgumentWithoutValue_ReturnsGenericText()
    {
        // Arrange
        var exception = new NotAllArgumentsMatchException([new OptionArgument { Kind = OptionArgumentKind.Value }]);

        // Act
        var error = CliErrorMapper.MapArgumentError(exception, ["version"]);

        // Assert
        error!.Message
            .Should().StartWith("Invalid arguments: some arguments match no option.");
    }

    private static RequiredArgumentMissingException CreateRequiredArgumentMissingException(Type optionsType,
        string propertyName)
    {
        var property = optionsType.GetProperty(propertyName)!;

        return new RequiredArgumentMissingException(property, property.GetCustomAttribute<OptionBaseAttribute>()!);
    }

    private static KitWarning CreateWarning(Type warningType)
    {
        var constructor = warningType.GetConstructors().Single();
        var arguments = constructor.GetParameters()
            .Select(x => x.ParameterType switch
            {
                { } type when type == typeof(string) => "login",
                { } type when type == typeof(int) => 1,
                { } type when type == typeof(bool) => true,
                { IsEnum: true } type => Enum.GetValues(type).GetValue(0),
                { } type when type.IsAssignableFrom(typeof(InvalidKitFileException)) =>
                    new InvalidKitFileException("file.json", "Broken."),
                { } type => throw new NotSupportedException($"No test value for a parameter of type {type}.")
            })
            .ToArray();

        return (KitWarning)constructor.Invoke(arguments);
    }

    private sealed record UnsupportedWarning : KitWarning;

    private sealed class UnsupportedFormatException(string filePath)
        : KitFileFormatException(filePath, "The format is not supported.");
}
