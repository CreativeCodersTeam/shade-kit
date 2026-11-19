using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Integrations;
using CreativeCoders.ShadeKit.Core;
using CreativeCoders.ShadeKit.Core.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Testing;
using static CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations.IntegrationsCommandTestData;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations;

public class AgentPresentationsTests
{
    [Fact]
    public void All_ComparedWithRegisteredIntegrationService_HasOneEntryPerAgentInRegistrationOrder()
    {
        // Arrange
        using var serviceProvider = new ServiceCollection()
            .AddShadeKitCore()
            .BuildServiceProvider();

        // Act
        var agentIds = serviceProvider.GetRequiredService<IIntegrationService>().AgentIds;

        // Assert
        AgentPresentations.All.Select(x => x.AgentId)
            .Should().Equal(agentIds);
    }

    [Fact]
    public void All_EveryEntry_HasDisplayNameRestartTextAndAgentSpecificDisabledNote()
    {
        // Assert
        foreach (var presentation in AgentPresentations.All)
        {
            presentation.DisplayName
                .Should().NotBeNullOrWhiteSpace($"the agent '{presentation.AgentId}' needs a display name");

            presentation.RestartText
                .Should().NotBeNullOrWhiteSpace($"the agent '{presentation.AgentId}' needs a restart note");

            ToPlainText(presentation.FormatDisabledNote(PluginId))
                .Should().StartWith("The plugin is disabled in ",
                    $"the agent '{presentation.AgentId}' needs its own disabled note")
                .And.NotBe(
                    $"The integration is disabled in the agent '{presentation.AgentId}'; enable it there.");
        }
    }

    [Theory]
    [InlineData("claude", null)]
    [InlineData("copilot", null)]
    [InlineData("codex", AgentPresentations.CodexIncompleteNote)]
    public void Find_KnownAgent_ReturnsEntryWithItsIncompleteNote(string agentId, string? expectedIncompleteNote)
    {
        // Act
        var presentation = AgentPresentations.Find(agentId);

        // Assert
        presentation
            .Should().NotBeNull();

        presentation.AgentId
            .Should().Be(agentId);

        presentation.IncompleteNote
            .Should().Be(expectedIncompleteNote);
    }

    [Theory]
    [InlineData("gemini")]
    [InlineData("Claude")]
    [InlineData("")]
    [InlineData(null)]
    public void Find_UnknownOrNullAgentId_ReturnsNull(string? agentId)
    {
        // Act
        var presentation = AgentPresentations.Find(agentId);

        // Assert
        presentation
            .Should().BeNull();
    }

    private static string ToPlainText(string markup)
    {
        var console = new TestConsole().Width(1000);
        console.MarkupLine(markup);

        return console.Lines.Single();
    }
}
