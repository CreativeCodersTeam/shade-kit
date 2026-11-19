using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Claude;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Claude;

public class ClaudeMarketplaceTests
{
    [Theory]
    [InlineData("github", AgentMarketplaceSourceKind.GitHub)]
    [InlineData("directory", AgentMarketplaceSourceKind.Directory)]
    [InlineData("GitHub", AgentMarketplaceSourceKind.Other)]
    [InlineData("url", AgentMarketplaceSourceKind.Other)]
    [InlineData(null, AgentMarketplaceSourceKind.Other)]
    public void ToAgentMarketplace_SourceKind_MapsKindAndKeepsValues(string? sourceKind,
        AgentMarketplaceSourceKind expectedSourceKind)
    {
        // Arrange
        var marketplace = new ClaudeMarketplace("creativecoders-shade-kit", sourceKind, "owner/repo", "v0.8.0",
            "/kit");

        // Act
        var agentMarketplace = marketplace.ToAgentMarketplace();

        // Assert
        agentMarketplace
            .Should().Be(new AgentMarketplace("creativecoders-shade-kit", expectedSourceKind, sourceKind,
                "owner/repo", "v0.8.0", "/kit"));
    }
}
