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
/// through the <see cref="CliEndToEndHarness" /> with the real integration service for GitHub Copilot CLI and OpenAI
/// Codex CLI, alone and together with Claude Code; <c>claude</c>, <c>copilot</c>, and <c>codex</c> are answered by the
/// <see cref="FakeClaudeCode" />, <see cref="FakeCopilot" />, and <see cref="FakeCodex" /> simulators, so no test starts
/// an agent CLI.
/// </summary>
[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed class IntegrationsMultiAgentEndToEndTests : IDisposable
{
    private const string CopilotResolveHint =
        "'copilot plugin uninstall shade-kit@creativecoders-shade-kit' and then " +
        "'copilot plugin marketplace remove creativecoders-shade-kit'";

    private const string CodexResolveHint = "'codex plugin marketplace remove creativecoders-shade-kit'";

    private const string ReleaseInstalledJson =
        """
        {
          "ref": "v0.8.0", "source": "CreativeCodersTeam/shade-kit#v0.8.0",
          "integrations": [
            { "agent": "{agent}", "pluginId": "shade-kit@creativecoders-shade-kit", "outcome": "installed",
              "previousKitVersion": null, "kitVersion": "0.8.0", "ref": "v0.8.0", "enabled": true }
          ]
        }
        """;

    private readonly CliEndToEndHarness _harness = new CliEndToEndHarness("shade-kit-multi-agent-tests-");

    private readonly FakeClaudeCode _claude = new FakeClaudeCode();

    private readonly FakeCopilot _copilot = new FakeCopilot();

    private readonly FakeCodex _codex = new FakeCodex();

    private string _engineVersion = "0.8.0";

    public IntegrationsMultiAgentEndToEndTests()
    {
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).ReturnsLazily(() => _engineVersion);

        _harness.ConfigureServices = x => x.AddSingleton(engineVersionProvider);
    }

    [Fact]
    [Trait("Requirement", "FP08-AC2")]
    public async Task IntegrationsInstall_CopilotWithoutIntegration_InstallsPluginFromReleaseTagOfEngine()
    {
        // Arrange
        _copilot.AttachTo(_harness);
        _harness.RemoteTags = ["v0.8.0"];

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--agent", "copilot", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        run.Root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        run.Root.GetProperty("data").ShouldBeJson(ReleaseInstalledJson.Replace("{agent}", "copilot",
            StringComparison.Ordinal));

        _copilot.Calls
            .Should().Equal(
                "plugin list --json",
                "plugin marketplace list --json",
                "plugin marketplace add CreativeCodersTeam/shade-kit#v0.8.0",
                "plugin list --json",
                "plugin install shade-kit@creativecoders-shade-kit",
                "plugin list --json");

        _copilot.Marketplace
            .Should().Be(FakeCopilotMarketplace.ForGitHub(FakeCopilot.Repo, "v0.8.0"));

        _copilot.IsPluginInstalled
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP08-AC3")]
    public async Task IntegrationsInstall_CodexWithoutIntegration_InstallsPluginFromReleaseTagOfEngine()
    {
        // Arrange
        _codex.AttachTo(_harness);
        _harness.RemoteTags = ["v0.8.0"];

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--agent", "codex", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        run.Root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        run.Root.GetProperty("data").ShouldBeJson(ReleaseInstalledJson.Replace("{agent}", "codex",
            StringComparison.Ordinal));

        _codex.Calls
            .Should().Equal(
                "plugin list --json",
                "plugin marketplace list --json",
                "plugin marketplace add CreativeCodersTeam/shade-kit --ref v0.8.0 --json",
                "plugin add shade-kit@creativecoders-shade-kit --json",
                "plugin list --json");

        _codex.Marketplace
            .Should().Be(FakeCodexMarketplace.ForGitHub(FakeCodex.Repo, "v0.8.0"));
    }

    [Theory]
    [InlineData("copilot", "copilot: installed shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
        "Restart GitHub Copilot CLI to load the skills.")]
    [InlineData("codex", "codex: installed shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
        "Start a new Codex session to load the skills.")]
    public async Task IntegrationsInstall_PlainModeNamedAgent_WritesResultAndRestartNoteOfThatAgent(string agent,
        string expectedResultLine, string expectedRestartNote)
    {
        // Arrange
        _copilot.AttachTo(_harness);
        _codex.AttachTo(_harness);

        // Act
        var (exitCode, output, error) = await _harness.RunAsync("integrations", "install", "--agent", agent);

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output.TrimEnd())
            .Should().Equal(expectedResultLine, expectedRestartNote);

        error
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("copilot", "GitHub Copilot CLI is needed, but its command 'copilot' was not found.")]
    [InlineData("codex", "OpenAI Codex CLI is needed, but its command 'codex' was not found.")]
    public async Task IntegrationsInstall_NamedAgentNotOnPath_Exits51BeforeReleaseTagCheck(string agent,
        string expectedMessageStart)
    {
        // Arrange
        _claude.AttachTo(_harness);
        _harness.RemoteTags = ["v0.8.0"];
        var gitCalls = new List<string>();
        _harness.OnGitCall = x => gitCalls.Add(string.Join(' ', x));

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--agent", agent, "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentNotFound, ShadeKitCliErrorCodes.AgentNotFound);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().StartWith(expectedMessageStart);

        gitCalls
            .Should().BeEmpty("a missing named agent fails before the network call");

        _claude.Calls
            .Should().BeEmpty();
    }

    [Fact]
    public async Task IntegrationsInstall_NoAgentDetected_Exits51NamingEveryAgentAndHowItIsDetected()
    {
        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentNotFound, ShadeKitCliErrorCodes.AgentNotFound);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be("No supported agent was found (supported: claude, copilot, codex). SHADE kit detects Claude " +
                         "Code by its CLI 'claude', GitHub Copilot CLI by 'copilot', and OpenAI Codex CLI by " +
                         "'codex', each in an absolute directory of the PATH. Install a supported agent, then run " +
                         "the command again. On Windows only the 'claude.exe' of the native installer of Claude " +
                         "Code is found, not the 'claude.cmd' of an npm installation; for GitHub Copilot CLI and " +
                         "OpenAI Codex CLI 'copilot.exe' and 'codex.exe' are found, and so are the npm shims " +
                         "'copilot.cmd' and 'codex.cmd', which are run through cmd.exe.");
    }

    [Fact]
    public async Task IntegrationsInstall_AllThreeAgentsDetected_InstallsInRegistrationOrder()
    {
        // Arrange
        _claude.AttachTo(_harness);
        _copilot.AttachTo(_harness);
        _codex.AttachTo(_harness);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("integrations").EnumerateArray()
            .Select(x => (x.GetProperty("agent").GetString(), x.GetProperty("outcome").GetString()))
            .Should().Equal(("claude", "installed"), ("copilot", "installed"), ("codex", "installed"));

        _claude.IsPluginInstalled
            .Should().BeTrue();

        _copilot.IsPluginInstalled
            .Should().BeTrue();

        _codex.IsPluginInstalled
            .Should().BeTrue();
    }

    [Fact]
    public async Task IntegrationsInstall_PlainModeAllThreeAgentsDetected_WritesOneRestartNotePerAgentInOrder()
    {
        // Arrange
        _claude.AttachTo(_harness);
        _copilot.AttachTo(_harness);
        _codex.AttachTo(_harness);

        // Act
        var (exitCode, output, _) = await _harness.RunAsync("integrations", "install");

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output.TrimEnd())
            .Should().Equal(
                "claude: installed shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "copilot: installed shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "codex: installed shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "Restart Claude Code to load the skills.",
                "Restart GitHub Copilot CLI to load the skills.",
                "Start a new Codex session to load the skills.");
    }

    [Fact]
    public async Task IntegrationsUpdate_SeveralAgentsInstalledAndOneNot_UpdatesInstalledOnesInRegistrationOrder()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        _copilot.AttachTo(_harness).WithInstalledPlugin("v0.7.0");
        _codex.AttachTo(_harness);

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
                { "agent": "claude", "pluginId": "shade-kit@creativecoders-shade-kit", "outcome": "current",
                  "previousKitVersion": "0.8.0", "kitVersion": "0.8.0", "ref": "v0.8.0", "enabled": true },
                { "agent": "copilot", "pluginId": "shade-kit@creativecoders-shade-kit", "outcome": "updated",
                  "previousKitVersion": "0.7.0", "kitVersion": "0.8.0", "ref": "v0.8.0", "enabled": true }
              ]
            }
            """);

        _copilot.Calls
            .Should().Equal(
                "plugin list --json",
                "plugin marketplace list --json",
                "plugin uninstall shade-kit@creativecoders-shade-kit",
                "plugin marketplace remove creativecoders-shade-kit",
                "plugin marketplace add CreativeCodersTeam/shade-kit#v0.8.0",
                "plugin install shade-kit@creativecoders-shade-kit",
                "plugin list --json");

        _copilot.Marketplace
            .Should().Be(FakeCopilotMarketplace.ForGitHub(FakeCopilot.Repo, "v0.8.0"));

        _codex.Calls
            .Should().Equal("plugin list --json");
    }

    [Fact]
    public async Task IntegrationsUpdate_PlainModeSeveralAgents_WritesRestartNotesOnlyForChangedAgents()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        _copilot.AttachTo(_harness);
        _codex.AttachTo(_harness).WithInstalledPlugin("v0.8.0");

        // Act
        var (exitCode, output, _) = await _harness.RunAsync("integrations", "update");

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output.TrimEnd())
            .Should().Equal(
                "claude: already current shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "codex: updated shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "Start a new Codex session to load the skills.");
    }

    [Fact]
    public async Task IntegrationsUpdate_CopilotMarketplaceKept_RefreshesMarketplaceAndUpdatesPlugin()
    {
        // Arrange
        _copilot.AttachTo(_harness).WithInstalledPlugin("v0.8.0");

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("integrations")[0].GetProperty("outcome").GetString()
            .Should().Be("updated", "copilot reports no state that tells an unchanged plugin from a refreshed one");

        _copilot.Calls
            .Should().Equal(
                "plugin list --json",
                "plugin marketplace list --json",
                "plugin marketplace update creativecoders-shade-kit",
                "plugin update shade-kit@creativecoders-shade-kit",
                "plugin list --json");
    }

    [Fact]
    public async Task IntegrationsUpdate_CodexMarketplaceWithSameRef_UpgradesMarketplaceAndAddsPluginAgain()
    {
        // Arrange
        _codex.AttachTo(_harness).WithInstalledPlugin("v0.8.0");

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("integrations")[0].ShouldBeJson(
            """
            { "agent": "codex", "pluginId": "shade-kit@creativecoders-shade-kit", "outcome": "updated",
              "previousKitVersion": "0.8.0", "kitVersion": "0.8.0", "ref": "v0.8.0", "enabled": true }
            """);

        _codex.Calls
            .Should().Equal(
                "plugin list --json",
                "plugin marketplace list --json",
                "plugin marketplace add CreativeCodersTeam/shade-kit --ref v0.8.0 --json",
                "plugin marketplace upgrade creativecoders-shade-kit --json",
                "plugin add shade-kit@creativecoders-shade-kit --json",
                "plugin list --json");
    }

    [Fact]
    public async Task IntegrationsUpdate_CodexMarketplaceWithOtherRef_SwitchesMarketplaceAndAddsPlugin()
    {
        // Arrange
        _codex.AttachTo(_harness).WithInstalledPlugin("v0.7.0");

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("integrations")[0].ShouldBeJson(
            """
            { "agent": "codex", "pluginId": "shade-kit@creativecoders-shade-kit", "outcome": "updated",
              "previousKitVersion": null, "kitVersion": "0.8.0", "ref": "v0.8.0", "enabled": true }
            """);

        _codex.Calls
            .Should().Equal(
                "plugin list --json",
                "plugin marketplace list --json",
                "plugin marketplace add CreativeCodersTeam/shade-kit --ref v0.8.0 --json",
                "plugin remove shade-kit@creativecoders-shade-kit --json",
                "plugin marketplace remove creativecoders-shade-kit --json",
                "plugin marketplace add CreativeCodersTeam/shade-kit --ref v0.8.0 --json",
                "plugin add shade-kit@creativecoders-shade-kit --json",
                "plugin list --json");

        _codex.Marketplace
            .Should().Be(FakeCodexMarketplace.ForGitHub(FakeCodex.Repo, "v0.8.0"));
    }

    [Fact]
    public async Task IntegrationsInstall_CodexDirectoryOverrideKept_AddsPluginAgainWithoutMarketplaceUpgrade()
    {
        // Arrange
        _codex.AttachTo(_harness);
        var sourcePath = _harness.TempDirectory.CreateDirectory("kit");
        var previousValue = Environment.GetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName);

        (int ExitCode, JsonElement Root) firstRun;
        (int ExitCode, JsonElement Root) secondRun;

        // Act
        Environment.SetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName, sourcePath);

        try
        {
            firstRun = await _harness.RunJsonAsync("integrations", "install", "--agent", "codex", "--json");
            secondRun = await _harness.RunJsonAsync("integrations", "install", "--agent", "codex", "--json");
        }
        finally
        {
            Environment.SetEnvironmentVariable(IntegrationService.MarketplaceSourceVariableName, previousValue);
        }

        // Assert
        firstRun.ExitCode
            .Should().Be(0);

        secondRun.ExitCode
            .Should().Be(0);

        secondRun.Root.GetProperty("data").GetProperty("integrations")[0].GetProperty("outcome").GetString()
            .Should().Be("updated");

        _codex.Calls
            .Should().Contain($"plugin marketplace add {sourcePath} --json")
            .And.NotContain(x => x.StartsWith("plugin marketplace upgrade", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IntegrationsInstall_SecondAgentFailsAfterFirstSucceeded_Exits52NamingCompletedIntegration()
    {
        // Arrange
        _claude.AttachTo(_harness);
        _copilot.AttachTo(_harness).Fail = arguments => arguments is ["plugin", "install", ..]
            ? new ExternalProcessResult(1, string.Empty, "Error: network unreachable\n")
            : null;

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentCommandFailed, ShadeKitCliErrorCodes.AgentCommandFailed);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be("'copilot plugin install shade-kit@creativecoders-shade-kit' failed: copilot exited with the " +
                         "exit code 1: Error: network unreachable. Fix the cause, make sure GitHub Copilot CLI is up " +
                         "to date, then run the command again. Before the agent 'copilot' failed, these " +
                         "integrations were done: claude: installed shade-kit@creativecoders-shade-kit 0.8.0 " +
                         "(v0.8.0).");

        _claude.IsPluginInstalled
            .Should().BeTrue();

        _codex.Calls
            .Should().BeEmpty();
    }

    [Fact]
    public async Task IntegrationsUpdate_CodexFailsAfterSwitchStartedAndCopilotSucceeded_AddsRepairHint()
    {
        // Arrange
        _copilot.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        _codex.AttachTo(_harness).WithInstalledPlugin("v0.7.0").Fail = arguments => arguments is
            ["plugin", "marketplace", "remove", ..]
            ? new ExternalProcessResult(1, string.Empty, "Error: permission denied\n")
            : null;

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.AgentCommandFailed, ShadeKitCliErrorCodes.AgentCommandFailed);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be("'codex plugin marketplace remove creativecoders-shade-kit --json' failed: codex exited with " +
                         "the exit code 1: Error: permission denied. Fix the cause, make sure OpenAI Codex CLI is " +
                         "up to date, then run the command again. The plugin marketplace of SHADE kit was already " +
                         "switched, so the integration may be incomplete; 'shade-kit integrations install' repairs " +
                         "it. Codex counts a failed check of the marketplace as another source, so the switch may " +
                         "also have followed a network, proxy, or login failure; restore the connection first. " +
                         "Before the agent 'codex' failed, these integrations were done: copilot: updated " +
                         "shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0).");
    }

    [Theory]
    [InlineData("copilot", "someone/fork#v0.8.0", CopilotResolveHint)]
    [InlineData("codex", "someone/fork", CodexResolveHint)]
    public async Task IntegrationsInstall_MarketplaceWithOtherRepo_Exits53NamingResolveCommandsOfAgent(string agent,
        string expectedExistingSource, string expectedResolveHint)
    {
        // Arrange
        _copilot.AttachTo(_harness).Marketplace = FakeCopilotMarketplace.ForGitHub("someone/fork", "v0.8.0");
        _codex.AttachTo(_harness).Marketplace = FakeCodexMarketplace.ForGitHub("someone/fork", "v0.8.0");

        // Act
        var run = await _harness.RunJsonAsync("integrations", "install", "--agent", agent, "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.IntegrationConflict, ShadeKitCliErrorCodes.IntegrationConflict);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be($"The plugin marketplace 'creativecoders-shade-kit' of the agent '{agent}' already exists " +
                         $"with the source '{expectedExistingSource}', but SHADE kit needs " +
                         $"'CreativeCodersTeam/shade-kit#v0.8.0'. Nothing was changed. Run {expectedResolveHint} " +
                         "to remove it, then run the command again.");

        _copilot.Calls.Concat(_codex.Calls)
            .Should().OnlyContain(x => x.StartsWith("plugin list", StringComparison.Ordinal) ||
                                       x.StartsWith("plugin marketplace list", StringComparison.Ordinal));

        _copilot.Marketplace
            .Should().Be(FakeCopilotMarketplace.ForGitHub("someone/fork", "v0.8.0"));

        _codex.Marketplace
            .Should().Be(FakeCodexMarketplace.ForGitHub("someone/fork", "v0.8.0"));
    }

    [Theory]
    [InlineData("copilot", "someone/fork#v0.8.0", CopilotResolveHint)]
    [InlineData("codex", "someone/fork", CodexResolveHint)]
    public async Task IntegrationsUpdate_MarketplaceWithOtherRepo_Exits53NamingResolveCommandsAndChangesNothing(
        string agent, string expectedExistingSource, string expectedResolveHint)
    {
        // Arrange
        if (agent == "copilot")
        {
            _copilot.AttachTo(_harness).WithInstalledPlugin("v0.8.0").Marketplace =
                FakeCopilotMarketplace.ForGitHub("someone/fork", "v0.8.0");
        }
        else
        {
            _codex.AttachTo(_harness).WithInstalledPlugin("v0.8.0").Marketplace =
                FakeCodexMarketplace.ForGitHub("someone/fork", "v0.8.0");
        }

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.IntegrationConflict, ShadeKitCliErrorCodes.IntegrationConflict);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be($"The plugin marketplace 'creativecoders-shade-kit' of the agent '{agent}' already exists " +
                         $"with the source '{expectedExistingSource}', but SHADE kit needs " +
                         $"'CreativeCodersTeam/shade-kit#v0.8.0'. Nothing was changed. Run {expectedResolveHint} " +
                         "to remove it, then run the command again.");

        _copilot.Calls.Concat(_codex.Calls)
            .Should().OnlyContain(x => x.StartsWith("plugin list", StringComparison.Ordinal) ||
                                       x.StartsWith("plugin marketplace list", StringComparison.Ordinal));

        (agent == "copilot" ? _copilot.IsPluginInstalled : _codex.IsPluginInstalled)
            .Should().BeTrue();
    }

    [Fact]
    public async Task IntegrationsUpdate_PrereleaseEngineWithThreeAgents_UpdatesAllFromMainWithOneWarning()
    {
        // Arrange
        _engineVersion = "0.9.0-beta.1";
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        _copilot.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        _codex.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        _harness.RemoteTags = [];
        var gitCalls = new List<string>();
        _harness.OnGitCall = x => gitCalls.Add(string.Join(' ', x));

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("warnings").EnumerateArray().Select(x => x.GetProperty("code").GetString())
            .Should().Equal(ShadeKitCliWarningCodes.IntegrationPrereleaseRef);

        run.Root.GetProperty("data").GetProperty("integrations").EnumerateArray()
            .Select(x => (x.GetProperty("agent").GetString(), x.GetProperty("ref").GetString()))
            .Should().Equal(("claude", "main"), ("copilot", "main"), ("codex", "main"));

        gitCalls
            .Should().NotContain(x => x.Contains("ls-remote", StringComparison.Ordinal));

        _copilot.Calls
            .Should().Contain("plugin marketplace add CreativeCodersTeam/shade-kit#main");

        _codex.Calls
            .Should().Contain("plugin marketplace add CreativeCodersTeam/shade-kit --ref main --json");
    }

    [Fact]
    public async Task IntegrationsInstall_PlainModeSecondAgentFails_Exits52AndWritesMessageNamingCompletedIntegration()
    {
        // Arrange
        _claude.AttachTo(_harness);
        _codex.AttachTo(_harness).Fail = arguments => arguments is ["plugin", "add", ..]
            ? new ExternalProcessResult(1, string.Empty, "Error: disk full\n")
            : null;

        // Act
        var (exitCode, output, error) = await _harness.RunAsync("integrations", "install");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.AgentCommandFailed);

        // The CLI host writes the message of an aborted command; the results of the first agent are not written.
        output
            .Should().NotContain("Restart");

        FoldWhitespace(output + error)
            .Should().Contain("'codex plugin add shade-kit@creativecoders-shade-kit --json' failed: codex exited " +
                              "with the exit code 1: Error: disk full.")
            .And.Contain("Before the agent 'codex' failed, these integrations were done: claude: installed " +
                         "shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0).");
    }

    [Fact]
    public async Task IntegrationsUpdate_CopilotPluginDisabled_KeepsItDisabledAndSaysHowToEnableIt()
    {
        // Arrange
        _copilot.AttachTo(_harness).WithInstalledPlugin("v0.8.0", enabled: false);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");
        var (_, output, _) = await _harness.RunAsync("integrations", "update");

        // Assert
        run.Root.GetProperty("data").GetProperty("integrations")[0].GetProperty("enabled").GetBoolean()
            .Should().BeFalse();

        FoldWhitespace(output)
            .Should().Be("copilot: updated shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0) The plugin is " +
                         "disabled in GitHub Copilot CLI. Run 'copilot plugin enable " +
                         "shade-kit@creativecoders-shade-kit' to enable it. Restart GitHub Copilot CLI to load the " +
                         "skills. ");
    }

    [Fact]
    public async Task IntegrationsUpdate_CodexPluginDisabled_ReportsItEnabledBecausePluginAddEnablesIt()
    {
        // Arrange
        _codex.AttachTo(_harness).WithInstalledPlugin("v0.8.0", enabled: false);

        // Act
        var run = await _harness.RunJsonAsync("integrations", "update", "--json");

        // Assert
        run.Root.GetProperty("data").GetProperty("integrations")[0].GetProperty("enabled").GetBoolean()
            .Should().BeTrue();

        _codex.IsPluginEnabled
            .Should().BeTrue();
    }

    [Fact]
    public async Task Version_ThreeAgentsWithOneMissing_ListsInstalledIntegrationsWithoutWarning()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        _codex.AttachTo(_harness).WithInstalledPlugin("v0.8.0");

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
                "ref": "v0.8.0", "enabled": true },
              { "agent": "codex", "pluginId": "shade-kit@creativecoders-shade-kit", "kitVersion": null,
                "ref": null, "enabled": true } ]
            """);

        A.CallTo(_harness.CopilotCommandRunner)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Version_ThreeAgentsWithOneFailing_ListsOthersWithAgentUnavailableWarning()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        _copilot.AttachTo(_harness).WithInstalledPlugin("v0.8.0").Fail = _ =>
            new ExternalProcessResult(1, string.Empty, "copilot crashed\n");
        _codex.AttachTo(_harness).WithInstalledPlugin("v0.8.0");

        // Act
        var run = await _harness.RunJsonAsync("version", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("integrations").EnumerateArray()
            .Select(x => x.GetProperty("agent").GetString())
            .Should().Equal("claude", "codex");

        run.Root.GetProperty("warnings").EnumerateArray().Select(x => x.GetProperty("code").GetString())
            .Should().Equal(ShadeKitCliWarningCodes.AgentUnavailable);

        run.Root.GetProperty("warnings")[0].GetProperty("message").GetString()
            .Should().StartWith("The SHADE kit integration of the agent 'copilot' cannot be read, so it is not " +
                                "listed:")
            .And.Contain("copilot crashed");
    }

    [Fact]
    public async Task Version_PlainModeThreeAgents_ListsEachWithoutNullValuesAndMarksDisabledOnes()
    {
        // Arrange
        _claude.AttachTo(_harness).WithInstalledPlugin("v0.8.0");
        _copilot.AttachTo(_harness).WithInstalledPlugin("v0.8.0", enabled: false);
        _codex.AttachTo(_harness).WithInstalledPlugin("v0.8.0", enabled: false);

        // Act
        var (exitCode, output, _) = await _harness.RunAsync("version");

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output.TrimEnd())
            .Should().Equal(
                "shade-kit 0.8.0",
                "CLI contract 1 (supported: 1 to 1)",
                "claude: shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "copilot: shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0) (disabled)",
                "codex: shade-kit@creativecoders-shade-kit (disabled)");
    }

    [Fact]
    [Trait("Requirement", "FP07-AC5")]
    public async Task IntegrationsInstallAndUpdate_CopilotAndCodexInGitRepository_CreateOrChangeNoFileInIt()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync();
        _copilot.AttachTo(_harness).WithInstalledPlugin("v0.7.0");
        _codex.AttachTo(_harness).WithInstalledPlugin("v0.7.0");
        var snapshot = _harness.TempDirectory.TakeSnapshot();

        // Act
        await _harness.RunAllOkAsync(["integrations", "update"], ["integrations", "install"],
            ["integrations", "install", "--agent", "copilot"], ["integrations", "install", "--agent", "codex"]);

        _engineVersion = "0.9.0";

        (await _harness.RunAsync("integrations", "update")).ExitCode
            .Should().Be(0);

        (await _harness.RunAsync("integrations", "install")).ExitCode
            .Should().Be(0);

        // Assert
        _harness.TempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _copilot.Calls
            .Should().Contain("plugin marketplace add CreativeCodersTeam/shade-kit#v0.9.0");

        _codex.Calls
            .Should().Contain("plugin marketplace add CreativeCodersTeam/shade-kit --ref v0.9.0 --json");

        _copilot.WorkingDirectories.Concat(_codex.WorkingDirectories)
            .Should().NotBeEmpty()
            .And.OnlyContain(x => !x.StartsWith(_harness.TempDirectory.RootPath, StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
