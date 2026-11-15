using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Exceptions;

public class IntegrationExceptionTests
{
    private static readonly MarketplaceSource ReleaseSource =
        MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit", "v0.8.0");

    [Fact]
    public void IntegrationSwitchCanceledException_CanceledInnerException_TakesTokenAndNamesAgent()
    {
        // Arrange
        var token = new CancellationToken(true);
        var innerException = new OperationCanceledException(token);

        // Act
        var exception = new IntegrationSwitchCanceledException("claude", innerException);

        // Assert
        exception.AgentId
            .Should().Be("claude");

        exception.CancellationToken
            .Should().Be(token);

        exception.InnerException
            .Should().BeSameAs(innerException);

        exception.Message
            .Should().Be("The SHADE kit integration of the agent 'claude' was canceled while its plugin marketplace " +
                         "was switched, so it may be incomplete.");
    }

    [Fact]
    public void IntegrationSwitchCanceledException_NullInnerException_UsesNoneToken()
    {
        // Act
        var exception = new IntegrationSwitchCanceledException("claude", null!);

        // Assert
        exception.CancellationToken
            .Should().Be(CancellationToken.None);
    }

    [Fact]
    public void IntegrationRunFailedException_CompletedAgents_MessageNamesFailedAndCompletedAgentsAndCause()
    {
        // Arrange
        var innerException = new InvalidOperationException("claude failed.");

        // Act
        var exception = new IntegrationRunFailedException("copilot", ReleaseSource,
            [CreateResult("claude"), CreateResult("codex")], innerException);

        // Assert
        exception.Message
            .Should().Be("The SHADE kit integration of the agent 'copilot' failed after the integrations of " +
                         "'claude', 'codex' were done: claude failed.");

        exception.FailedAgentId
            .Should().Be("copilot");

        exception.MarketplaceSource
            .Should().Be(ReleaseSource);

        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void IntegrationRunFailedException_CallerChangesListAfterwards_KeepsCopy()
    {
        // Arrange
        var completed = new List<IntegrationResult> { CreateResult("claude") };

        // Act
        var exception = new IntegrationRunFailedException("copilot", ReleaseSource, completed,
            new InvalidOperationException("failed."));
        completed.Add(CreateResult("codex"));

        // Assert
        exception.CompletedIntegrations
            .Should().Equal(CreateResult("claude"));
    }

    [Fact]
    public void IntegrationRunFailedException_NullList_KeepsEmptyList()
    {
        // Act
        var exception = new IntegrationRunFailedException("copilot", ReleaseSource, null!,
            new InvalidOperationException("failed."));

        // Assert
        exception.CompletedIntegrations
            .Should().BeEmpty();
    }

    private static IntegrationResult CreateResult(string agentId)
    {
        return new IntegrationResult(agentId, "shade-kit", IntegrationOutcome.Installed, null, "0.8.0", "v0.8.0",
            true);
    }
}
