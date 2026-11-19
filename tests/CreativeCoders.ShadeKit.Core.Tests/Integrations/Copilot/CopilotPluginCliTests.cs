using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Copilot;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Copilot;

// The runner is faked, so these tests never start copilot. The plugin list follows the shape of the GitHub Docs plugin
// reference; the marketplace list shape is not documented, so the tests cover the shapes the reader accepts.
public class CopilotPluginCliTests
{
    private const string WorkingDirectory = "/home/me";

    private const string PluginId = "shade-kit@creativecoders-shade-kit";

    private const string MarketplaceName = "creativecoders-shade-kit";

    private const string GitHubSource = "CreativeCodersTeam/shade-kit#v0.8.0";

    private const string NullValue = "<null>";

    private const string PluginListOutput =
        """
        [
          {
            "name": "shade-kit",
            "marketplace": "creativecoders-shade-kit",
            "version": "0.8.0",
            "enabled": true,
            "source": "CreativeCodersTeam/shade-kit",
            "installedFrom": "marketplace"
          },
          {
            "name": "local-tools",
            "enabled": false,
            "source": { "kind": "path", "path": "/src/tools" },
            "installedFrom": { "kind": "direct" },
            "extra": [1, 2]
          }
        ]
        """;

    private const string MarketplaceListOutput =
        """
        [
          {
            "name": "copilot-plugins",
            "source": { "source": "github", "repo": "github/copilot-plugins", "ref": "main", "extra": 1 },
            "lastUpdated": "2026-11-13T20:01:57.417Z"
          },
          {
            "name": "creativecoders-shade-kit",
            "source": "github",
            "repo": "CreativeCodersTeam/shade-kit",
            "ref": "v0.8.0"
          },
          {
            "name": "local-kit",
            "source": { "type": "directory", "path": "/src/shade-kit" }
          },
          {
            "name": "bare"
          }
        ]
        """;

    private readonly ICopilotCommandRunner _copilotCommandRunner = A.Fake<ICopilotCommandRunner>();

    private string? _actualWorkingDirectory;

    private IReadOnlyList<string>? _actualArguments;

    public static TheoryData<string, string[]> ChangeCommands => new()
    {
        { "add", ["plugin", "marketplace", "add", GitHubSource] },
        { "remove", ["plugin", "marketplace", "remove", MarketplaceName] },
        { "update-marketplace", ["plugin", "marketplace", "update", MarketplaceName] },
        { "install", ["plugin", "install", PluginId] },
        { "update", ["plugin", "update", PluginId] },
        { "uninstall", ["plugin", "uninstall", PluginId] }
    };

    public static TheoryData<string> ChangeCommandNames =>
        ["add", "remove", "update-marketplace", "install", "update", "uninstall"];

    public static TheoryData<string> CommandNames =>
    [
        "list-plugins", "list-marketplaces", "add", "remove", "update-marketplace", "install", "update",
        "uninstall"
    ];

    [Fact]
    public void Constructor_NullCopilotCommandRunner_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CopilotPluginCli(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("copilotCommandRunner");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidWorkingDirectory_ThrowsArgumentException(string? workingDirectory)
    {
        // Act
        var act = () => new CopilotPluginCli(_copilotCommandRunner, workingDirectory!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(workingDirectory));
    }

    [Fact]
    public void Constructor_DefaultWorkingDirectory_UsesExistingHomeOrTemporaryDirectory()
    {
        // Act
        var sut = new CopilotPluginCli(_copilotCommandRunner);

        // Assert
        sut.WorkingDirectory
            .Should().BeOneOf(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Path.GetTempPath());

        Directory.Exists(sut.WorkingDirectory)
            .Should().BeTrue();
    }

    [Fact]
    public async Task ListPluginsAsync_CopilotListsPlugins_ReturnsPluginsInOrderAndIgnoresUnreadFields()
    {
        // Arrange
        SetUpRun(0, PluginListOutput);
        var sut = CreateSut();

        // Act
        var plugins = await sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        plugins
            .Should().Equal(
                new CopilotInstalledPlugin("shade-kit", MarketplaceName, "0.8.0", true),
                new CopilotInstalledPlugin("local-tools", null, null, false));

        AssertRun("plugin", "list", "--json");
    }

    [Fact]
    public async Task ListPluginsAsync_EmptyArrayWithTrailingLineBreak_ReturnsEmptyList()
    {
        // Arrange
        SetUpRun(0, "[]\n");
        var sut = CreateSut();

        // Act
        var plugins = await sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        plugins
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ListPluginsAsync_NullOptionalFields_ReturnsNullValues()
    {
        // Arrange
        SetUpRun(0, """[{"name":"x","marketplace":null,"version":null,"enabled":false}]""");
        var sut = CreateSut();

        // Act
        var plugins = await sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        plugins
            .Should().Equal(new CopilotInstalledPlugin("x", null, null, false));
    }

    [Theory]
    [InlineData("""{"name":"x","enabled":true}""", "The output of copilot is not a JSON array.")]
    [InlineData("""[{"name":"x","enabled":true}, "y"]""", "The entry 2 of the copilot output is not a JSON object.")]
    [InlineData("""[{"enabled":true}]""", "The entry 1 of the copilot output has no non-empty 'name' string.")]
    [InlineData("""[{"name":"","enabled":true}]""",
        "The entry 1 of the copilot output has no non-empty 'name' string.")]
    [InlineData("""[{"name":7,"enabled":true}]""",
        "The field 'name' of the entry 1 of the copilot output is not a string.")]
    [InlineData("""[{"name":"x"}]""", "The entry 1 of the copilot output has no 'enabled' Boolean.")]
    [InlineData("""[{"name":"x","enabled":"true"}]""", "The entry 1 of the copilot output has no 'enabled' Boolean.")]
    [InlineData("""[{"name":"x","enabled":null}]""", "The entry 1 of the copilot output has no 'enabled' Boolean.")]
    [InlineData("""[{"name":"x","enabled":true,"marketplace":{"name":"m"}}]""",
        "The field 'marketplace' of the entry 1 of the copilot output is not a string.")]
    [InlineData("""[{"name":"x","enabled":true,"version":1}]""",
        "The field 'version' of the entry 1 of the copilot output is not a string.")]
    public async Task ListPluginsAsync_UnexpectedShape_ThrowsCopilotCommandFailedExceptionNamingProblem(
        string output, string expectedReason)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotCommandFailedException>()).Which;

        exception.Reason
            .Should().Be(expectedReason);

        exception.ExitCode
            .Should().Be(0);

        exception.Arguments
            .Should().Equal("plugin", "list", "--json");
    }

    [Theory]
    [InlineData("")]
    [InlineData("No plugins installed.")]
    [InlineData("Warning: direct plugin installs are deprecated.\n[]")]
    [InlineData("[]\nUpdate available: 1.0.113")]
    [InlineData("[{\"name\":")]
    public async Task ListPluginsAsync_OutputIsNotOneJsonValue_ThrowsCopilotCommandFailedExceptionWithJsonError(
        string output)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotCommandFailedException>()).Which;

        exception.Reason
            .Should().StartWith("The output of copilot is not valid JSON: ")
            .And.EndWith(".");

        exception.InnerException
            .Should().BeAssignableTo<JsonException>();
    }

    [Fact]
    public async Task ListPluginsAsync_StrayLineOnStandardError_ReadsStandardOutput()
    {
        // Arrange
        SetUpRun(0, """[{"name":"x","enabled":true}]""", "Warning: an update is available.\n");
        var sut = CreateSut();

        // Act
        var plugins = await sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        plugins
            .Should().Equal(new CopilotInstalledPlugin("x", null, null, true));
    }

    [Fact]
    public async Task ListPluginsAsync_NonZeroExitCodeWithValidArray_ThrowsCopilotCommandFailedExceptionWithStandardError()
    {
        // Arrange
        SetUpRun(1, "[]", "Error: not signed in.\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("copilot exited with the exit code 1: Error: not signed in.");

        exception.ExitCode
            .Should().Be(1);

        exception.CopilotMessage
            .Should().Be("Error: not signed in.");
    }

    [Fact]
    public async Task ListMarketplacesAsync_CopilotListsMarketplaces_ReadsObjectAndStringSourcesInOrder()
    {
        // Arrange
        SetUpRun(0, MarketplaceListOutput);
        var sut = CreateSut();

        // Act
        var marketplaces = await sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        marketplaces
            .Should().Equal(
                new CopilotMarketplace("copilot-plugins", "github", "github/copilot-plugins", "main", null),
                new CopilotMarketplace(MarketplaceName, "github", "CreativeCodersTeam/shade-kit", "v0.8.0", null),
                new CopilotMarketplace("local-kit", "directory", null, null, "/src/shade-kit"),
                new CopilotMarketplace("bare", null, null, null, null));

        AssertRun("plugin", "marketplace", "list", "--json");
    }

    [Fact]
    public async Task ListMarketplacesAsync_SourceObjectWithSourceAndType_PrefersSource()
    {
        // Arrange
        SetUpRun(0, """[{"name":"x","source":{"source":"github","type":"git","repo":"o/r"}}]""");
        var sut = CreateSut();

        // Act
        var marketplaces = await sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        marketplaces
            .Should().Equal(new CopilotMarketplace("x", "github", "o/r", null, null));
    }

    [Fact]
    public async Task ListMarketplacesAsync_SourceObject_IgnoresFieldsNextToIt()
    {
        // Arrange
        SetUpRun(0, """[{"name":"x","source":{"source":"directory","path":"/a"},"repo":"o/r","path":"/b"}]""");
        var sut = CreateSut();

        // Act
        var marketplaces = await sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        marketplaces
            .Should().Equal(new CopilotMarketplace("x", "directory", null, null, "/a"));
    }

    [Fact]
    public async Task ListMarketplacesAsync_NullOptionalFields_ReturnsNullValues()
    {
        // Arrange
        SetUpRun(0, """[{"name":"x","source":null,"repo":null,"ref":null,"path":null}]""");
        var sut = CreateSut();

        // Act
        var marketplaces = await sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        marketplaces
            .Should().Equal(new CopilotMarketplace("x", null, null, null, null));
    }

    [Theory]
    [InlineData("""{"name":"x"}""", "The output of copilot is not a JSON array.")]
    [InlineData("""[{"name":"x"}, 3]""", "The entry 2 of the copilot output is not a JSON object.")]
    [InlineData("""[{"source":"github"}]""", "The entry 1 of the copilot output has no non-empty 'name' string.")]
    [InlineData("""[{"name":"  "}]""", "The entry 1 of the copilot output has no non-empty 'name' string.")]
    [InlineData("""[{"name":"x","source":1}]""",
        "The field 'source' of the entry 1 of the copilot output is neither a string nor a JSON object.")]
    [InlineData("""[{"name":"x","source":["github"]}]""",
        "The field 'source' of the entry 1 of the copilot output is neither a string nor a JSON object.")]
    [InlineData("""[{"name":"x","ref":["main"]}]""",
        "The field 'ref' of the entry 1 of the copilot output is not a string.")]
    [InlineData("""[{"name":"x","source":{"source":true}}]""",
        "The field 'source' of the source of the entry 1 of the copilot output is not a string.")]
    [InlineData("""[{"name":"x","source":{"type":3}}]""",
        "The field 'type' of the source of the entry 1 of the copilot output is not a string.")]
    [InlineData("""[{"name":"x","source":{"source":"github","repo":42}}]""",
        "The field 'repo' of the source of the entry 1 of the copilot output is not a string.")]
    [InlineData("""[{"name":"x","source":{"source":"directory","path":{"abs":"/src"}}}]""",
        "The field 'path' of the source of the entry 1 of the copilot output is not a string.")]
    public async Task ListMarketplacesAsync_UnexpectedShape_ThrowsCopilotCommandFailedExceptionNamingProblem(
        string output, string expectedReason)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotCommandFailedException>()).Which;

        exception.Reason
            .Should().Be(expectedReason);

        exception.Arguments
            .Should().Equal("plugin", "marketplace", "list", "--json");
    }

    [Fact]
    public async Task ListMarketplacesAsync_StrayLineBeforeArray_ThrowsCopilotCommandFailedException()
    {
        // Arrange
        SetUpRun(0, "Refreshing marketplaces...\n[]");
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CopilotCommandFailedException>())
            .Which.Reason.Should().StartWith("The output of copilot is not valid JSON: ");
    }

    [Fact]
    public async Task ListMarketplacesAsync_NonZeroExitCodeWithoutStandardError_NamesLastStandardOutputLine()
    {
        // Arrange
        SetUpRun(2, "Loading\nError: settings are broken\n\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("copilot exited with the exit code 2: Error: settings are broken.");

        exception.CopilotMessage
            .Should().Be("Error: settings are broken");
    }

    [Theory]
    [MemberData(nameof(ChangeCommands))]
    public async Task ChangeCommand_ExitCodeZero_RunsExactArgumentsWithoutJsonInWorkingDirectory(
        string commandName, string[] expectedArguments)
    {
        // Arrange
        SetUpRun(0, "Done.\n", "Warning: something.\n");
        var sut = CreateSut();

        // Act
        await RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken);

        // Assert
        AssertRun(expectedArguments);
    }

    [Theory]
    [MemberData(nameof(ChangeCommands))]
    public async Task ChangeCommand_NonZeroExitCodeWithStandardError_ThrowsCopilotCommandFailedWithLastErrorLine(
        string commandName, string[] expectedArguments)
    {
        // Arrange
        SetUpRun(1, "Working...\n", "Something went wrong\nError: Marketplace \"creativecoders-shade-kit\" not found\n");
        var sut = CreateSut();

        // Act
        var act = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("copilot exited with the exit code 1: Error: Marketplace \"creativecoders-shade-kit\" " +
                         "not found.");

        exception.ExitCode
            .Should().Be(1);

        exception.CopilotMessage
            .Should().Be("Error: Marketplace \"creativecoders-shade-kit\" not found");

        exception.Arguments
            .Should().Equal(expectedArguments);
    }

    [Theory]
    [MemberData(nameof(ChangeCommandNames))]
    public async Task ChangeCommand_NonZeroExitCodeWithOnlyStandardOutput_NamesLastStandardOutputLine(
        string commandName)
    {
        // Arrange
        SetUpRun(3, "Plugin is already installed.\n", "  \n");
        var sut = CreateSut();

        // Act
        var act = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CopilotCommandFailedException>())
            .Which.Reason.Should().Be("copilot exited with the exit code 3: Plugin is already installed.");
    }

    [Theory]
    [InlineData("Fetching 10%\rFetching 90%\rError: fetch failed\r\n", "Error: fetch failed")]
    [InlineData("Working...\r\nError: fetch failed\r\n\r\n", "Error: fetch failed")]
    [InlineData("Error: fetch failed\rFetching 90%", "Fetching 90%")]
    public async Task ChangeCommand_StandardErrorWithCarriageReturns_NamesLastLineWithoutCarriageReturn(
        string standardError, string expectedMessage)
    {
        // Arrange
        SetUpRun(1, "", standardError);
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateMarketplaceAsync(MarketplaceName, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotCommandFailedException>()).Which;

        exception.CopilotMessage
            .Should().Be(expectedMessage);

        exception.Reason
            .Should().Be($"copilot exited with the exit code 1: {expectedMessage}.")
            .And.NotContain("\r");
    }

    [Fact]
    public async Task ChangeCommand_NegativeExitCode_NamesExitCodeInInvariantCulture()
    {
        // Arrange
        SetUpRun(-1073741510, "", "");
        var sut = CreateSut();

        // Act
        var act = () => sut.InstallPluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("copilot exited with the exit code -1073741510.");

        exception.ExitCode
            .Should().Be(-1073741510);
    }

    [Theory]
    [InlineData("add", "source")]
    [InlineData("remove", "name")]
    [InlineData("update-marketplace", "name")]
    [InlineData("install", "pluginId")]
    [InlineData("update", "pluginId")]
    [InlineData("uninstall", "pluginId")]
    public async Task ChangeCommand_InvalidValue_ThrowsArgumentExceptionNamingCallerParameter(string commandName,
        string expectedParameterName)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var dashAct = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken, "-x");
        var nullAct = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken,
            NullValue);

        // Assert
        await dashAct.Should().ThrowAsync<ArgumentException>()
            .WithParameterName(expectedParameterName);

        // As documented: a null value is an ArgumentException like an empty one, not an ArgumentNullException.
        await nullAct.Should().ThrowExactlyAsync<ArgumentException>()
            .WithParameterName(expectedParameterName);

        AssertNoRun();
    }

    [Fact]
    public async Task ListMarketplacesAsync_SourceObjectWithNullSourceOrNoKind_FallsBackToTypeOrNull()
    {
        // Arrange
        SetUpRun(0, """[{"name":"a","source":{"source":null,"type":"github","repo":"o/r"}},{"name":"b","source":{"repo":"o/r"}}]""");
        var sut = CreateSut();

        // Act
        var marketplaces = await sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        marketplaces
            .Should().Equal(
                new CopilotMarketplace("a", "github", "o/r", null, null),
                new CopilotMarketplace("b", null, "o/r", null, null));
    }

    [Fact]
    public async Task ChangeCommand_NonZeroExitCodeWithoutOutput_NamesOnlyExitCode()
    {
        // Arrange
        SetUpRun(127, "", "");
        var sut = CreateSut();

        // Act
        var act = () => sut.InstallPluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("copilot exited with the exit code 127.");

        exception.CopilotMessage
            .Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(ChangeCommandNames))]
    public async Task ChangeCommand_ValueStartsWithDash_ThrowsArgumentExceptionWithoutRunning(string commandName)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken, "--force");

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .Which.Message.Should().Contain("copilot would read it as an option");

        AssertNoRun();
    }

    [Theory]
    [MemberData(nameof(ChangeCommandNames))]
    public async Task ChangeCommand_EmptyValue_ThrowsArgumentExceptionWithoutRunning(string commandName)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken, " ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        AssertNoRun();
    }

    [Fact]
    public async Task AddMarketplaceAsync_DirectorySource_PassesAbsolutePathUnchanged()
    {
        // Arrange
        SetUpRun(0, "");
        var sut = CreateSut();

        // Act
        await sut.AddMarketplaceAsync("/src/shade kit", TestContext.Current.CancellationToken);

        // Assert
        AssertRun("plugin", "marketplace", "add", "/src/shade kit");
    }

    [Fact]
    public async Task AddMarketplaceAsync_RunnerThrowsCopilotNotFound_PropagatesException()
    {
        // Arrange
        var notFound = new CopilotNotFoundException(["plugin", "marketplace", "add", GitHubSource], "Not found.");
        A.CallTo(() => _copilotCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
                A<CancellationToken>._))
            .ThrowsAsync(notFound);
        var sut = CreateSut();

        // Act
        var act = () => sut.AddMarketplaceAsync(GitHubSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CopilotNotFoundException>())
            .Which.Should().BeSameAs(notFound);
    }

    [Fact]
    public async Task RemoveMarketplaceAsync_RunnerCanceled_PropagatesOperationCanceledException()
    {
        // Arrange
        A.CallTo(() => _copilotCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
                A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());
        var sut = CreateSut();

        // Act
        var act = () => sut.RemoveMarketplaceAsync(MarketplaceName, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowExactlyAsync<OperationCanceledException>();
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task EveryCommand_Token_PassesTokenAndWorkingDirectoryToRunner(string commandName)
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        SetUpRun(0, commandName.StartsWith("list", StringComparison.Ordinal) ? "[]" : "");
        var sut = CreateSut();
        var token = cancellationTokenSource.Token;

        // Act
        Task task = commandName switch
        {
            "list-plugins" => sut.ListPluginsAsync(token),
            "list-marketplaces" => sut.ListMarketplacesAsync(token),
            _ => RunChangeCommandAsync(sut, commandName, token)
        };
        await task;

        // Assert
        A.CallTo(() => _copilotCommandRunner.RunAsync(WorkingDirectory, A<IReadOnlyList<string>>._, token))
            .MustHaveHappenedOnceExactly();
    }

    private static Task RunChangeCommandAsync(CopilotPluginCli sut, string commandName,
        CancellationToken cancellationToken, string? value = null)
    {
        // NullValue passes null; no value passes the valid default of the command.
        string Pick(string defaultValue) => value switch
        {
            null => defaultValue,
            NullValue => null!,
            _ => value
        };

        return commandName switch
        {
            "add" => sut.AddMarketplaceAsync(Pick(GitHubSource), cancellationToken),
            "remove" => sut.RemoveMarketplaceAsync(Pick(MarketplaceName), cancellationToken),
            "update-marketplace" => sut.UpdateMarketplaceAsync(Pick(MarketplaceName), cancellationToken),
            "install" => sut.InstallPluginAsync(Pick(PluginId), cancellationToken),
            "update" => sut.UpdatePluginAsync(Pick(PluginId), cancellationToken),
            "uninstall" => sut.UninstallPluginAsync(Pick(PluginId), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(commandName), commandName, null)
        };
    }

    private CopilotPluginCli CreateSut()
    {
        return new CopilotPluginCli(_copilotCommandRunner, WorkingDirectory);
    }

    private void SetUpRun(int exitCode, string standardOutput, string standardError = "")
    {
        A.CallTo(() => _copilotCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
                A<CancellationToken>._))
            .Invokes((string workingDirectory, IReadOnlyList<string> arguments, CancellationToken _) =>
            {
                _actualWorkingDirectory = workingDirectory;
                _actualArguments = arguments;
            })
            .Returns(new ExternalProcessResult(exitCode, standardOutput, standardError));
    }

    private void AssertRun(params string[] expectedArguments)
    {
        _actualWorkingDirectory
            .Should().Be(WorkingDirectory);

        _actualArguments
            .Should().Equal(expectedArguments);
    }

    private void AssertNoRun()
    {
        A.CallTo(_copilotCommandRunner)
            .MustNotHaveHappened();
    }
}
