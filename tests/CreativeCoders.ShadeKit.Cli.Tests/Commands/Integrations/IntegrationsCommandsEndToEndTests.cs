using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using static CreativeCoders.ShadeKit.Cli.Tests.Commands.CliEndToEndHarness;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations;

/// <summary>
/// Runs <c>shade-kit integrations install</c>, <c>shade-kit integrations update</c>, and <c>shade-kit version</c>
/// through the <see cref="CliEndToEndHarness" /> with the real integration service; <c>claude</c> is answered by the
/// <see cref="FakeClaudeCode" /> simulator, so no test starts <c>claude</c>.
/// </summary>
[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed class IntegrationsCommandsEndToEndTests : IDisposable
{
    private readonly CliEndToEndHarness _harness = new CliEndToEndHarness("shade-kit-integrations-tests-");

    private readonly FakeClaudeCode _claude = new FakeClaudeCode();

    private string _engineVersion = "0.8.0";

    public IntegrationsCommandsEndToEndTests()
    {
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).ReturnsLazily(() => _engineVersion);

        _harness.ConfigureServices = x => x.AddSingleton(engineVersionProvider);
    }

    [Fact]
    [Trait("Requirement", "FP07-AC1")]
    [Trait("Requirement", "FP07-AC2")]
    public async Task IntegrationsInstall_ClaudeWithoutIntegration_InstallsPluginFromReleaseTagOfEngine()
    {
        // Arrange
        _claude.AttachTo(_harness);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--agent", "claude", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        run.Root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        run.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "ref": "v0.8.0", "source": "CreativeCodersTeam/shade-kit#v0.8.0",
              "integrations": [
                { "agent": "claude", "pluginId": "shade-kit@creativecoders-shade-kit", "outcome": "installed",
                  "previousKitVersion": null, "kitVersion": "0.8.0", "ref": "v0.8.0", "enabled": true }
              ]
            }
            """);

        _claude.Calls
            .Should().Contain([
                "plugin marketplace add CreativeCodersTeam/shade-kit#v0.8.0 --scope user --json",
                "plugin install shade-kit@creativecoders-shade-kit --scope user --json"
            ]);

        _claude.Marketplace
            .Should().Be(FakeClaudeMarketplace.ForGitHub(FakeClaudeCode.Repo, "v0.8.0"));
    }

    [Fact]
    public async Task IntegrationsInstall_PlainModeWithoutAgent_InstallsForDetectedClaudeAndAsksForRestart()
    {
        // Arrange
        _claude.AttachTo(_harness);

        // Act
        var (exitCode, output, error) = await _harness.RunAsync("integrations", "install");

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output.TrimEnd())
            .Should().Equal(
                "claude: installed shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "Restart Claude Code to load the skills.");

        error
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("CLAUDE")]
    [InlineData("Claude")]
    public async Task IntegrationsInstall_AgentInOtherCase_InstallsForClaude(string agent)
    {
        // Arrange
        _claude.AttachTo(_harness);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--agent", agent, "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("integrations")[0].GetProperty("agent").GetString()
            .Should().Be("claude");
    }

    [Theory]
    [InlineData("copilot")]
    [InlineData("")]
    public async Task IntegrationsInstall_UnknownAgent_WritesInvalidArgumentsEnvelopeAndExits2(string agent)
    {
        // Arrange
        _claude.AttachTo(_harness);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--agent", agent, "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be($"Invalid arguments: '{agent}' is not a supported agent for --agent (supported: claude). " +
                         "Run 'shade-kit integrations install --help' for usage.");

        _claude.Calls
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("integrations", "install", "--agent", "--json")]
    [InlineData("integrations", "install", "--json", "--agent")]
    public async Task IntegrationsInstall_AgentWithoutValue_WritesInvalidArgumentsEnvelopeAndExits2(
        params string[] args)
    {
        // Arrange
        _claude.AttachTo(_harness);

        // Act
        var run = await _harness.RunJsonAsync(args);

        // Assert
        AssertError(run, ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be("Invalid arguments: --agent needs the id of a supported agent (claude), but got none. Run " +
                         "'shade-kit integrations install --help' for usage.");

        _claude.Calls
            .Should().BeEmpty();
    }

    [Fact]
    public async Task IntegrationsInstall_PlainModeUnknownAgent_WritesMessageToStandardErrorAndExits2()
    {
        // Act
        var (exitCode, output, error) = await _harness.RunAsync("integrations", "install", "--agent", "copilot");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        output
            .Should().BeEmpty();

        error
            .Should().Contain("'copilot' is not a supported agent for --agent (supported: claude)");
    }

    [Fact]
    public async Task IntegrationsInstall_NoAgentDetected_WritesAgentNotFoundEnvelopeAndExits51()
    {
        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentNotFound, ShadeKitCliErrorCodes.AgentNotFound);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().StartWith("No supported agent was found (supported: claude).");
    }

    [Fact]
    public async Task IntegrationsInstall_NamedClaudeNotOnPath_WritesAgentNotFoundEnvelopeAndExits51()
    {
        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--agent", "claude", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentNotFound, ShadeKitCliErrorCodes.AgentNotFound);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().StartWith("Claude Code is needed, but its CLI 'claude' was not found.");
    }

    [Fact]
    public async Task IntegrationsInstall_NamedClaudeNotOnPath_FailsWithoutReleaseTagCheck()
    {
        // Arrange
        _harness.RemoteTags = ["v0.8.0"];
        var gitCalls = new List<string>();
        _harness.OnGitCall = x => gitCalls.Add(string.Join(' ', x));

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--agent", "claude", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentNotFound, ShadeKitCliErrorCodes.AgentNotFound);

        gitCalls
            .Should().BeEmpty("a missing named agent fails before the network call");
    }

    [Fact]
    public async Task IntegrationsInstall_PluginInstallFails_WritesAgentCommandFailedEnvelopeAndExits52()
    {
        // Arrange
        _claude.AttachTo(_harness).Fail = arguments => arguments is ["plugin", "install", ..]
            ? new ExternalProcessResult(1,
                """{"command":"install","outcome":"failed","plugin":"shade-kit@creativecoders-shade-kit","message":"Network error","failureCode":"network"}""" +
                "\n", "✘ Network error\n")
            : null;

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentCommandFailed, ShadeKitCliErrorCodes.AgentCommandFailed);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().StartWith("'claude plugin install shade-kit@creativecoders-shade-kit --scope user --json' failed:")
            .And.Contain("Network error");
    }

    [Fact]
    public async Task IntegrationsInstall_MarketplaceWithOtherRepo_WritesIntegrationConflictEnvelopeAndChangesNothing()
    {
        // Arrange
        _claude.AttachTo(_harness).Marketplace = FakeClaudeMarketplace.ForGitHub("someone/fork", "v0.8.0");

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.IntegrationConflict, ShadeKitCliErrorCodes.IntegrationConflict);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("claude plugin marketplace remove creativecoders-shade-kit");

        _claude.Calls
            .Should().OnlyContain(x => x.EndsWith("list --json", StringComparison.Ordinal));

        _claude.Marketplace
            .Should().Be(FakeClaudeMarketplace.ForGitHub("someone/fork", "v0.8.0"));
    }

    [Fact]
    public async Task IntegrationsInstall_FailureAfterMarketplaceSwitch_NamesInstallAsRepair()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.7.0").Fail = arguments =>
            arguments is ["plugin", "marketplace", "add", ..]
                ? new ExternalProcessResult(1, string.Empty, "✘ Remote branch v0.8.0 not found\n")
                : null;

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentCommandFailed, ShadeKitCliErrorCodes.AgentCommandFailed);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().EndWith("'shade-kit integrations install' repairs it.");
    }

    [Fact]
    public async Task IntegrationsInstall_PrereleaseEngine_InstallsFromMainWithPrereleaseWarning()
    {
        // Arrange
        _engineVersion = "0.9.0-beta.1";
        _claude.AttachTo(_harness);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("warnings").EnumerateArray().Select(x => x.GetProperty("code").GetString())
            .Should().Equal(ShadeKitCliWarningCodes.IntegrationPrereleaseRef);

        run.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "ref": "main", "source": "CreativeCodersTeam/shade-kit#main",
              "integrations": [
                { "agent": "claude", "pluginId": "shade-kit@creativecoders-shade-kit", "outcome": "installed",
                  "previousKitVersion": null, "kitVersion": null, "ref": "main", "enabled": true }
              ]
            }
            """);
    }

    [Fact]
    public async Task IntegrationsInstall_ReleaseTagExists_ChecksTagAndInstallsFromIt()
    {
        // Arrange
        _claude.AttachTo(_harness);
        _harness.RemoteTags = ["v0.7.0", "v0.8.0"];
        var gitCalls = new List<string>();
        _harness.OnGitCall = x => gitCalls.Add(string.Join(' ', x));

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("source").GetString()
            .Should().Be("CreativeCodersTeam/shade-kit#v0.8.0");

        gitCalls
            .Should().Equal("-c credential.helper= -c protocol.allow=never -c protocol.https.allow=always " +
                            "ls-remote --tags https://github.com/CreativeCodersTeam/shade-kit.git refs/tags/v0.8.0");
    }

    [Fact]
    public async Task IntegrationsInstall_ReleaseTagMissing_InstallsFromMainWithReleaseTagMissingWarning()
    {
        // Arrange
        _engineVersion = "0.8.1";
        _claude.AttachTo(_harness);
        _harness.RemoteTags = ["v0.8.0"];

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("warnings").ShouldBeJson(
            """
            [ { "code": "integration-release-tag-missing",
                "message": "The release tag 'v0.8.1' of the engine 0.8.1 does not exist in CreativeCodersTeam/shade-kit, so the integrations come from the branch 'main' instead; they may not fit this engine." } ]
            """);

        run.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "ref": "main", "source": "CreativeCodersTeam/shade-kit#main",
              "integrations": [
                { "agent": "claude", "pluginId": "shade-kit@creativecoders-shade-kit", "outcome": "installed",
                  "previousKitVersion": null, "kitVersion": null, "ref": "main", "enabled": true }
              ]
            }
            """);

        _claude.Calls
            .Should().Contain("plugin marketplace add CreativeCodersTeam/shade-kit#main --scope user --json");
    }

    [Fact]
    public async Task IntegrationsUpdate_PlainModeReleaseTagMissing_WritesWarningAndUpdatesFromMain()
    {
        // Arrange
        _engineVersion = "0.8.1";
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        _harness.RemoteTags = ["v0.8.0"];

        // Act
        var (exitCode, output, _) = await _harness.RunAsync("integrations", "update");

        // Assert
        exitCode
            .Should().Be(0);

        FoldWhitespace(output)
            .Should().Contain("Warning: The release tag 'v0.8.1' of the engine 0.8.1 does not exist in " +
                              "CreativeCodersTeam/shade-kit, so the integrations come from the branch 'main' " +
                              "instead; they may not fit this engine.")
            .And.Contain("claude: updated shade-kit@creativecoders-shade-kit 0.8.0 -> (main)");
    }

    [Fact]
    public async Task IntegrationsInstall_ReleaseTagCannotBeChecked_InstallsFromTagWithoutWarning()
    {
        // Arrange
        _claude.AttachTo(_harness);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("ref").GetString()
            .Should().Be("v0.8.0");
    }

    [Fact]
    public async Task IntegrationsInstall_DirectoryOverride_InstallsFromDirectoryAndReportsItsPath()
    {
        // Arrange
        _claude.AttachTo(_harness);
        var sourcePath = _harness.TempDirectory.CreateDirectory("kit");
        var previousValue = Environment.GetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName);

        (int ExitCode, JsonElement Root) run;
        (int ExitCode, string Output, string Error) plainRun;

        // Act
        Environment.SetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName, sourcePath);

        try
        {
            run = await _harness.RunJsonAsync("integrations", "install", "--json");
            plainRun = await _harness.RunAsync("integrations", "install");
        }
        finally
        {
            Environment.SetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName, previousValue);
        }

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("ref").ValueKind
            .Should().Be(JsonValueKind.Null);

        run.Root.GetProperty("data").GetProperty("source").GetString()
            .Should().Be(sourcePath);

        run.Root.GetProperty("data").GetProperty("integrations")[0].GetProperty("kitVersion").ValueKind
            .Should().Be(JsonValueKind.Null);

        // The console wraps long lines at spaces and splits words that are longer than a line, such as the path.
        FoldWhitespace(plainRun.Output)
            .Should().StartWith("claude: already current shade-kit@creativecoders-shade-kit from ");

        RemoveWhitespace(plainRun.Output)
            .Should().Contain(RemoveWhitespace(sourcePath));
    }

    [Theory]
    [InlineData("integrations", "install", "--json")]
    [InlineData("integrations", "update", "--json")]
    public async Task IntegrationsCommand_InvalidMarketplaceSourceOverride_WritesInvalidArgumentsEnvelopeAndExits2(
        params string[] args)
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        var previousValue = Environment.GetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName);

        (int ExitCode, JsonElement Root) run;

        // Act
        Environment.SetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName, "owner/repo#");

        try
        {
            run = await _harness.RunJsonAsync(args);
        }
        finally
        {
            Environment.SetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName, previousValue);
        }

        // Assert
        AssertError(run, ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().StartWith($"The environment variable {IntegrationService.MarketplaceSourceVariableName} is " +
                                "set to 'owner/repo#'");

        _claude.Calls
            .Should().NotContain(x => x.Contains(" add ", StringComparison.Ordinal) ||
                                      x.Contains(" remove ", StringComparison.Ordinal) ||
                                      x.Contains(" install ", StringComparison.Ordinal) ||
                                      x.Contains(" update ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://github.com/CreativeCodersTeam/shade-kit.git", "It is a URL")]
    [InlineData("git@github.com:CreativeCodersTeam/shade-kit.git", "It is an scp-like git URL")]
    public async Task IntegrationsInstall_UrlAsMarketplaceSourceOverride_Exits2NamingAllowedFormsWithoutClaudeCall(
        string value, string expectedReasonStart)
    {
        // Arrange
        _claude.AttachTo(_harness);
        var previousValue = Environment.GetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName);

        (int ExitCode, JsonElement Root) run;

        // Act
        Environment.SetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName, value);

        try
        {
            run = await _harness.RunJsonAsync("integrations", "install", "--json");
        }
        finally
        {
            Environment.SetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName, previousValue);
        }

        // Assert
        AssertError(run, ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().StartWith($"The environment variable {IntegrationService.MarketplaceSourceVariableName} is " +
                                $"set to '{value}', which is not a marketplace source: {expectedReasonStart}")
            .And.Contain("a GitHub repository is written as owner/repo#ref or owner/repo.")
            .And.EndWith("Set it to owner/repo#ref, owner/repo, or the path of a local directory, or remove it, " +
                         "then run the command again.");

        _claude.Calls
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP07-AC3")]
    public async Task IntegrationsUpdate_AfterEngineUpdate_SwitchesToNewTagAndReportsOldAndNewVersion()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.7.0");

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "ref": "v0.8.0", "source": "CreativeCodersTeam/shade-kit#v0.8.0",
              "integrations": [
                { "agent": "claude", "pluginId": "shade-kit@creativecoders-shade-kit", "outcome": "updated",
                  "previousKitVersion": "0.7.0", "kitVersion": "0.8.0", "ref": "v0.8.0", "enabled": true }
              ]
            }
            """);

        _claude.Calls
            .Should().ContainInOrder(
                "plugin marketplace remove creativecoders-shade-kit --json",
                "plugin marketplace add CreativeCodersTeam/shade-kit#v0.8.0 --scope user --json",
                "plugin install shade-kit@creativecoders-shade-kit --scope user --json");

        _claude.Marketplace
            .Should().Be(FakeClaudeMarketplace.ForGitHub(FakeClaudeCode.Repo, "v0.8.0"));
    }

    [Fact]
    [Trait("Requirement", "FP07-AC3")]
    public async Task IntegrationsUpdate_PlainModeAfterEngineUpdate_WritesOldAndNewVersionAndAsksForRestart()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.7.0");

        // Act
        var (exitCode, output, _) = await _harness.RunAsync("integrations", "update");

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output.TrimEnd())
            .Should().Equal(
                "claude: updated shade-kit@creativecoders-shade-kit 0.7.0 -> 0.8.0 (v0.8.0)",
                "Restart Claude Code to load the skills.");
    }

    [Fact]
    public async Task IntegrationsUpdate_IntegrationOfEngineVersion_ReportsCurrentWithoutRestartHint()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");
        var (exitCode, output, _) = await _harness.RunAsync("integrations", "update");

        // Assert
        run.Root.GetProperty("data").GetProperty("integrations")[0].GetProperty("outcome").GetString()
            .Should().Be("current");

        _claude.Calls
            .Should().NotContain(x => x.StartsWith("plugin marketplace add", StringComparison.Ordinal) ||
                                      x.StartsWith("plugin marketplace remove", StringComparison.Ordinal));

        exitCode
            .Should().Be(0);

        GetLines(output.TrimEnd())
            .Should().Equal("claude: already current shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)");
    }

    [Fact]
    public async Task IntegrationsUpdate_KeptMarketplace_ReadsPluginsAndMarketplacesOnceAndRefreshesMarketplace()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        _claude.Calls
            .Should().Equal(
                "plugin list --json",
                "plugin marketplace list --json",
                "plugin marketplace update creativecoders-shade-kit --json",
                "plugin update shade-kit@creativecoders-shade-kit --scope user --json");
    }

    [Fact]
    public async Task IntegrationsUpdate_BranchMarketplaceWithNewCommits_RefreshesMarketplaceAndReportsUpdated()
    {
        // Arrange
        _engineVersion = "0.9.0-alpha.1";
        _claude.AttachTo(_harness).WithInstalledPlugin("main");
        _claude.RemoteRevision++;

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("integrations")[0].GetProperty("outcome").GetString()
            .Should().Be("updated", "the new commits of the branch must reach the plugin");

        _claude.Calls
            .Should().ContainInOrder(
                "plugin marketplace update creativecoders-shade-kit --json",
                "plugin update shade-kit@creativecoders-shade-kit --scope user --json");
    }

    [Fact]
    public async Task IntegrationsUpdate_BranchMarketplaceWithoutNewCommits_ReportsCurrent()
    {
        // Arrange
        _engineVersion = "0.9.0-alpha.1";
        _claude.AttachTo(_harness).WithInstalledPlugin("main");

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.Root.GetProperty("data").GetProperty("integrations")[0].GetProperty("outcome").GetString()
            .Should().Be("current");
    }

    [Fact]
    public async Task IntegrationsUpdate_MarketplaceRefreshFails_WritesAgentCommandFailedEnvelopeAndExits52()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0").Fail = arguments =>
            arguments is ["plugin", "marketplace", "update", ..]
                ? new ExternalProcessResult(1,
                    """{"command":"marketplace-update","outcome":"failed","marketplace":"creativecoders-shade-kit","message":"Clone failed"}""" +
                    "\n", "✘ Clone failed\n")
                : null;

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentCommandFailed, ShadeKitCliErrorCodes.AgentCommandFailed);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().StartWith("'claude plugin marketplace update creativecoders-shade-kit --json' failed:")
            .And.Contain("Clone failed");

        _claude.Calls
            .Should().NotContain(x => x.StartsWith("plugin update", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IntegrationsUpdate_DisabledPlugin_KeepsItDisabledAndSaysHowToEnableIt()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0", enabled: false);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");
        var (_, output, _) = await _harness.RunAsync("integrations", "update");

        // Assert
        run.Root.GetProperty("data").GetProperty("integrations")[0].GetProperty("enabled").GetBoolean()
            .Should().BeFalse();

        _claude.IsPluginEnabled
            .Should().BeFalse();

        FoldWhitespace(output)
            .Should().Be("claude: already current shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0) The plugin is " +
                         "disabled in Claude Code. Run 'claude plugin enable shade-kit@creativecoders-shade-kit' to " +
                         "enable it. ");
    }

    [Fact]
    [Trait("Requirement", "FP07-AC6")]
    public async Task IntegrationsUpdate_NothingInstalled_WritesEmptyIntegrationsAndExits0()
    {
        // Arrange
        _claude.AttachTo(_harness);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        run.Root.GetProperty("data").ShouldBeJson(
            """
            { "ref": null, "source": null, "integrations": [] }
            """);

        run.Root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        _claude.Calls
            .Should().Equal("plugin list --json");
    }

    [Theory]
    [InlineData("0.8.0")]
    [InlineData("0.9.0-beta.1")]
    public async Task IntegrationsUpdate_NothingInstalled_ChecksNoTagAndWritesNoSourceWarning(string engineVersion)
    {
        // Arrange
        _engineVersion = engineVersion;
        _claude.AttachTo(_harness);
        _harness.RemoteTags = [];
        var gitCalls = new List<string>();
        _harness.OnGitCall = x => gitCalls.Add(string.Join(' ', x));

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        gitCalls
            .Should().NotContain(x => x.Contains("ls-remote", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "FP07-AC6")]
    public async Task IntegrationsUpdate_PlainModeNothingInstalled_SaysNothingIsInstalledAndExits0()
    {
        // Arrange
        _claude.AttachTo(_harness);

        // Act
        var (exitCode, output, error) = await _harness.RunAsync("integrations", "update");

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output.TrimEnd())
            .Should().Equal(
                "No SHADE kit integration is installed.",
                "Run 'shade-kit integrations install' to install it.");

        error
            .Should().BeEmpty();
    }

    [Fact]
    public async Task IntegrationsUpdate_ClaudeNotOnPath_WritesAgentNotFoundEnvelopeAndExits51()
    {
        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentNotFound, ShadeKitCliErrorCodes.AgentNotFound);
    }

    [Fact]
    public async Task IntegrationsUpdate_PluginListFails_WritesAgentCommandFailedEnvelopeAndExits52()
    {
        // Arrange
        _claude.AttachTo(_harness).Fail = _ => new ExternalProcessResult(0, "not json\n", string.Empty);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentCommandFailed, ShadeKitCliErrorCodes.AgentCommandFailed);
    }

    [Fact]
    public async Task IntegrationsUpdate_MarketplaceWithDirectorySource_WritesIntegrationConflictEnvelopeAndExits53()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0").Marketplace =
            new FakeClaudeMarketplace("directory", null, null, "/src/shade-kit");

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.IntegrationConflict, ShadeKitCliErrorCodes.IntegrationConflict);

        _claude.IsPluginInstalled
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP07-AC5")]
    public async Task IntegrationsInstallAndUpdate_InGitRepository_CreateOrChangeNoFileInIt()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync();
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.7.0");
        var snapshot = _harness.TempDirectory.TakeSnapshot();

        // Act
        await _harness.RunAllOkAsync(["integrations", "update"], ["integrations", "install"],
            ["integrations", "install", "--agent", "claude"]);

        _engineVersion = "0.9.0";

        (await _harness.RunAsync("integrations", "update")).ExitCode
            .Should().Be(0);

        (await _harness.RunAsync("integrations", "install")).ExitCode
            .Should().Be(0);

        // Assert
        _harness.TempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _claude.Calls
            .Should().Contain("plugin marketplace add CreativeCodersTeam/shade-kit#v0.9.0 --scope user --json");

        _claude.WorkingDirectories
            .Should().NotBeEmpty()
            .And.OnlyContain(x => !x.StartsWith(_harness.TempDirectory.RootPath, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "FP07-AC4")]
    public async Task Version_JsonModeWithInstalledClaudeIntegration_ListsItWithKitVersion()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");

        // Act
        var run = await _harness.RunJsonAsync("version", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("integrations").ShouldBeJson(
            """
            [ { "agent": "claude", "pluginId": "shade-kit@creativecoders-shade-kit", "kitVersion": "0.8.0",
                "ref": "v0.8.0", "enabled": true } ]
            """);
    }

    [Fact]
    [Trait("Requirement", "FP07-AC4")]
    public async Task Version_AfterInstall_ListsIntegrationOfEngineVersionInPlainMode()
    {
        // Arrange
        _claude.AttachTo(_harness);
        await _harness.RunAllOkAsync(["integrations", "install"]);

        // Act
        var (exitCode, output, _) = await _harness.RunAsync("version");

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output.TrimEnd())
            .Should().Equal("shade-kit 0.8.0", "claude: shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)");
    }

    [Fact]
    public async Task Version_ClaudeNotOnPath_SucceedsWithEmptyIntegrationsAndNoWarning()
    {
        // Act
        var run = await _harness.RunJsonAsync("version", "--json", "--expect", "0.8.0");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        run.Root.GetProperty("data").GetProperty("expectedVersion").GetString()
            .Should().Be("0.8.0");

        run.Root.GetProperty("data").GetProperty("integrations").GetArrayLength()
            .Should().Be(0);

        run.Root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        A.CallTo(_harness.ClaudeCommandRunner)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Version_ClaudeOnPathFails_SucceedsWithEmptyIntegrationsAndAgentUnavailableWarning()
    {
        // Arrange
        _claude.AttachTo(_harness).Fail = _ => new ExternalProcessResult(1, string.Empty, "claude crashed");

        // Act
        var run = await _harness.RunJsonAsync("version", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("integrations").GetArrayLength()
            .Should().Be(0);

        run.Root.GetProperty("warnings").EnumerateArray().Select(x => x.GetProperty("code").GetString())
            .Should().Equal(ShadeKitCliWarningCodes.AgentUnavailable);
    }

    [Fact]
    public async Task Version_ExpectMismatchWithInstalledIntegration_StillExits50()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");

        // Act
        var run = await _harness.RunJsonAsync("version", "--json", "--expect", "0.9.0");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.VersionMismatch, ShadeKitCliErrorCodes.VersionMismatch);

        _claude.Calls
            .Should().BeEmpty();
    }

    [Fact]
    public async Task IntegrationsHelp_PlainMode_ListsInstallAndUpdate()
    {
        // Act
        var (exitCode, output, _) = await _harness.RunAsync("integrations", "--help");

        // Assert
        exitCode
            .Should().Be(0);

        output
            .Should().Contain("install").And.Contain("update");
    }

    public void Dispose()
    {
        _harness.Dispose();
    }

    private static string RemoveWhitespace(string text)
    {
        return string.Concat(text.Where(x => !char.IsWhiteSpace(x)));
    }
}
