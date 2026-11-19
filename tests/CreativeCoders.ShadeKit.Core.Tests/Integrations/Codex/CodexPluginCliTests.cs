using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Codex;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Codex;

// The runner is faked, so these tests never start codex. The outputs follow the serialized types of
// codex-rs/cli/src/plugin_cmd.rs and codex-rs/cli/src/marketplace_cmd.rs at the tag rust-v0.169.0.
public class CodexPluginCliTests
{
    private const string WorkingDirectory = "/home/me";

    private const string PluginId = "shade-kit@creativecoders-shade-kit";

    private const string MarketplaceName = "creativecoders-shade-kit";

    private const string Repo = "CreativeCodersTeam/shade-kit";

    private const string NullValue = "<null>";

    private const string ChangeOutput = """{ "pluginId": "shade-kit@creativecoders-shade-kit" }""" + "\n";

    private const string PluginListOutput =
        """
        {
          "installed": [
            {
              "pluginId": "shade-kit@creativecoders-shade-kit",
              "name": "shade-kit",
              "marketplaceName": "creativecoders-shade-kit",
              "version": "local",
              "installed": true,
              "enabled": true,
              "source": { "source": "local", "path": "/home/me/.codex/.tmp/marketplaces/creativecoders-shade-kit" },
              "marketplaceSource": { "sourceType": "git", "source": "https://github.com/CreativeCodersTeam/shade-kit.git" },
              "installPolicy": "AVAILABLE",
              "authPolicy": "ON_USE"
            },
            {
              "pluginId": "tools@other",
              "name": "tools",
              "marketplaceName": "other",
              "version": null,
              "installed": true,
              "enabled": false,
              "source": { "source": "git", "url": "https://example.com/tools.git", "ref": "main" },
              "installPolicy": "AVAILABLE",
              "authPolicy": "ON_INSTALL"
            }
          ],
          "available": [
            { "pluginId": "x@y", "installed": false }
          ]
        }

        """;

    private const string MarketplaceListOutput =
        """
        {
          "marketplaces": [
            {
              "name": "openai-bundled",
              "root": "/home/me/.codex/.tmp/bundled-marketplaces/openai-bundled"
            },
            {
              "name": "creativecoders-shade-kit",
              "root": "/home/me/.codex/.tmp/marketplaces/creativecoders-shade-kit",
              "marketplaceSource": {
                "sourceType": "git",
                "source": "https://github.com/CreativeCodersTeam/shade-kit.git",
                "extra": 1
              }
            },
            {
              "name": "local-kit",
              "root": "/src/shade-kit",
              "marketplaceSource": { "sourceType": "local", "source": "/src/shade-kit" },
              "extra": [1, 2]
            }
          ]
        }
        """;

    private const string MarketplaceAddOutput =
        """
        {
          "marketplaceName": "creativecoders-shade-kit",
          "installedRoot": "/home/me/.codex/.tmp/marketplaces/creativecoders-shade-kit",
          "alreadyAdded": false
        }
        """;

    private static readonly MarketplaceSource ReleaseSource = MarketplaceSource.ForGitHub(Repo, "v0.8.0");

    private readonly ICodexCommandRunner _codexCommandRunner = A.Fake<ICodexCommandRunner>();

    private string? _actualWorkingDirectory;

    private IReadOnlyList<string>? _actualArguments;

    public static TheoryData<string, string[]> ChangeCommands => new()
    {
        { "upgrade", ["plugin", "marketplace", "upgrade", MarketplaceName, "--json"] },
        { "remove-marketplace", ["plugin", "marketplace", "remove", MarketplaceName, "--json"] },
        { "add-plugin", ["plugin", "add", PluginId, "--json"] },
        { "remove-plugin", ["plugin", "remove", PluginId, "--json"] }
    };

    public static TheoryData<string> ChangeCommandNames =>
        ["upgrade", "remove-marketplace", "add-plugin", "remove-plugin"];

    public static TheoryData<string> CommandNames =>
    [
        "list-plugins", "list-marketplaces", "add-marketplace", "upgrade", "remove-marketplace", "add-plugin",
        "remove-plugin"
    ];

    [Fact]
    public void Constructor_NullCodexCommandRunner_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CodexPluginCli(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("codexCommandRunner");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidWorkingDirectory_ThrowsArgumentException(string? workingDirectory)
    {
        // Act
        var act = () => new CodexPluginCli(_codexCommandRunner, workingDirectory!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(workingDirectory));
    }

    [Fact]
    public void Constructor_DefaultWorkingDirectory_UsesExistingHomeOrTemporaryDirectory()
    {
        // Act
        var sut = new CodexPluginCli(_codexCommandRunner);

        // Assert
        sut.WorkingDirectory
            .Should().BeOneOf(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Path.GetTempPath());

        Directory.Exists(sut.WorkingDirectory)
            .Should().BeTrue();
    }

    [Fact]
    public async Task ListPluginsAsync_CodexListsPlugins_ReturnsInstalledEntriesInOrderAndIgnoresUnreadFields()
    {
        // Arrange
        SetUpRun(0, PluginListOutput, "Warning: failed to list remote marketplace plugins: offline\n");
        var sut = CreateSut();

        // Act
        var plugins = await sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        plugins
            .Should().Equal(
                new CodexInstalledPlugin(PluginId, "shade-kit", MarketplaceName, "local", true, true),
                new CodexInstalledPlugin("tools@other", "tools", "other", null, true, false));

        AssertRun("plugin", "list", "--json");
    }

    [Fact]
    public async Task ListPluginsAsync_InstalledFalseEntry_ReturnsEntryWithInstalledFalse()
    {
        // Arrange
        SetUpRun(0, """
                    {"installed":[{"pluginId":"a@b","name":"a","marketplaceName":"b","installed":false,"enabled":true}]}
                    """);
        var sut = CreateSut();

        // Act
        var plugins = await sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        plugins
            .Should().Equal(new CodexInstalledPlugin("a@b", "a", "b", null, false, true));
    }

    [Fact]
    public async Task ListPluginsAsync_EmptyInstalledArrayWithoutAvailable_ReturnsEmptyList()
    {
        // Arrange
        SetUpRun(0, "{\"installed\":[]}\n");
        var sut = CreateSut();

        // Act
        var plugins = await sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        plugins
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("""[]""", "The output of codex is not a JSON object.")]
    [InlineData("""{"available":[]}""", "The codex output has no 'installed' array.")]
    [InlineData("""{"installed":{}}""", "The codex output has no 'installed' array.")]
    [InlineData("""{"installed":null}""", "The codex output has no 'installed' array.")]
    [InlineData("""{"installed":["x"]}""", "The entry 1 of 'installed' of the codex output is not a JSON object.")]
    [InlineData("""{"installed":[{"pluginId":"a@b","name":"a","marketplaceName":"b","installed":true,"enabled":true},{"pluginId":"c@d"}]}""",
        "The entry 2 of 'installed' of the codex output has no non-empty 'name' string.")]
    [InlineData("""{"installed":[{"name":"a","marketplaceName":"b","installed":true,"enabled":true}]}""",
        "The entry 1 of 'installed' of the codex output has no non-empty 'pluginId' string.")]
    [InlineData("""{"installed":[{"pluginId":"a@b","marketplaceName":"b","installed":true,"enabled":true}]}""",
        "The entry 1 of 'installed' of the codex output has no non-empty 'name' string.")]
    [InlineData("""{"installed":[{"pluginId":"a@b","name":" ","marketplaceName":"b","installed":true,"enabled":true}]}""",
        "The entry 1 of 'installed' of the codex output has no non-empty 'name' string.")]
    [InlineData("""{"installed":[{"pluginId":"a@b","name":"a","installed":true,"enabled":true}]}""",
        "The entry 1 of 'installed' of the codex output has no non-empty 'marketplaceName' string.")]
    [InlineData("""{"installed":[{"pluginId":7,"name":"a","marketplaceName":"b","installed":true,"enabled":true}]}""",
        "The field 'pluginId' of the entry 1 of 'installed' of the codex output is not a string.")]
    [InlineData("""{"installed":[{"pluginId":"a@b","name":"a","marketplaceName":"b","enabled":true}]}""",
        "The entry 1 of 'installed' of the codex output has no 'installed' Boolean.")]
    [InlineData("""{"installed":[{"pluginId":"a@b","name":"a","marketplaceName":"b","installed":true}]}""",
        "The entry 1 of 'installed' of the codex output has no 'enabled' Boolean.")]
    [InlineData("""{"installed":[{"pluginId":"a@b","name":"a","marketplaceName":"b","installed":true,"enabled":"true"}]}""",
        "The entry 1 of 'installed' of the codex output has no 'enabled' Boolean.")]
    [InlineData("""{"installed":[{"pluginId":"a@b","name":"a","marketplaceName":"b","installed":true,"enabled":true,"version":1}]}""",
        "The field 'version' of the entry 1 of 'installed' of the codex output is not a string.")]
    public async Task ListPluginsAsync_UnexpectedShape_ThrowsCodexCommandFailedExceptionNamingProblem(string output,
        string expectedReason)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.Reason
            .Should().Be(expectedReason);

        exception.ExitCode
            .Should().Be(0);

        exception.Arguments
            .Should().Equal("plugin", "list", "--json");
    }

    [Theory]
    [InlineData("")]
    [InlineData("No marketplace plugins found.")]
    [InlineData("Warning: something\n{\"installed\":[]}")]
    [InlineData("{\"installed\":[]}\n{\"installed\":[]}")]
    [InlineData("{\"installed\":[")]
    public async Task ListPluginsAsync_OutputIsNotOneJsonValue_ThrowsCodexCommandFailedExceptionWithJsonError(
        string output)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.Reason
            .Should().StartWith("The output of codex is not valid JSON: ")
            .And.EndWith(".");

        exception.InnerException
            .Should().BeAssignableTo<JsonException>();
    }

    [Fact]
    public async Task ListPluginsAsync_NonZeroExitCodeWithValidObject_ThrowsCodexCommandFailedExceptionWithLastErrorLine()
    {
        // Arrange
        SetUpRun(1, "{\"installed\":[]}",
            "Error: failed to load configured marketplace snapshot(s):\n- `creativecoders-shade-kit` at /x: broken\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("codex exited with the exit code 1: - `creativecoders-shade-kit` at /x: broken.");

        exception.ExitCode
            .Should().Be(1);

        exception.CodexMessage
            .Should().Be("- `creativecoders-shade-kit` at /x: broken");
    }

    [Fact]
    public async Task ListMarketplacesAsync_CodexListsMarketplaces_ReadsEntriesWithAndWithoutSourceInOrder()
    {
        // Arrange
        SetUpRun(0, MarketplaceListOutput);
        var sut = CreateSut();

        // Act
        var marketplaces = await sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        marketplaces
            .Should().Equal(
                new CodexMarketplace("openai-bundled", "/home/me/.codex/.tmp/bundled-marketplaces/openai-bundled",
                    null, null),
                new CodexMarketplace(MarketplaceName, "/home/me/.codex/.tmp/marketplaces/creativecoders-shade-kit",
                    "git", "https://github.com/CreativeCodersTeam/shade-kit.git"),
                new CodexMarketplace("local-kit", "/src/shade-kit", "local", "/src/shade-kit"));

        AssertRun("plugin", "marketplace", "list", "--json");
    }

    [Fact]
    public async Task ListMarketplacesAsync_NullMarketplaceSource_ReturnsEntryWithoutSource()
    {
        // Arrange
        SetUpRun(0, """{"marketplaces":[{"name":"x","root":"/r","marketplaceSource":null}]}""");
        var sut = CreateSut();

        // Act
        var marketplaces = await sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        marketplaces
            .Should().Equal(new CodexMarketplace("x", "/r", null, null));
    }

    [Theory]
    [InlineData("""{"marketplace":[]}""", "The codex output has no 'marketplaces' array.")]
    [InlineData("""{"marketplaces":[3]}""", "The entry 1 of 'marketplaces' of the codex output is not a JSON object.")]
    [InlineData("""{"marketplaces":[{"name":"x","root":"/r"},"y"]}""",
        "The entry 2 of 'marketplaces' of the codex output is not a JSON object.")]
    [InlineData("""{"marketplaces":[{"root":"/r"}]}""",
        "The entry 1 of 'marketplaces' of the codex output has no non-empty 'name' string.")]
    [InlineData("""{"marketplaces":[{"name":"x"}]}""",
        "The entry 1 of 'marketplaces' of the codex output has no non-empty 'root' string.")]
    [InlineData("""{"marketplaces":[{"name":"x","root":"/r","marketplaceSource":"git"}]}""",
        "The field 'marketplaceSource' of the entry 1 of 'marketplaces' of the codex output is not a JSON object.")]
    [InlineData("""{"marketplaces":[{"name":"x","root":"/r","marketplaceSource":{"source":"/s"}}]}""",
        "The 'marketplaceSource' of the entry 1 of 'marketplaces' of the codex output has no non-empty " +
        "'sourceType' string.")]
    [InlineData("""{"marketplaces":[{"name":"x","root":"/r","marketplaceSource":{"sourceType":"local"}}]}""",
        "The 'marketplaceSource' of the entry 1 of 'marketplaces' of the codex output has no non-empty " +
        "'source' string.")]
    [InlineData("""{"marketplaces":[{"name":"x","root":"/r","marketplaceSource":{"sourceType":1,"source":"/s"}}]}""",
        "The field 'sourceType' of the 'marketplaceSource' of the entry 1 of 'marketplaces' of the codex output " +
        "is not a string.")]
    public async Task ListMarketplacesAsync_UnexpectedShape_ThrowsCodexCommandFailedExceptionNamingProblem(
        string output, string expectedReason)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.Reason
            .Should().Be(expectedReason);

        exception.Arguments
            .Should().Equal("plugin", "marketplace", "list", "--json");
    }

    [Fact]
    public async Task ListMarketplacesAsync_StrayLineAfterObject_ThrowsCodexCommandFailedException()
    {
        // Arrange
        SetUpRun(0, "{\"marketplaces\":[]}\nUpdate available: 0.170.0");
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexCommandFailedException>())
            .Which.Reason.Should().StartWith("The output of codex is not valid JSON: ");
    }

    [Fact]
    [Trait("Requirement", "FP08-AC3")]
    public async Task AddMarketplaceAsync_GitHubSourceWithRef_RunsRepoWithSeparateRefOptionAndReadsResult()
    {
        // Arrange
        SetUpRun(0, MarketplaceAddOutput);
        var sut = CreateSut();

        // Act
        var result = await sut.AddMarketplaceAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new CodexMarketplaceAddResult(MarketplaceName,
                "/home/me/.codex/.tmp/marketplaces/creativecoders-shade-kit", false));

        AssertRun("plugin", "marketplace", "add", Repo, "--ref", "v0.8.0", "--json");
    }

    [Fact]
    [Trait("Requirement", "FP08-AC3")]
    public async Task AddMarketplaceAsync_GitHubSourceWithoutRef_RunsRepoWithoutRefOption()
    {
        // Arrange
        SetUpRun(0, MarketplaceAddOutput.Replace("false", "true", StringComparison.Ordinal));
        var sut = CreateSut();

        // Act
        var result = await sut.AddMarketplaceAsync(MarketplaceSource.ForGitHub(Repo),
            TestContext.Current.CancellationToken);

        // Assert
        result.AlreadyAdded
            .Should().BeTrue();

        AssertRun("plugin", "marketplace", "add", Repo, "--json");
    }

    [Fact]
    [Trait("Requirement", "FP08-AC3")]
    public async Task AddMarketplaceAsync_DirectorySource_RunsAbsolutePathWithoutRefOption()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "shade kit");
        SetUpRun(0, MarketplaceAddOutput);
        var sut = CreateSut();

        // Act
        await sut.AddMarketplaceAsync(MarketplaceSource.ForDirectory(directory),
            TestContext.Current.CancellationToken);

        // Assert
        AssertRun("plugin", "marketplace", "add", MarketplaceSource.ForDirectory(directory).Text, "--json");
    }

    [Fact]
    public async Task AddMarketplaceAsync_DirectoryWithHash_ThrowsCodexCommandFailedWithoutRunning()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "kit#2");
        var sut = CreateSut();

        // Act
        var act = () => sut.AddMarketplaceAsync(MarketplaceSource.ForDirectory(directory),
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.ExitCode
            .Should().BeNull();

        exception.Reason
            .Should().Contain("contains the character '#'");

        exception.Arguments
            .Should().Equal("plugin", "marketplace", "add", MarketplaceSource.ForDirectory(directory).Text, "--json");

        AssertNoRun();
    }

    [Fact]
    public async Task AddMarketplaceAsync_RefStartsWithDash_ThrowsArgumentExceptionWithoutRunning()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.AddMarketplaceAsync(MarketplaceSource.ForGitHub(Repo, "-x"),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>().WithParameterName("source"))
            .Which.Message.Should().Contain("codex would read it as an option");

        AssertNoRun();
    }

    [Fact]
    public async Task AddMarketplaceAsync_NullSource_ThrowsArgumentNullExceptionWithoutRunning()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.AddMarketplaceAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("source");

        AssertNoRun();
    }

    [Theory]
    [InlineData("""{"installedRoot":"/r","alreadyAdded":true}""",
        "The codex output has no non-empty 'marketplaceName' string.")]
    [InlineData("""{"marketplaceName":"m","alreadyAdded":true}""",
        "The codex output has no non-empty 'installedRoot' string.")]
    [InlineData("""{"marketplaceName":"m","installedRoot":"/r"}""", "The codex output has no 'alreadyAdded' Boolean.")]
    [InlineData("""{"marketplaceName":"m","installedRoot":"/r","alreadyAdded":"yes"}""",
        "The codex output has no 'alreadyAdded' Boolean.")]
    [InlineData("""{"marketplaceName":["m"],"installedRoot":"/r","alreadyAdded":true}""",
        "The field 'marketplaceName' of the codex output is not a string.")]
    [InlineData("""["m"]""", "The output of codex is not a JSON object.")]
    public async Task AddMarketplaceAsync_UnexpectedShape_ThrowsCodexCommandFailedExceptionNamingProblem(
        string output, string expectedReason)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.AddMarketplaceAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.Reason
            .Should().Be(expectedReason);

        exception.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task AddMarketplaceAsync_OtherSourceRefused_ThrowsCodexCommandFailedWithCodexMessage()
    {
        // Arrange
        SetUpRun(1, "", "Error: marketplace 'creativecoders-shade-kit' is already added from a different source; " +
                        "remove it before adding this source\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.AddMarketplaceAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.ExitCode
            .Should().Be(1);

        exception.CodexMessage
            .Should().Be("Error: marketplace 'creativecoders-shade-kit' is already added from a different source; " +
                         "remove it before adding this source");

        exception.Arguments
            .Should().Equal("plugin", "marketplace", "add", Repo, "--ref", "v0.8.0", "--json");
    }

    [Theory]
    [MemberData(nameof(ChangeCommands))]
    public async Task ChangeCommand_ExitCodeZeroWithJsonObject_RunsExactArgumentsWithJsonInWorkingDirectory(
        string commandName, string[] expectedArguments)
    {
        // Arrange
        SetUpRun(0, ChangeOutput, "Warning: something.\n");
        var sut = CreateSut();

        // Act
        await RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken);

        // Assert
        AssertRun(expectedArguments);
    }

    [Theory]
    [MemberData(nameof(ChangeCommandNames))]
    public async Task ChangeCommand_ExitCodeZeroWithoutJsonObject_ThrowsCodexCommandFailedException(
        string commandName)
    {
        // Arrange
        SetUpRun(0, "Added plugin `shade-kit` from marketplace `creativecoders-shade-kit`.\n");
        var sut = CreateSut();

        // Act
        var act = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.Reason
            .Should().StartWith("The output of codex is not valid JSON: ");

        exception.ExitCode
            .Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(ChangeCommandNames))]
    public async Task ChangeCommand_ExitCodeZeroWithJsonArray_ThrowsCodexCommandFailedException(string commandName)
    {
        // Arrange
        SetUpRun(0, "[]");
        var sut = CreateSut();

        // Act
        var act = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexCommandFailedException>())
            .Which.Reason.Should().Be("The output of codex is not a JSON object.");
    }

    [Theory]
    [MemberData(nameof(ChangeCommands))]
    public async Task ChangeCommand_NonZeroExitCodeWithStandardError_ThrowsCodexCommandFailedWithLastErrorLine(
        string commandName, string[] expectedArguments)
    {
        // Arrange
        SetUpRun(1, "",
            "Failed to upgrade marketplace `creativecoders-shade-kit`: fetch failed\n" +
            "Error: 1 upgrade failure(s) occurred.\n");
        var sut = CreateSut();

        // Act
        var act = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("codex exited with the exit code 1: Error: 1 upgrade failure(s) occurred.");

        exception.ExitCode
            .Should().Be(1);

        exception.CodexMessage
            .Should().Be("Error: 1 upgrade failure(s) occurred.");

        exception.Arguments
            .Should().Equal(expectedArguments);
    }

    [Theory]
    [MemberData(nameof(ChangeCommandNames))]
    public async Task ChangeCommand_NonZeroExitCodeWithOnlyStandardOutput_NamesLastStandardOutputLine(
        string commandName)
    {
        // Arrange
        SetUpRun(3, "Plugin is broken.\n", "  \n");
        var sut = CreateSut();

        // Act
        var act = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexCommandFailedException>())
            .Which.Reason.Should().Be("codex exited with the exit code 3: Plugin is broken.");
    }

    [Theory]
    [InlineData("Fetching 10%\rFetching 90%\rError: fetch failed\r\n", "Error: fetch failed")]
    [InlineData("Error: top\r\n\r\nCaused by:\r\n    network down\r\n\r\n", "network down")]
    [InlineData("Error: fetch failed\rFetching 90%", "Fetching 90%")]
    public async Task ChangeCommand_StandardErrorWithCarriageReturns_NamesLastLineWithoutCarriageReturn(
        string standardError, string expectedMessage)
    {
        // Arrange
        SetUpRun(1, "", standardError);
        var sut = CreateSut();

        // Act
        var act = () => sut.UpgradeMarketplaceAsync(MarketplaceName, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.CodexMessage
            .Should().Be(expectedMessage);

        exception.Reason
            .Should().Be($"codex exited with the exit code 1: {expectedMessage}.")
            .And.NotContain("\r");
    }

    [Fact]
    public async Task ChangeCommand_NonZeroExitCodeWithoutOutput_NamesOnlyExitCodeInInvariantCulture()
    {
        // Arrange
        SetUpRun(-1073741510, "", "");
        var sut = CreateSut();

        // Act
        var act = () => sut.AddPluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("codex exited with the exit code -1073741510.");

        exception.ExitCode
            .Should().Be(-1073741510);

        exception.CodexMessage
            .Should().BeNull();
    }

    [Theory]
    [InlineData("upgrade", "name")]
    [InlineData("remove-marketplace", "name")]
    [InlineData("add-plugin", "pluginId")]
    [InlineData("remove-plugin", "pluginId")]
    public async Task ChangeCommand_InvalidValue_ThrowsArgumentExceptionNamingCallerParameter(string commandName,
        string expectedParameterName)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var dashAct = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken, "--all");
        var emptyAct = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken, " ");
        var nullAct = () => RunChangeCommandAsync(sut, commandName, TestContext.Current.CancellationToken,
            NullValue);

        // Assert
        (await dashAct.Should().ThrowAsync<ArgumentException>().WithParameterName(expectedParameterName))
            .Which.Message.Should().Contain("codex would read it as an option");

        await emptyAct.Should().ThrowAsync<ArgumentException>()
            .WithParameterName(expectedParameterName);

        // As documented: a null value is an ArgumentException like an empty one, not an ArgumentNullException.
        await nullAct.Should().ThrowExactlyAsync<ArgumentException>()
            .WithParameterName(expectedParameterName);

        AssertNoRun();
    }

    [Fact]
    public async Task AddPluginAsync_RunnerThrowsCodexNotFound_PropagatesException()
    {
        // Arrange
        var notFound = new CodexNotFoundException(["plugin", "add", PluginId, "--json"], "Not found.");
        A.CallTo(() => _codexCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(notFound);
        var sut = CreateSut();

        // Act
        var act = () => sut.AddPluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexNotFoundException>())
            .Which.Should().BeSameAs(notFound);
    }

    [Fact]
    public async Task RemoveMarketplaceAsync_RunnerCanceled_PropagatesOperationCanceledException()
    {
        // Arrange
        A.CallTo(() => _codexCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
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
        SetUpRun(0, commandName switch
        {
            "list-plugins" => "{\"installed\":[]}",
            "list-marketplaces" => "{\"marketplaces\":[]}",
            "add-marketplace" => MarketplaceAddOutput,
            _ => ChangeOutput
        });
        var sut = CreateSut();
        var token = cancellationTokenSource.Token;

        // Act
        Task task = commandName switch
        {
            "list-plugins" => sut.ListPluginsAsync(token),
            "list-marketplaces" => sut.ListMarketplacesAsync(token),
            "add-marketplace" => sut.AddMarketplaceAsync(ReleaseSource, token),
            _ => RunChangeCommandAsync(sut, commandName, token)
        };
        await task;

        // Assert
        A.CallTo(() => _codexCommandRunner.RunAsync(WorkingDirectory, A<IReadOnlyList<string>>._, token))
            .MustHaveHappenedOnceExactly();

        _actualArguments
            .Should().EndWith("--json");
    }

    private static Task RunChangeCommandAsync(CodexPluginCli sut, string commandName,
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
            "upgrade" => sut.UpgradeMarketplaceAsync(Pick(MarketplaceName), cancellationToken),
            "remove-marketplace" => sut.RemoveMarketplaceAsync(Pick(MarketplaceName), cancellationToken),
            "add-plugin" => sut.AddPluginAsync(Pick(PluginId), cancellationToken),
            "remove-plugin" => sut.RemovePluginAsync(Pick(PluginId), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(commandName), commandName, null)
        };
    }

    private CodexPluginCli CreateSut()
    {
        return new CodexPluginCli(_codexCommandRunner, WorkingDirectory);
    }

    private void SetUpRun(int exitCode, string standardOutput, string standardError = "")
    {
        A.CallTo(() => _codexCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
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
        A.CallTo(_codexCommandRunner)
            .MustNotHaveHappened();
    }
}
