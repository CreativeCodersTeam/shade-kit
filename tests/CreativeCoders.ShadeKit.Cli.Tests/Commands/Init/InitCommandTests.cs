using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Init;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Store;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Init;

public class InitCommandTests
{
    private static readonly RepositoryInitResult Result = new("/repo",
    [
        new InitializedFile(".shadekit/config.json", InitializedFileAction.Created),
        new InitializedFile(".shadekit/features/", InitializedFileAction.Created),
        new InitializedFile(".gitignore", InitializedFileAction.Appended),
        new InitializedFile("CLAUDE.md", InitializedFileAction.Updated)
    ]);

    [Fact]
    [Trait("Requirement", "FP02-AC1")]
    public async Task ExecuteAsync_PlainMode_WritesSummaryWithOneLinePerFileAndCommitHint()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateInitializer(Result));

        // Act
        var result = await command.ExecuteAsync(new InitCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                "Initialised SHADE kit in /repo.",
                "created  .shadekit/config.json",
                "created  .shadekit/features/",
                "appended  .gitignore",
                "updated  CLAUDE.md",
                "Commit '.shadekit/' and the changed files together with your code.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PlainMode_WritesNoJson()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateInitializer(Result), jsonOutput);

        // Act
        await command.ExecuteAsync(new InitCommandOptions());

        // Assert
        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModePathsWithMarkupCharacters_WritesPathsVerbatim()
    {
        // Arrange
        var console = new TestConsole();
        var initializer = CreateInitializer(new RepositoryInitResult("/repo/[red]x[/]",
            [new InitializedFile("[docs]/CLAUDE.md", InitializedFileAction.Created)]));

        var command = CreateCommand(console, initializer);

        // Act
        await command.ExecuteAsync(new InitCommandOptions());

        // Assert
        console.Lines.Take(2)
            .Should().Equal("Initialised SHADE kit in /repo/[red]x[/].", "created  [docs]/CLAUDE.md");
    }

    [Fact]
    [Trait("Requirement", "FP02-AC1")]
    public async Task ExecuteAsync_JsonMode_WritesEnvelopeWithRepositoryRootAndFileActions()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, CreateInitializer(Result), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new InitCommandOptions { Json = true });

        // Assert
        jsonOutput.ToString()
            .Should().Be(
                """
                {
                  "ok": true,
                  "data": {
                    "repositoryRoot": "/repo",
                    "files": [
                      {
                        "path": ".shadekit/config.json",
                        "action": "created"
                      },
                      {
                        "path": ".shadekit/features/",
                        "action": "created"
                      },
                      {
                        "path": ".gitignore",
                        "action": "appended"
                      },
                      {
                        "path": "CLAUDE.md",
                        "action": "updated"
                      }
                    ]
                  },
                  "warnings": [],
                  "error": null
                }

                """.ReplaceLineEndings("\n"));

        result.ExitCode
            .Should().Be(0);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModePathsWithMarkupCharacters_WritesPathsVerbatim()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var initializer = CreateInitializer(new RepositoryInitResult("/repo/[red]x[/]",
            [new InitializedFile("[docs]/CLAUDE.md", InitializedFileAction.Created)]));

        var command = CreateCommand(new TestConsole(), initializer, jsonOutput);

        // Act
        await command.ExecuteAsync(new InitCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var data = document.RootElement.GetProperty("data");

        data.GetProperty("repositoryRoot").GetString()
            .Should().Be("/repo/[red]x[/]");

        data.GetProperty("files")[0].GetProperty("path").GetString()
            .Should().Be("[docs]/CLAUDE.md");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithoutFiles_WritesHeaderAndCommitHintOnly()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateInitializer(new RepositoryInitResult("/repo", [])));

        // Act
        await command.ExecuteAsync(new InitCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                "Initialised SHADE kit in /repo.",
                "Commit '.shadekit/' and the changed files together with your code.");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithoutFiles_WritesEmptyFileList()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateInitializer(new RepositoryInitResult("/repo", [])),
            jsonOutput);

        // Act
        await command.ExecuteAsync(new InitCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("data").GetProperty("files").GetArrayLength()
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_InitializerThrowsMappedExceptionSynchronously_ThrowsAbortExceptionWithMappedExitCode()
    {
        // Arrange
        var initializer = A.Fake<IRepositoryInitializer>();
        A.CallTo(() => initializer.InitializeAsync(A<string>._, A<CancellationToken>._))
            .Throws(new KitAlreadyInitializedException("/repo/.shadekit"));

        var command = CreateCommand(new TestConsole(), initializer);

        // Act
        var act = () => command.ExecuteAsync(new InitCommandOptions());

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.KitAlreadyInitialized);
    }

    [Fact]
    public void FromResult_UndefinedFileAction_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var result = new RepositoryInitResult("/repo", [new InitializedFile("CLAUDE.md", (InitializedFileAction)99)]);

        // Act
        var act = () => InitCommandData.FromResult(result);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("action");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_UndefinedFileAction_ThrowsArgumentOutOfRangeExceptionWithoutOutput(bool json)
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var initializer = CreateInitializer(new RepositoryInitResult("/repo",
            [new InitializedFile("CLAUDE.md", (InitializedFileAction)99)]));

        var command = CreateCommand(console, initializer, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new InitCommandOptions { Json = json });

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();

        jsonOutput.ToString()
            .Should().BeEmpty();

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP02-AC7")]
    public async Task ExecuteAsync_NotAGitRepository_ThrowsAbortExceptionWithExitCode12AndEscapedPath()
    {
        // Arrange
        var notAGitRepositoryException = new NotAGitRepositoryException("/kit/[nested]/src");
        var console = new TestConsole();
        var command = CreateCommand(console, CreateThrowingInitializer(notAGitRepositoryException));

        // Act
        var act = () => command.ExecuteAsync(new InitCommandOptions());

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.NotAGitRepository);

        exception.PrintMessage
            .Should().BeTrue();

        exception.InnerException
            .Should().BeSameAs(notAGitRepositoryException);

        exception.Message
            .Should().Contain("'/kit/[[nested]]/src'");

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP02-AC7")]
    public async Task ExecuteAsync_JsonModeNotAGitRepository_WritesErrorEnvelopeAndAbortsWithoutPrintedMessage()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            CreateThrowingInitializer(new NotAGitRepositoryException("/kit/[nested]/src")), jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new InitCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.NotAGitRepository);

        exception.PrintMessage
            .Should().BeFalse();

        AssertErrorEnvelope(jsonOutput, ShadeKitCliErrorCodes.NotAGitRepository)
            .Should().Contain("'/kit/[nested]/src'");
    }

    [Fact]
    [Trait("Requirement", "FP02-AC6")]
    public async Task ExecuteAsync_KitAlreadyInitialized_ThrowsAbortExceptionWithExitCode13AndUpgradeHint()
    {
        // Arrange
        var alreadyInitializedException = new KitAlreadyInitializedException("/repo/[x]/.shadekit");
        var console = new TestConsole();
        var command = CreateCommand(console, CreateThrowingInitializer(alreadyInitializedException));

        // Act
        var act = () => command.ExecuteAsync(new InitCommandOptions());

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitAlreadyInitialized);

        exception.PrintMessage
            .Should().BeTrue();

        exception.InnerException
            .Should().BeSameAs(alreadyInitializedException);

        exception.Message
            .Should().Contain("'/repo/[[x]]/.shadekit'")
            .And.Contain("shade-kit upgrade");

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP02-AC6")]
    public async Task ExecuteAsync_JsonModeKitAlreadyInitialized_WritesErrorEnvelopeWithUpgradeHintAndAbortsSilently()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            CreateThrowingInitializer(new KitAlreadyInitializedException("/repo/.shadekit")), jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new InitCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitAlreadyInitialized);

        exception.PrintMessage
            .Should().BeFalse();

        AssertErrorEnvelope(jsonOutput, ShadeKitCliErrorCodes.KitAlreadyInitialized)
            .Should().Contain("shade-kit upgrade");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidManagedSection_ThrowsAbortExceptionWithExitCode23AndEscapedMessage()
    {
        // Arrange
        var invalidSectionException = new InvalidManagedSectionException("/repo/[x]/CLAUDE.md",
            "The begin marker in line 3 has no end marker.");

        var console = new TestConsole();
        var command = CreateCommand(console, CreateThrowingInitializer(invalidSectionException));

        // Act
        var act = () => command.ExecuteAsync(new InitCommandOptions());

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidManagedSection);

        exception.PrintMessage
            .Should().BeTrue();

        exception.InnerException
            .Should().BeSameAs(invalidSectionException);

        exception.Message
            .Should().Contain("'/repo/[[x]]/CLAUDE.md'")
            .And.Contain("The begin marker in line 3 has no end marker.");

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeInvalidManagedSection_WritesErrorEnvelopeAndAbortsSilently()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            CreateThrowingInitializer(new InvalidManagedSectionException("/repo/AGENTS.md", "Broken.")), jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new InitCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidManagedSection);

        exception.PrintMessage
            .Should().BeFalse();

        AssertErrorEnvelope(jsonOutput, ShadeKitCliErrorCodes.InvalidManagedSection)
            .Should().Contain("'/repo/AGENTS.md'");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_UnexpectedException_PropagatesExceptionUnwrappedWithoutOutput(bool json)
    {
        // Arrange
        var ioException = new IOException("disk error");
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, CreateThrowingInitializer(ioException), jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new InitCommandOptions { Json = json });

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .Which.Should().BeSameAs(ioException);

        jsonOutput.ToString()
            .Should().BeEmpty();

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public void ExecuteAsync_KitAlreadyInitialized_ReturnsFaultedTaskInsteadOfThrowingSynchronously()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(),
            CreateThrowingInitializer(new KitAlreadyInitializedException("/repo/.shadekit")));

        Task<CommandResult>? task = null;

        // Act
        var act = () => { task = command.ExecuteAsync(new InitCommandOptions()); };

        // Assert
        act.Should().NotThrow();

        task!.IsFaulted
            .Should().BeTrue();

        task.Exception!.InnerException
            .Should().BeOfType<CliCommandAbortException>();
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), CreateInitializer(Result));

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Fact]
    public void Ctor_NullConsole_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new InitCommand(null!, A.Fake<IRepositoryInitializer>(), A.Fake<ICliJsonWriter>(),
            CliErrorMapperTests.CreateMapper());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ansiConsole");
    }

    [Fact]
    public void Ctor_NullRepositoryInitializer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new InitCommand(new TestConsole(), null!, A.Fake<ICliJsonWriter>(),
            CliErrorMapperTests.CreateMapper());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repositoryInitializer");
    }

    [Fact]
    public void Ctor_NullJsonWriter_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new InitCommand(new TestConsole(), A.Fake<IRepositoryInitializer>(), null!,
            CliErrorMapperTests.CreateMapper());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jsonWriter");
    }

    [Fact]
    public void Ctor_NullErrorMapper_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new InitCommand(new TestConsole(), A.Fake<IRepositoryInitializer>(),
            A.Fake<ICliJsonWriter>(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("errorMapper");
    }

    [Fact]
    public void InitCommandOptions_Type_DerivesFromJsonCommandOptions()
    {
        // Assert
        typeof(InitCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    [Fact]
    public void InitCommand_CliCommandAttribute_IsRegisteredAsTopLevelInitCommand()
    {
        // Act
        var attribute = typeof(InitCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute!.Commands
            .Should().Equal("init");
    }

    [Fact]
    public void FromResult_NullResult_ThrowsArgumentNullException()
    {
        // Act
        var act = () => InitCommandData.FromResult(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("result");
    }

    internal static InitCommand CreateCommand(IAnsiConsole console, IRepositoryInitializer repositoryInitializer,
        TextWriter? jsonOutput = null)
    {
        return new InitCommand(console, repositoryInitializer, new CliJsonWriter(jsonOutput ?? TextWriter.Null),
            CliErrorMapperTests.CreateMapper());
    }

    internal static IRepositoryInitializer CreateInitializer(RepositoryInitResult result)
    {
        var initializer = A.Fake<IRepositoryInitializer>();
        A.CallTo(() => initializer.InitializeAsync(A<string>._, A<CancellationToken>._)).Returns(result);

        return initializer;
    }

    private static IRepositoryInitializer CreateThrowingInitializer(Exception exception)
    {
        var initializer = A.Fake<IRepositoryInitializer>();
        A.CallTo(() => initializer.InitializeAsync(A<string>._, A<CancellationToken>._)).ThrowsAsync(exception);

        return initializer;
    }

    /// <summary>
    /// Asserts that the output is exactly one failure envelope with the specified error code and returns its message.
    /// </summary>
    private static string AssertErrorEnvelope(StringWriter jsonOutput, string expectedCode)
    {
        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.EnumerateObject().Select(x => x.Name)
            .Should().Equal("ok", "data", "warnings", "error");

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("data").ValueKind
            .Should().Be(JsonValueKind.Null);

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(expectedCode);

        return root.GetProperty("error").GetProperty("message").GetString()!;
    }
}
