using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.New;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Store;
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
                "'/repo/CLAUDE.md' is a directory, but shade-kit must write a file at this path. Move or remove " +
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
        var error = mapper.Map(new UnknownProfileException("hotfix"));

        // Assert
        error
            .Should().Be(new CliError(ShadeKitCliExitCodes.UnknownProfile, ShadeKitCliErrorCodes.UnknownProfile,
                "The workflow profile 'hotfix' is unknown. Pass one of 'feature', 'bugfix', 'quick' with " +
                "--profile, or set 'defaultProfile' in '.shadekit/config.json' to one of them."));
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
        var act = () => CliErrorMapper.MapWarning((FormatWarning)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("warning");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC5")]
    public void MapWarning_CurrentFeatureMismatch_ReturnsMismatchWarningNamingBothFeatures()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(FeatureWarning.CurrentFeatureMismatch("override-id", "branch-id"));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.CurrentFeatureMismatch);

        warning.Message
            .Should().Be("The current branch selects feature 'branch-id', but the local override selects " +
                         "'override-id'. The branch wins. Run 'shade-kit feature set branch-id' to make the " +
                         "override match the branch, or switch to the branch of 'override-id' to work on it.");
    }

    [Fact]
    public void MapWarning_CurrentFeatureMismatchWithoutBranchFeature_ThrowsArgumentException()
    {
        // Act
        var act = () => CliErrorMapper.MapWarning(
            new FeatureWarning(FeatureWarningKind.CurrentFeatureMismatch, "override-id"));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("warning");
    }

    [Fact]
    public void MapWarning_CurrentFeatureMissing_ReturnsMissingWarningWithSetHint()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(FeatureWarning.CurrentFeatureMissing("gone"));

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
        var warning = CliErrorMapper.MapWarning(FeatureWarning.FeatureFileMissing("login"));

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
        var warning = CliErrorMapper.MapWarning(FeatureWarning.FeatureFolderInvalid("Draft Folder"));

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
        var warning = CliErrorMapper.MapWarning(FeatureWarning.FeatureIdMismatch("copy-of-login", "login"));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.FeatureIdMismatch);

        warning.Message
            .Should().Be("'.shadekit/features/copy-of-login/feature.json' has the id 'login'; the folder name " +
                         "'copy-of-login' is used.");
    }

    [Fact]
    public void MapWarning_FeatureIdMismatchWithoutFileId_ThrowsArgumentException()
    {
        // Act
        var act = () => CliErrorMapper.MapWarning(new FeatureWarning(FeatureWarningKind.FeatureIdMismatch, "login"));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("warning");
    }

    [Theory]
    [InlineData("It uses format version 99, but this engine supports only up to version 1")]
    [InlineData("It uses format version 99, but this engine supports only up to version 1.")]
    public void MapWarning_CurrentFeatureInvalid_ReturnsInvalidWarningWithReasonAndSetHint(string reason)
    {
        // Act
        var warning = CliErrorMapper.MapWarning(FeatureWarning.CurrentFeatureInvalid(reason));

        // Assert
        warning.Code
            .Should().Be(ShadeKitCliWarningCodes.CurrentFeatureInvalid);

        warning.Message
            .Should().Be("The local override '.shadekit/features/current-feature.json' cannot be used and is " +
                         "ignored: It uses format version 99, but this engine supports only up to version 1. Run " +
                         "'shade-kit feature set <id>' to replace it.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapWarning_CurrentFeatureInvalidWithoutDetail_ThrowsArgumentException(string? detail)
    {
        // Act
        var act = () => CliErrorMapper.MapWarning(
            new FeatureWarning(FeatureWarningKind.CurrentFeatureInvalid, string.Empty, Detail: detail));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("warning");
    }

    [Fact]
    public void MapWarning_EveryFeatureWarningKind_ReturnsDistinctWarningCode()
    {
        // Act
        var codes = Enum.GetValues<FeatureWarningKind>()
            .Select(kind => CliErrorMapper.MapWarning(new FeatureWarning(kind, "login", "other", "Detail")).Code)
            .ToArray();

        // Assert
        codes
            .Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(ShadeKitCliExitCodesTests.GetConstants<string>(typeof(ShadeKitCliWarningCodes)).Values);
    }

    [Fact]
    public void MapWarning_CurrentFeatureMissingWithInvalidId_ContainsIdVerbatim()
    {
        // Act
        var warning = CliErrorMapper.MapWarning(FeatureWarning.CurrentFeatureMissing("../x"));

        // Assert
        warning.Message
            .Should().Contain("selects feature '../x', which does not exist");
    }

    [Fact]
    public void MapWarning_UndefinedFeatureWarningKind_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => CliErrorMapper.MapWarning(new FeatureWarning((FeatureWarningKind)99, "login"));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("warning");
    }

    [Fact]
    public void MapWarning_NullFeatureWarning_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliErrorMapper.MapWarning((FeatureWarning)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
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
        var exceptionTypes = typeof(KitFileFormatException).Assembly.GetTypes()
            .Where(x => x is { IsAbstract: false, IsPublic: true } && x.IsAssignableTo(typeof(Exception)))
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
}
