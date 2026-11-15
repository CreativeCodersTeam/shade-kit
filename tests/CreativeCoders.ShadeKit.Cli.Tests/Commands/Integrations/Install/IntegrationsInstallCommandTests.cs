using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Integrations.Install;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Integrations.Warnings;
using FakeItEasy;
using Spectre.Console.Testing;
using static CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations.IntegrationsCommandTestData;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations.Install;

public sealed class IntegrationsInstallCommandTests : IDisposable
{
    private static readonly string[] ClaudeAgentIds = ["claude"];

    private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();

    private readonly TestConsole _console = new TestConsole().Width(1000);

    private readonly IIntegrationService _integrationService = A.Fake<IIntegrationService>();

    private readonly StringWriter _jsonOutput = new StringWriter();

    public IntegrationsInstallCommandTests()
    {
        A.CallTo(() => _integrationService.InstallAsync(A<IReadOnlyCollection<string>?>._, A<CancellationToken>._))
            .Returns(CreateRun(CreateResult(IntegrationOutcome.Installed)));
    }

    [Fact]
    [Trait("Requirement", "FP07-AC2")]
    public async Task ExecuteAsync_JsonMode_WritesEnvelopeWithTargetedSourceAndResultOfEachAgent()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = await command.ExecuteAsync(new IntegrationsInstallCommandOptions { Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        _jsonOutput.ToString()
            .Should().Be(
                """
                {
                  "ok": true,
                  "data": {
                    "ref": "v0.8.0",
                    "source": "CreativeCodersTeam/shade-kit#v0.8.0",
                    "integrations": [
                      {
                        "agent": "claude",
                        "pluginId": "shade-kit@creativecoders-shade-kit",
                        "outcome": "installed",
                        "previousKitVersion": null,
                        "kitVersion": "0.8.0",
                        "ref": "v0.8.0",
                        "enabled": true
                      }
                    ]
                  },
                  "warnings": [],
                  "error": null
                }
                """.ReplaceLineEndings("\n") + "\n");

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithPrereleaseWarning_WritesWarningIntoEnvelope()
    {
        // Arrange
        A.CallTo(() => _integrationService.InstallAsync(A<IReadOnlyCollection<string>?>._, A<CancellationToken>._))
            .Returns(CreateRun([CreateResult(IntegrationOutcome.Installed)],
                [new IntegrationPrereleaseRefWarning("0.9.0-beta.1", "main")],
                MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit", "main")));

        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new IntegrationsInstallCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(_jsonOutput.ToString());

        document.RootElement.GetProperty("warnings").EnumerateArray().Select(x => x.GetProperty("code").GetString())
            .Should().Equal(ShadeKitCliWarningCodes.IntegrationPrereleaseRef);

        document.RootElement.GetProperty("data").GetProperty("ref").GetString()
            .Should().Be("main");
    }

    [Fact]
    public async Task ExecuteAsync_PlainMode_WritesOneLinePerAgentAndRestartHint()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new IntegrationsInstallCommandOptions());

        // Assert
        _console.Lines
            .Should().Equal(
                "claude: installed shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "Restart Claude Code to load the skills.");

        _jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithWarning_WritesWarningBeforeResultLines()
    {
        // Arrange
        A.CallTo(() => _integrationService.InstallAsync(A<IReadOnlyCollection<string>?>._, A<CancellationToken>._))
            .Returns(CreateRun([CreateResult(IntegrationOutcome.Current)],
                [new IntegrationPrereleaseRefWarning("0.9.0-beta.1", "main")]));

        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new IntegrationsInstallCommandOptions());

        // Assert
        _console.Lines
            .Should().HaveCount(2);

        _console.Lines[0]
            .Should().StartWith("Warning: The engine 0.9.0-beta.1 is a prerelease");

        _console.Lines[1]
            .Should().Be("claude: already current shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)");
    }

    [Fact]
    public async Task ExecuteAsync_WithAgent_InstallsForThatAgentWithCancellationTokenOfRun()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new IntegrationsInstallCommandOptions { Agent = "claude" });

        // Assert
        A.CallTo(() => _integrationService.InstallAsync(
                A<IReadOnlyCollection<string>?>.That.Matches(x => x != null && x.SequenceEqual(ClaudeAgentIds)),
                _cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAgent_InstallsForEveryDetectedAgent()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new IntegrationsInstallCommandOptions());

        // Assert
        A.CallTo(() => _integrationService.InstallAsync(null, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeNoAgentFound_WritesFailureEnvelopeAndAbortsWith51()
    {
        // Arrange
        A.CallTo(() => _integrationService.InstallAsync(A<IReadOnlyCollection<string>?>._, A<CancellationToken>._))
            .ThrowsAsync(new NoAgentFoundException(["claude"]));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new IntegrationsInstallCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.AgentNotFound);

        exception.PrintMessage
            .Should().BeFalse();

        using var document = JsonDocument.Parse(_jsonOutput.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.AgentNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeIntegrationConflict_AbortsWith53AndMessageWithoutOutput()
    {
        // Arrange
        A.CallTo(() => _integrationService.InstallAsync(A<IReadOnlyCollection<string>?>._, A<CancellationToken>._))
            .ThrowsAsync(new IntegrationConflictException("claude", "creativecoders-shade-kit", "someone/fork",
                "CreativeCodersTeam/shade-kit#v0.8.0", "claude plugin marketplace remove creativecoders-shade-kit"));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new IntegrationsInstallCommandOptions());

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.IntegrationConflict);

        exception.PrintMessage
            .Should().BeTrue();

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeCanceledDuringMarketplaceSwitch_WritesCanceledEnvelopeWithRepairHint()
    {
        // Arrange
        await _cancellationSource.CancelAsync();
        A.CallTo(() => _integrationService.InstallAsync(A<IReadOnlyCollection<string>?>._, A<CancellationToken>._))
            .ThrowsAsync(new IntegrationSwitchCanceledException("claude",
                new OperationCanceledException(_cancellationSource.Token)));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new IntegrationsInstallCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);

        using var document = JsonDocument.Parse(_jsonOutput.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.Canceled);

        document.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should().StartWith("The command was canceled")
            .And.EndWith("'shade-kit integrations install' repairs it.");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeCanceledDuringMarketplaceSwitch_AbortsWith130AndRepairHint()
    {
        // Arrange
        A.CallTo(() => _integrationService.InstallAsync(A<IReadOnlyCollection<string>?>._, A<CancellationToken>._))
            .ThrowsAsync(new IntegrationSwitchCanceledException("claude", new OperationCanceledException()));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new IntegrationsInstallCommandOptions());

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);

        exception.PrintMessage
            .Should().BeTrue();

        exception.Message
            .Should().Contain("'shade-kit integrations install' repairs it.");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeSecondAgentFailed_WritesFailureEnvelopeNamingCompletedAgents()
    {
        // Arrange
        A.CallTo(() => _integrationService.InstallAsync(A<IReadOnlyCollection<string>?>._, A<CancellationToken>._))
            .ThrowsAsync(new IntegrationRunFailedException("copilot", ReleaseSource,
                [CreateResult(IntegrationOutcome.Installed)],
                new ClaudeCommandFailedException(["plugin", "install"], 1, "claude failed.")));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new IntegrationsInstallCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.AgentCommandFailed);

        using var document = JsonDocument.Parse(_jsonOutput.ToString());

        document.RootElement.GetProperty("data").ValueKind
            .Should().Be(JsonValueKind.Null, "the failure envelope keeps its contract");

        var error = document.RootElement.GetProperty("error");

        error.EnumerateObject().Select(x => x.Name)
            .Should().Equal("code", "message");

        error.GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.AgentCommandFailed);

        error.GetProperty("message").GetString()
            .Should().EndWith("Before the agent 'copilot' failed, these integrations were done: claude: installed " +
                              "shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0).");
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Fact]
    public void Ctor_NullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new IntegrationsInstallCommand(null!, _integrationService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Ctor_NullIntegrationService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new IntegrationsInstallCommand(TestShadeKitCommandContext.Create(_console), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("integrationService");
    }

    [Fact]
    public void IntegrationsInstallCommand_CliCommandAttribute_IsRegisteredInIntegrationsGroup()
    {
        // Act
        var attribute = typeof(IntegrationsInstallCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute!.Commands
            .Should().Equal("integrations", "install");
    }

    [Fact]
    public void IntegrationsInstallCommandOptions_Type_DerivesFromJsonCommandOptions()
    {
        // Assert
        typeof(IntegrationsInstallCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    public void Dispose()
    {
        _cancellationSource.Dispose();
        _jsonOutput.Dispose();
    }

    private IntegrationsInstallCommand CreateCommand()
    {
        return new IntegrationsInstallCommand(
            TestShadeKitCommandContext.Create(_console, jsonOutput: _jsonOutput,
                cancellation: new TestCliCancellation(_cancellationSource.Token)),
            _integrationService);
    }
}
