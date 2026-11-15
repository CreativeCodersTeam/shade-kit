using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Claude;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Claude;

// The runner is faked with the output of Claude Code 2.1.323, so these tests never start claude.
public class ClaudePluginCliTests
{
    private const string WorkingDirectory = "/home/me";

    private const string PluginId = "shade-kit@creativecoders-shade-kit";

    private const string GitHubSource = "CreativeCodersTeam/shade-kit#v0.8.0";

    private const string MarketplaceListOutput =
        """
        [
          {
            "name": "creativecoders-ai-store",
            "source": "github",
            "repo": "CreativeCodersTeam/ai-store",
            "ref": "main",
            "installLocation": "/home/me/.claude/plugins/marketplaces/creativecoders-ai-store"
          },
          {
            "name": "anthropic-agent-skills",
            "source": "github",
            "repo": "anthropics/skills",
            "installLocation": "/home/me/.claude/plugins/marketplaces/anthropic-agent-skills"
          },
          {
            "name": "creativecoders-shade-kit",
            "source": "directory",
            "path": "/src/shade-kit",
            "installLocation": "/src/shade-kit"
          }
        ]
        """;

    private const string PluginListOutput =
        """
        [
          {
            "id": "shade-kit@creativecoders-shade-kit",
            "version": "0.8.0",
            "scope": "user",
            "enabled": true,
            "installPath": "/home/me/.claude/plugins/cache/creativecoders-shade-kit/shade-kit/0.8.0",
            "installedAt": "2026-11-13T20:01:57.417Z",
            "lastUpdated": "2026-11-13T20:01:57.417Z",
            "projectEnabled": false
          },
          {
            "id": "cc-ai-dotnet@creativecoders-ai-store",
            "version": "4972bfa14908",
            "scope": "project",
            "enabled": false
          }
        ]
        """;

    private const string UpdateNotFoundOutput =
        """{"command":"update","outcome":"failed","plugin":"nope@creativecoders-shade-kit","pluginId":"nope@creativecoders-shade-kit","scope":"user","message":"Plugin \"nope\" not found","failureCode":"not_found"}""";

    private readonly IClaudeCommandRunner _claudeCommandRunner = A.Fake<IClaudeCommandRunner>();

    private string? _actualWorkingDirectory;

    private IReadOnlyList<string>? _actualArguments;

    [Fact]
    public void Constructor_NullClaudeCommandRunner_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ClaudePluginCli(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("claudeCommandRunner");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidWorkingDirectory_ThrowsArgumentException(string? workingDirectory)
    {
        // Act
        var act = () => new ClaudePluginCli(_claudeCommandRunner, workingDirectory!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(workingDirectory));
    }

    [Fact]
    public void Constructor_DefaultWorkingDirectory_UsesExistingHomeOrTemporaryDirectory()
    {
        // Act
        var sut = new ClaudePluginCli(_claudeCommandRunner);

        // Assert
        sut.WorkingDirectory
            .Should().BeOneOf(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Path.GetTempPath());

        Directory.Exists(sut.WorkingDirectory)
            .Should().BeTrue();
    }

    [Fact]
    public async Task ListMarketplacesAsync_ClaudeListsMarketplaces_ReturnsMarketplacesInOrderAndIgnoresUnknownFields()
    {
        // Arrange
        SetUpRun(0, MarketplaceListOutput);
        var sut = CreateSut();

        // Act
        var marketplaces = await sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        marketplaces
            .Should().Equal(
                new ClaudeMarketplace("creativecoders-ai-store", ClaudeMarketplace.GitHubSourceKind,
                    "CreativeCodersTeam/ai-store", "main", null),
                new ClaudeMarketplace("anthropic-agent-skills", ClaudeMarketplace.GitHubSourceKind,
                    "anthropics/skills", null, null),
                new ClaudeMarketplace("creativecoders-shade-kit", ClaudeMarketplace.DirectorySourceKind, null, null,
                    "/src/shade-kit"));

        AssertRun("plugin", "marketplace", "list", "--json");
    }

    [Fact]
    public async Task ListMarketplacesAsync_EmptyArray_ReturnsEmptyList()
    {
        // Arrange
        SetUpRun(0, "[]\n");
        var sut = CreateSut();

        // Act
        var marketplaces = await sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        marketplaces
            .Should().BeEmpty();
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
            .Should().Equal(new ClaudeMarketplace("x", null, null, null, null));
    }

    [Theory]
    [InlineData("""{"name":"x"}""", "The output of claude is not a JSON array.")]
    [InlineData("""[{"name":"x"}, 3]""", "The entry 2 of the claude output is not a JSON object.")]
    [InlineData("""[{"source":"github"}]""", "The entry 1 of the claude output has no non-empty 'name' string.")]
    [InlineData("""[{"name":null}]""", "The entry 1 of the claude output has no non-empty 'name' string.")]
    [InlineData("""[{"name":""}]""", "The entry 1 of the claude output has no non-empty 'name' string.")]
    [InlineData("""[{"name":"  "}]""", "The entry 1 of the claude output has no non-empty 'name' string.")]
    [InlineData("""[{"name":42}]""", "The field 'name' of the entry 1 of the claude output is not a string.")]
    [InlineData("""[{"name":"x","source":1}]""",
        "The field 'source' of the entry 1 of the claude output is not a string.")]
    [InlineData("""[{"name":"x","ref":["main"]}]""",
        "The field 'ref' of the entry 1 of the claude output is not a string.")]
    [InlineData("""[{"name":"x","repo":42}]""",
        "The field 'repo' of the entry 1 of the claude output is not a string.")]
    [InlineData("""[{"name":"x","path":{"abs":"/src"}}]""",
        "The field 'path' of the entry 1 of the claude output is not a string.")]
    public async Task ListMarketplacesAsync_UnexpectedShape_ThrowsClaudeCommandFailedExceptionNamingProblem(
        string output, string expectedReason)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().Be(expectedReason);

        exception.ExitCode
            .Should().Be(0);

        exception.Arguments
            .Should().Equal("plugin", "marketplace", "list", "--json");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Usage: claude plugin marketplace list")]
    [InlineData("[{\"name\":")]
    public async Task ListMarketplacesAsync_OutputIsNotJson_ThrowsClaudeCommandFailedExceptionWithJsonError(
        string output)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().StartWith("The output of claude is not valid JSON: ")
            .And.EndWith(".");

        exception.InnerException
            .Should().BeAssignableTo<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task ListMarketplacesAsync_NonZeroExitCodeWithFailureLine_ThrowsClaudeCommandFailedExceptionWithDetails()
    {
        // Arrange
        SetUpRun(1, """{"command":"marketplace-list","outcome":"failed","message":"Broken settings.","failureCode":"invalid_settings"}""");
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("claude exited with the exit code 1 and reported the outcome 'failed': Broken settings " +
                         "(failure code 'invalid_settings').");

        exception.ExitCode
            .Should().Be(1);

        exception.ClaudeMessage
            .Should().Be("Broken settings.");

        exception.FailureCode
            .Should().Be("invalid_settings");
    }

    [Fact]
    public async Task ListMarketplacesAsync_NonZeroExitCodeWithValidArray_ThrowsClaudeCommandFailedException()
    {
        // Arrange
        SetUpRun(2, MarketplaceListOutput);
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("claude exited with the exit code 2 and printed no JSON object on standard output.");

        exception.ExitCode
            .Should().Be(2);
    }

    [Fact]
    public async Task ListPluginsAsync_ClaudeListsPlugins_ReturnsPluginsInOrderAndIgnoresUnknownFields()
    {
        // Arrange
        SetUpRun(0, PluginListOutput);
        var sut = CreateSut();

        // Act
        var plugins = await sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        plugins
            .Should().Equal(
                new ClaudeInstalledPlugin(PluginId, "0.8.0", ClaudeInstalledPlugin.UserScope, true),
                new ClaudeInstalledPlugin("cc-ai-dotnet@creativecoders-ai-store", "4972bfa14908", "project", false));

        AssertRun("plugin", "list", "--json");
    }

    [Fact]
    public async Task ListPluginsAsync_MissingVersionAndScope_ReturnsNullValues()
    {
        // Arrange
        SetUpRun(0, """[{"id":"x@y","enabled":false}]""");
        var sut = CreateSut();

        // Act
        var plugins = await sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        plugins
            .Should().Equal(new ClaudeInstalledPlugin("x@y", null, null, false));
    }

    [Theory]
    [InlineData("""[{"version":"1.0.0","enabled":true}]""", "The entry 1 of the claude output has no non-empty 'id' string.")]
    [InlineData("""[{"id":"","enabled":true}]""", "The entry 1 of the claude output has no non-empty 'id' string.")]
    [InlineData("""[{"id":"x@y"}]""", "The entry 1 of the claude output has no 'enabled' Boolean.")]
    [InlineData("""[{"id":"x@y","enabled":null}]""", "The entry 1 of the claude output has no 'enabled' Boolean.")]
    [InlineData("""[{"id":"x@y","enabled":true,"version":1.2}]""",
        "The field 'version' of the entry 1 of the claude output is not a string.")]
    [InlineData("""[{"id":"x@y","enabled":"true"}]""", "The entry 1 of the claude output has no 'enabled' Boolean.")]
    [InlineData("""[{"id":"x@y","enabled":true,"scope":1}]""",
        "The field 'scope' of the entry 1 of the claude output is not a string.")]
    [InlineData("""["x@y"]""", "The entry 1 of the claude output is not a JSON object.")]
    public async Task ListPluginsAsync_UnexpectedShape_ThrowsClaudeCommandFailedExceptionNamingProblem(string output,
        string expectedReason)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().Be(expectedReason);
    }

    [Fact]
    public async Task AddMarketplaceAsync_ClaudeReportsOk_RunsAddWithUserScope()
    {
        // Arrange
        SetUpRun(0, """{"command":"marketplace-add","outcome":"ok","marketplace":"creativecoders-shade-kit","message":"Successfully added marketplace: creativecoders-shade-kit (declared in user settings)"}""" + "\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.AddMarketplaceAsync(GitHubSource, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();

        AssertRun("plugin", "marketplace", "add", GitHubSource, "--scope", "user", "--json");
    }

    [Fact]
    public async Task AddMarketplaceAsync_LocalDirectorySource_PassesPathAsOneArgument()
    {
        // Arrange
        const string source = "/Users/me/src/shade kit";
        SetUpRun(0, """{"command":"marketplace-add","outcome":"ok","message":"Marketplace is already on disk."}""");
        var sut = CreateSut();

        // Act
        await sut.AddMarketplaceAsync(source, TestContext.Current.CancellationToken);

        // Assert
        AssertRun("plugin", "marketplace", "add", source, "--scope", "user", "--json");
    }

    [Fact]
    public async Task AddMarketplaceAsync_ClaudeRejectsRef_ThrowsClaudeCommandFailedExceptionWithClaudeMessage()
    {
        // Arrange
        SetUpRun(1, """{"command":"marketplace-add","outcome":"failed","message":"Remote branch 0123abc not found"}""",
            "✘ Failed to add marketplace: Remote branch 0123abc not found\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.AddMarketplaceAsync("owner/repo#0123abc", TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("claude exited with the exit code 1 and reported the outcome 'failed': Remote branch " +
                         "0123abc not found.");

        exception.ClaudeMessage
            .Should().Be("Remote branch 0123abc not found");

        exception.FailureCode
            .Should().BeNull();

        exception.CommandText
            .Should().Be("claude plugin marketplace add owner/repo#0123abc --scope user --json");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("--scope")]
    [InlineData("-h")]
    public async Task AddMarketplaceAsync_InvalidSource_ThrowsArgumentExceptionWithoutRunningClaude(string? source)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.AddMarketplaceAsync(source!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName(nameof(source));

        AssertNoRun();
    }

    [Fact]
    public async Task RemoveMarketplaceAsync_ClaudeReportsOk_RunsRemoveWithoutScope()
    {
        // Arrange
        SetUpRun(0, """{"command":"marketplace-remove","outcome":"ok","marketplace":"creativecoders-shade-kit","message":"Successfully removed marketplace: creativecoders-shade-kit"}""");
        var sut = CreateSut();

        // Act
        await sut.RemoveMarketplaceAsync("creativecoders-shade-kit", TestContext.Current.CancellationToken);

        // Assert
        AssertRun("plugin", "marketplace", "remove", "creativecoders-shade-kit", "--json");
    }

    [Fact]
    public async Task RemoveMarketplaceAsync_UnknownMarketplace_ThrowsClaudeCommandFailedExceptionWithFailureCode()
    {
        // Arrange
        SetUpRun(1, """{"command":"marketplace-remove","marketplace":"nope","outcome":"failed","message":"Marketplace 'nope' not found","failureCode":"not_configured"}""",
            "✘ Failed to remove marketplace: Marketplace 'nope' not found\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.RemoveMarketplaceAsync("nope", TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("claude exited with the exit code 1 and reported the outcome 'failed': Marketplace 'nope' " +
                         "not found (failure code 'not_configured').");

        exception.FailureCode
            .Should().Be("not_configured");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-x")]
    public async Task RemoveMarketplaceAsync_InvalidName_ThrowsArgumentExceptionWithoutRunningClaude(string? name)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.RemoveMarketplaceAsync(name!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName(nameof(name));

        AssertNoRun();
    }

    [Fact]
    public async Task UpdateMarketplaceAsync_ClaudeReportsOk_RunsMarketplaceUpdateWithName()
    {
        // Arrange (output of Claude Code 2.1.323 for a git marketplace that follows a branch)
        SetUpRun(0, """{"command":"marketplace-update","outcome":"ok","marketplace":"creativecoders-shade-kit","message":"Refreshing marketplace cache (timeout: 120s)…\nCloning repository (timeout: 120s): https://github.com/CreativeCodersTeam/shade-kit.git (ref: main)\nReplacing the existing marketplace clone…\nClone complete, validating marketplace…\nSuccessfully updated marketplace: creativecoders-shade-kit"}""");
        var sut = CreateSut();

        // Act
        await sut.UpdateMarketplaceAsync("creativecoders-shade-kit", TestContext.Current.CancellationToken);

        // Assert
        AssertRun("plugin", "marketplace", "update", "creativecoders-shade-kit", "--json");
    }

    [Fact]
    public async Task UpdateMarketplaceAsync_UnknownMarketplace_ThrowsClaudeCommandFailedExceptionWithFailureCode()
    {
        // Arrange (output of Claude Code 2.1.323)
        SetUpRun(1, """{"command":"marketplace-update","marketplace":"nope","outcome":"failed","message":"Marketplace 'nope' not found. Available marketplaces: creativecoders-shade-kit","failureCode":"not_configured"}""",
            "✘ Failed to update marketplace(s): Marketplace 'nope' not found.\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateMarketplaceAsync("nope", TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.CommandText
            .Should().Be("claude plugin marketplace update nope --json");

        exception.FailureCode
            .Should().Be("not_configured");
    }

    [Fact]
    public async Task UpdateMarketplaceAsync_FailedOutcomeWithExitCodeZero_ThrowsClaudeCommandFailedException()
    {
        // Arrange
        SetUpRun(0, """{"command":"marketplace-update","outcome":"failed","message":"Clone failed."}""");
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateMarketplaceAsync("creativecoders-shade-kit", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ClaudeCommandFailedException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-x")]
    public async Task UpdateMarketplaceAsync_InvalidName_ThrowsArgumentExceptionWithoutRunningClaude(string? name)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateMarketplaceAsync(name!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName(nameof(name));

        AssertNoRun();
    }

    [Fact]
    public async Task InstallPluginAsync_NewPlugin_RunsInstallWithUserScopeAndReturnsNoVersions()
    {
        // Arrange
        SetUpRun(0, """{"command":"install","outcome":"ok","plugin":"shade-kit@creativecoders-shade-kit","pluginId":"shade-kit@creativecoders-shade-kit","scope":"user","message":"Successfully installed plugin: shade-kit@creativecoders-shade-kit (scope: user)"}""");
        var sut = CreateSut();

        // Act
        var result = await sut.InstallPluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new ClaudePluginInstallResult(null, null));

        AssertRun("plugin", "install", PluginId, "--scope", "user", "--json");
    }

    [Fact]
    public async Task InstallPluginAsync_PluginAlreadyInstalled_ReturnsInstalledAndAvailableVersion()
    {
        // Arrange
        SetUpRun(0, """{"command":"install","outcome":"ok","pluginId":"shade-kit@creativecoders-shade-kit","scope":"user","message":"Already installed.","installedVersion":"0.8.0","availableVersion":"0.9.0"}""");
        var sut = CreateSut();

        // Act
        var result = await sut.InstallPluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new ClaudePluginInstallResult("0.8.0", "0.9.0"));
    }

    [Fact]
    public async Task InstallPluginAsync_VersionIsNotString_ThrowsClaudeCommandFailedException()
    {
        // Arrange
        SetUpRun(0, """{"command":"install","outcome":"ok","installedVersion":8}""");
        var sut = CreateSut();

        // Act
        var act = () => sut.InstallPluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be("The field 'installedVersion' of the result of the claude output is not a string.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("--force")]
    public async Task InstallPluginAsync_InvalidPluginId_ThrowsArgumentExceptionWithoutRunningClaude(
        string? pluginId)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.InstallPluginAsync(pluginId!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName(nameof(pluginId));

        AssertNoRun();
    }

    [Fact]
    public async Task UpdatePluginAsync_PluginUpdated_RunsUpdateWithUserScopeAndReturnsVersions()
    {
        // Arrange
        SetUpRun(0, """{"command":"update","outcome":"ok","plugin":"shade-kit@creativecoders-shade-kit","pluginId":"shade-kit@creativecoders-shade-kit","scope":"user","message":"Updated.","updateOutcome":"updated","oldVersion":"0.8.0","newVersion":"0.9.0"}""");
        var sut = CreateSut();

        // Act
        var result = await sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new ClaudePluginUpdateResult(ClaudePluginUpdateResult.UpdatedOutcome, "0.8.0", "0.9.0"));

        result.IsUpToDate
            .Should().BeFalse();

        AssertRun("plugin", "update", PluginId, "--scope", "user", "--json");
    }

    [Fact]
    public async Task UpdatePluginAsync_PluginUpToDate_ReturnsUpToDateResult()
    {
        // Arrange
        SetUpRun(0, """{"command":"update","outcome":"ok","pluginId":"shade-kit@creativecoders-shade-kit","scope":"user","message":"shade-kit is already at the latest version (f35fdf204ffd).","updateOutcome":"up_to_date","oldVersion":"f35fdf204ffd","newVersion":"f35fdf204ffd"}""");
        var sut = CreateSut();

        // Act
        var result = await sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new ClaudePluginUpdateResult(ClaudePluginUpdateResult.UpToDateOutcome, "f35fdf204ffd",
                "f35fdf204ffd"));

        result.IsUpToDate
            .Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePluginAsync_NoUpdateOutcome_ReturnsResultThatIsNotUpToDate()
    {
        // Arrange
        SetUpRun(0, """{"command":"update","outcome":"ok"}""");
        var sut = CreateSut();

        // Act
        var result = await sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new ClaudePluginUpdateResult(null, null, null));

        result.IsUpToDate
            .Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePluginAsync_PluginNotFound_ThrowsClaudeCommandFailedExceptionWithMessageAndFailureCode()
    {
        // Arrange
        SetUpRun(1, UpdateNotFoundOutput + "\n",
            "✘ Failed to update plugin \"nope@creativecoders-shade-kit\": Plugin \"nope\" not found\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync("nope@creativecoders-shade-kit", TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Message
            .Should().Be("The claude command 'claude plugin update nope@creativecoders-shade-kit --scope user " +
                         "--json' failed: claude exited with the exit code 1 and reported the outcome 'failed': " +
                         "Plugin \"nope\" not found (failure code 'not_found').");

        exception.ExitCode
            .Should().Be(1);

        exception.ClaudeMessage
            .Should().Be("Plugin \"nope\" not found");

        exception.FailureCode
            .Should().Be("not_found");
    }

    [Fact]
    public async Task UpdatePluginAsync_FailedOutcomeWithExitCodeZero_ThrowsClaudeCommandFailedException()
    {
        // Arrange
        SetUpRun(0, UpdateNotFoundOutput);
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("claude reported the outcome 'failed': Plugin \"nope\" not found (failure code " +
                         "'not_found').");

        exception.ExitCode
            .Should().Be(0);
    }

    [Theory]
    [InlineData("""{"command":"update"}""")]
    [InlineData("""{"command":"update","outcome":null}""")]
    [InlineData("""{"command":"update","outcome":true}""")]
    public async Task UpdatePluginAsync_NoOutcome_ThrowsClaudeCommandFailedException(string output)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be("claude reported no outcome.");
    }

    [Fact]
    public async Task UpdatePluginAsync_OtherLinesAroundResult_ReadsLastJsonObjectLine()
    {
        // Arrange
        var output = "Checking marketplace...\r\n" +
                     """{"command":"progress","outcome":"failed"}""" + "\r\n" +
                     """{"command":"update","outcome":"ok","updateOutcome":"updated","oldVersion":"1","newVersion":"2"}""" +
                     "\r\n{not json\r\n[1,2]\r\n\r\n";
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var result = await sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new ClaudePluginUpdateResult(ClaudePluginUpdateResult.UpdatedOutcome, "1", "2"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Updated.\n")]
    [InlineData("[{\"outcome\":\"ok\"}]")]
    [InlineData("{\"outcome\":")]
    public async Task UpdatePluginAsync_NoJsonObjectLine_ThrowsClaudeCommandFailedException(string output)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be("claude printed no JSON object on standard output.");
    }

    [Fact]
    public async Task UpdatePluginAsync_NonZeroExitCodeWithoutJson_ThrowsClaudeCommandFailedExceptionWithStandardError()
    {
        // Arrange
        SetUpRun(1, string.Empty, "error: unknown option '--json'\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("claude exited with the exit code 1 and printed no JSON object on standard output: error: " +
                         "unknown option '--json'.");

        exception.ClaudeMessage
            .Should().BeNull();

        exception.FailureCode
            .Should().BeNull();
    }

    [Fact]
    public async Task UpdatePluginAsync_NonZeroExitCodeWithOkLineWithoutMessage_ThrowsClaudeCommandFailedException()
    {
        // Arrange
        SetUpRun(3, """{"command":"update","outcome":"ok"}""");
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be("claude exited with the exit code 3 and reported the outcome 'ok'.");
    }

    [Fact]
    public async Task UpdatePluginAsync_NonZeroExitCodeWithFailureCodeOnly_NamesExitCodeAndFailureCode()
    {
        // Arrange
        SetUpRun(1, """{"failureCode":"timeout","message":"  "}""");
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be("claude exited with the exit code 1 (failure code 'timeout').");
    }

    [Fact]
    public async Task UpdatePluginAsync_ClaudeNotFound_PropagatesClaudeNotFoundException()
    {
        // Arrange
        A.CallTo(() => _claudeCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
                A<CancellationToken>._))
            .ThrowsAsync(new ClaudeNotFoundException(["plugin"], "Not found."));
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowExactlyAsync<ClaudeNotFoundException>();
    }

    [Fact]
    public async Task ListPluginsAsync_Token_PassesTokenToRunner()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        SetUpRun(0, "[]");
        var sut = CreateSut();

        // Act
        await sut.ListPluginsAsync(cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => _claudeCommandRunner.RunAsync(WorkingDirectory, A<IReadOnlyList<string>>._,
                cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ListMarketplacesAsync_TextLineBeforeArray_ThrowsClaudeCommandFailedException()
    {
        // Arrange
        SetUpRun(0, "Update available.\n[]");
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().StartWith("The output of claude is not valid JSON: ");
    }

    [Fact]
    public async Task ListPluginsAsync_EmptyArray_ReturnsEmptyList()
    {
        // Arrange
        SetUpRun(0, "[]");
        var sut = CreateSut();

        // Act
        var plugins = await sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        plugins
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ListPluginsAsync_NonZeroExitCodeWithFailureLine_ThrowsClaudeCommandFailedExceptionWithDetails()
    {
        // Arrange
        SetUpRun(1, """{"command":"list","outcome":"failed","message":"Cannot read plugins","failureCode":"io"}""");
        var sut = CreateSut();

        // Act
        var act = () => sut.ListPluginsAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().Be("claude exited with the exit code 1 and reported the outcome 'failed': Cannot read plugins " +
                         "(failure code 'io').");

        exception.CommandText
            .Should().Be("claude plugin list --json");
    }

    [Fact]
    public async Task InstallPluginAsync_FailedOutcome_ThrowsClaudeCommandFailedExceptionWithDetails()
    {
        // Arrange
        SetUpRun(1, """{"command":"install","outcome":"failed","message":"Plugin \"shade-kit\" not found in marketplace","failureCode":"not_found"}""");
        var sut = CreateSut();

        // Act
        var act = () => sut.InstallPluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.FailureCode
            .Should().Be("not_found");

        exception.CommandText
            .Should().Be("claude plugin install shade-kit@creativecoders-shade-kit --scope user --json");
    }

    [Theory]
    [InlineData("""{"outcome":"ok","availableVersion":false}""", "availableVersion")]
    public async Task InstallPluginAsync_AvailableVersionIsNotString_ThrowsClaudeCommandFailedException(
        string output, string field)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.InstallPluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be($"The field '{field}' of the result of the claude output is not a string.");
    }

    [Theory]
    [InlineData("""{"outcome":"ok","updateOutcome":1}""", "updateOutcome")]
    [InlineData("""{"outcome":"ok","oldVersion":{}}""", "oldVersion")]
    [InlineData("""{"outcome":"ok","newVersion":[]}""", "newVersion")]
    public async Task UpdatePluginAsync_ResultFieldIsNotString_ThrowsClaudeCommandFailedException(string output,
        string field)
    {
        // Arrange
        SetUpRun(0, output);
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be($"The field '{field}' of the result of the claude output is not a string.");
    }

    [Fact]
    public async Task UpdatePluginAsync_OutcomeInOtherCase_ThrowsClaudeCommandFailedException()
    {
        // Arrange
        SetUpRun(0, """{"command":"update","outcome":"OK"}""");
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be("claude reported the outcome 'OK'.");
    }

    [Fact]
    public async Task UpdatePluginAsync_ExitCodeZeroWithoutJsonButWithStandardError_NamesStandardError()
    {
        // Arrange
        SetUpRun(0, "Done\n", "  warning: settings are read-only  \n");
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be("claude printed no JSON object on standard output: warning: settings are read-only.");
    }

    [Fact]
    public async Task UpdatePluginAsync_PrettyPrintedResultObject_ThrowsClaudeCommandFailedException()
    {
        // Arrange
        SetUpRun(0, "{\n  \"command\": \"update\",\n  \"outcome\": \"ok\"\n}\n");
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be("claude printed no JSON object on standard output.");
    }

    [Fact]
    public async Task UpdatePluginAsync_NegativeExitCode_NamesExitCodeWithSign()
    {
        // Arrange
        SetUpRun(-1073741510, string.Empty);
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be("claude exited with the exit code -1073741510 and printed no JSON object on standard " +
                         "output.");
    }

    [Fact]
    public async Task UpdatePluginAsync_MessageWithWhitespaceAndDotsAndWhitespaceFailureCode_NamesTrimmedMessageOnly()
    {
        // Arrange
        SetUpRun(1, """{"outcome":"failed","message":"  Network down...  ","failureCode":"  "}""");
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdatePluginAsync(PluginId, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which.Reason
            .Should().Be("claude exited with the exit code 1 and reported the outcome 'failed': Network down.");
    }

    [Fact]
    public async Task ListMarketplacesAsync_ClaudeUnavailable_PropagatesClaudeUnavailableException()
    {
        // Arrange
        A.CallTo(() => _claudeCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
                A<CancellationToken>._))
            .ThrowsAsync(new ClaudeUnavailableException(["plugin"], "Cannot start."));
        var sut = CreateSut();

        // Act
        var act = () => sut.ListMarketplacesAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowExactlyAsync<ClaudeUnavailableException>();
    }

    [Fact]
    public async Task AddMarketplaceAsync_RunnerCanceled_PropagatesOperationCanceledException()
    {
        // Arrange
        A.CallTo(() => _claudeCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
                A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());
        var sut = CreateSut();

        // Act
        var act = () => sut.AddMarketplaceAsync(GitHubSource, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowExactlyAsync<OperationCanceledException>();
    }

    public static TheoryData<string> CommandNames =>
        ["list-marketplaces", "add", "remove", "update-marketplace", "install", "update"];

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task EveryCommand_Token_PassesTokenToRunner(string commandName)
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var output = commandName == "list-marketplaces" ? "[]" : """{"outcome":"ok"}""";
        SetUpRun(0, output);
        var sut = CreateSut();
        var token = cancellationTokenSource.Token;

        // Act
        Task task = commandName switch
        {
            "list-marketplaces" => sut.ListMarketplacesAsync(token),
            "add" => sut.AddMarketplaceAsync(GitHubSource, token),
            "remove" => sut.RemoveMarketplaceAsync("creativecoders-shade-kit", token),
            "update-marketplace" => sut.UpdateMarketplaceAsync("creativecoders-shade-kit", token),
            "install" => sut.InstallPluginAsync(PluginId, token),
            _ => sut.UpdatePluginAsync(PluginId, token)
        };
        await task;

        // Assert
        A.CallTo(() => _claudeCommandRunner.RunAsync(WorkingDirectory, A<IReadOnlyList<string>>._, token))
            .MustHaveHappenedOnceExactly();
    }

    private ClaudePluginCli CreateSut()
    {
        return new ClaudePluginCli(_claudeCommandRunner, WorkingDirectory);
    }

    private void SetUpRun(int exitCode, string standardOutput, string standardError = "")
    {
        A.CallTo(() => _claudeCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
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
        A.CallTo(_claudeCommandRunner)
            .MustNotHaveHappened();
    }
}
