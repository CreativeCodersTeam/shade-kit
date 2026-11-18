using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Integrations.Update;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using FakeItEasy;
using Spectre.Console.Testing;
using static CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations.IntegrationsCommandTestData;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations.Update;

public sealed class IntegrationsUpdateCommandTests : IDisposable
{
    private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();

    private readonly TestConsole _console = new TestConsole().Width(1000);

    private readonly IIntegrationService _integrationService = A.Fake<IIntegrationService>();

    private readonly StringWriter _jsonOutput = new StringWriter();

    [Fact]
    [Trait("Requirement", "FP07-AC3")]
    public async Task ExecuteAsync_JsonModeUpdated_WritesEnvelopeWithPreviousAndNewKitVersion()
    {
        // Arrange
        ReturnRun(CreateRun(CreateResult(IntegrationOutcome.Updated, "0.7.0")));
        var command = CreateCommand();

        // Act
        var result = await command.ExecuteAsync(new IntegrationsUpdateCommandOptions { Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(_jsonOutput.ToString());

        JsonSerializer.Serialize(document.RootElement.GetProperty("data"))
            .Should().Be(
                "{\"ref\":\"v0.8.0\",\"source\":\"CreativeCodersTeam/shade-kit#v0.8.0\",\"integrations\":[" +
                "{\"agent\":\"claude\",\"pluginId\":\"shade-kit@creativecoders-shade-kit\",\"outcome\":\"updated\"," +
                "\"previousKitVersion\":\"0.7.0\",\"kitVersion\":\"0.8.0\",\"ref\":\"v0.8.0\",\"enabled\":true}]}");

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP07-AC3")]
    public async Task ExecuteAsync_PlainModeUpdated_WritesOldAndNewVersionAndRestartHint()
    {
        // Arrange
        ReturnRun(CreateRun(CreateResult(IntegrationOutcome.Updated, "0.7.0")));
        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new IntegrationsUpdateCommandOptions());

        // Assert
        _console.Lines
            .Should().Equal(
                "claude: updated shade-kit@creativecoders-shade-kit 0.7.0 -> 0.8.0 (v0.8.0)",
                "Restart Claude Code to load the skills.");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeCurrentAndDisabled_WritesDisabledNoteWithoutRestartHint()
    {
        // Arrange
        ReturnRun(CreateRun(CreateResult(IntegrationOutcome.Current, "0.8.0", enabled: false)));
        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new IntegrationsUpdateCommandOptions());

        // Assert
        _console.Lines
            .Should().Equal(
                "claude: already current shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "  The plugin is disabled in Claude Code. Run 'claude plugin enable " +
                "shade-kit@creativecoders-shade-kit' to enable it.");
    }

    [Fact]
    [Trait("Requirement", "FP07-AC6")]
    public async Task ExecuteAsync_JsonModeNothingInstalled_WritesSuccessEnvelopeWithEmptyIntegrations()
    {
        // Arrange
        ReturnRun(CreateNothingInstalledRun());
        var command = CreateCommand();

        // Act
        var result = await command.ExecuteAsync(new IntegrationsUpdateCommandOptions { Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(_jsonOutput.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        document.RootElement.GetProperty("data").ShouldBeJson(
            """
            { "ref": null, "source": null, "integrations": [] }
            """);

        document.RootElement.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);
    }

    [Fact]
    [Trait("Requirement", "FP07-AC6")]
    public async Task ExecuteAsync_PlainModeNothingInstalled_SaysNothingIsInstalledAndHowToInstall()
    {
        // Arrange
        ReturnRun(CreateNothingInstalledRun());
        var command = CreateCommand();

        // Act
        var result = await command.ExecuteAsync(new IntegrationsUpdateCommandOptions());

        // Assert
        result.ExitCode
            .Should().Be(0);

        _console.Lines
            .Should().Equal(
                "No SHADE kit integration is installed.",
                "Run 'shade-kit integrations install' to install it.");
    }

    [Fact]
    public async Task ExecuteAsync_AnyMode_UpdatesWithCancellationTokenOfRun()
    {
        // Arrange
        ReturnRun(CreateRun());
        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new IntegrationsUpdateCommandOptions { Json = true });

        // Assert
        A.CallTo(() => _integrationService.UpdateAsync(_cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeAgentCommandFailed_WritesFailureEnvelopeAndAbortsWith52()
    {
        // Arrange
        A.CallTo(() => _integrationService.UpdateAsync(A<CancellationToken>._))
            .ThrowsAsync(new ClaudeCommandFailedException(["plugin", "list", "--json"], 1, "claude exited with 1."));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new IntegrationsUpdateCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.AgentCommandFailed);

        using var document = JsonDocument.Parse(_jsonOutput.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.AgentCommandFailed);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeSecondAgentFailedAfterSwitch_AbortsWithRepairHintAndCompletedAgents()
    {
        // Arrange
        A.CallTo(() => _integrationService.UpdateAsync(A<CancellationToken>._))
            .ThrowsAsync(new IntegrationRunFailedException("copilot", ReleaseSource,
                [CreateResult(IntegrationOutcome.Updated, "0.7.0")],
                new IntegrationIncompleteException("copilot",
                    new ClaudeCommandFailedException(["plugin", "install"], 1, "claude failed."))));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new IntegrationsUpdateCommandOptions());

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.AgentCommandFailed);

        exception.Message
            .Should().Contain("'shade-kit integrations install' repairs it.")
            .And.EndWith("Before the agent 'copilot' failed, these integrations were done: claude: updated " +
                         "shade-kit@creativecoders-shade-kit 0.7.0 -> 0.8.0 (v0.8.0).");

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeCodexIncompleteAfterSwitch_WritesFailureEnvelopeNamingNetworkCause()
    {
        // Arrange
        A.CallTo(() => _integrationService.UpdateAsync(A<CancellationToken>._))
            .ThrowsAsync(new IntegrationIncompleteException("codex",
                new CodexCommandFailedException(["plugin", "marketplace", "add"], 1, "codex failed.")));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new IntegrationsUpdateCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.AgentCommandFailed);

        using var document = JsonDocument.Parse(_jsonOutput.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.AgentCommandFailed);

        document.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should().EndWith("'shade-kit integrations install' repairs it. Codex counts a failed check of the " +
                              "marketplace as another source, so the switch may also have followed a network, " +
                              "proxy, or login failure; restore the connection first.");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeCanceledDuringMarketplaceSwitch_WritesCanceledEnvelopeWithRepairHint()
    {
        // Arrange
        A.CallTo(() => _integrationService.UpdateAsync(A<CancellationToken>._))
            .ThrowsAsync(new IntegrationSwitchCanceledException("claude", new OperationCanceledException()));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new IntegrationsUpdateCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);

        using var document = JsonDocument.Parse(_jsonOutput.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.Canceled);

        document.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should().EndWith("'shade-kit integrations install' repairs it.");
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
        var act = () => new IntegrationsUpdateCommand(null!, _integrationService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Ctor_NullIntegrationService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new IntegrationsUpdateCommand(TestShadeKitCommandContext.Create(_console), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("integrationService");
    }

    [Fact]
    public void IntegrationsUpdateCommand_CliCommandAttribute_IsRegisteredInIntegrationsGroup()
    {
        // Act
        var attribute = typeof(IntegrationsUpdateCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute!.Commands
            .Should().Equal("integrations", "update");
    }

    [Fact]
    public void IntegrationsUpdateCommandOptions_Type_DerivesFromJsonCommandOptions()
    {
        // Assert
        typeof(IntegrationsUpdateCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    public void Dispose()
    {
        _cancellationSource.Dispose();
        _jsonOutput.Dispose();
    }

    private void ReturnRun(IntegrationRunResult result)
    {
        A.CallTo(() => _integrationService.UpdateAsync(A<CancellationToken>._))
            .Returns(result);
    }

    private IntegrationsUpdateCommand CreateCommand()
    {
        return new IntegrationsUpdateCommand(
            TestShadeKitCommandContext.Create(_console, jsonOutput: _jsonOutput,
                cancellation: new TestCliCancellation(_cancellationSource.Token)),
            _integrationService);
    }
}
