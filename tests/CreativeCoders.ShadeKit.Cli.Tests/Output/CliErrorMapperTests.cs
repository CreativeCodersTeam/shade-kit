using System.Runtime.CompilerServices;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Store;
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
}
