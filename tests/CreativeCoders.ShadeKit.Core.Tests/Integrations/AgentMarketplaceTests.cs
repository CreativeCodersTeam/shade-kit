using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations;

public class AgentMarketplaceTests
{
    [Theory]
    [InlineData("v0.8.0", "0.8.0")]
    [InlineData("v1.2.3-beta.1", "1.2.3-beta.1")]
    [InlineData("main", null)]
    [InlineData("0.8.0", null)]
    [InlineData("v0.8", null)]
    [InlineData(null, null)]
    public void GitHubRefAndKitVersion_GitHubSource_AreTakenFromRef(string? @ref, string? expectedKitVersion)
    {
        // Arrange
        var marketplace = new AgentMarketplace("creativecoders-shade-kit", AgentMarketplaceSourceKind.GitHub,
            "github", "CreativeCodersTeam/shade-kit", @ref, null);

        // Act
        var gitHubRef = marketplace.GitHubRef;
        var kitVersion = marketplace.KitVersion;

        // Assert
        gitHubRef
            .Should().Be(@ref);

        kitVersion
            .Should().Be(expectedKitVersion);
    }

    [Theory]
    [InlineData(AgentMarketplaceSourceKind.Directory)]
    [InlineData(AgentMarketplaceSourceKind.Other)]
    public void GitHubRefAndKitVersion_OtherSourceKindWithRef_AreNull(AgentMarketplaceSourceKind sourceKind)
    {
        // Arrange
        var marketplace = new AgentMarketplace("creativecoders-shade-kit", sourceKind, "git",
            "CreativeCodersTeam/shade-kit", "v0.8.0", "/kit");

        // Act
        var gitHubRef = marketplace.GitHubRef;
        var kitVersion = marketplace.KitVersion;

        // Assert
        gitHubRef
            .Should().BeNull("only the ref of a GitHub source names a kit version");

        kitVersion
            .Should().BeNull();
    }
}
