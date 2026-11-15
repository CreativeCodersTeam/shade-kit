using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Codex;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Codex;

public class CodexMarketplaceTests
{
    private const string Name = "creativecoders-shade-kit";

    private const string Root = "/home/me/.codex/.tmp/marketplaces/creativecoders-shade-kit";

    [Theory]
    [InlineData("https://github.com/CreativeCodersTeam/shade-kit.git", "CreativeCodersTeam/shade-kit")]
    [InlineData("https://github.com/o/r.git", "o/r")]
    [InlineData("https://github.com/my_org.x/repo-name_1.git", "my_org.x/repo-name_1")]
    public void ToAgentMarketplace_GitSourceOfGitHubUrl_MapsToGitHubWithRepoAndWithoutRef(string source,
        string expectedRepo)
    {
        // Arrange
        var marketplace = new CodexMarketplace(Name, Root, "git", source);

        // Act
        var agentMarketplace = marketplace.ToAgentMarketplace();

        // Assert
        agentMarketplace
            .Should().Be(new AgentMarketplace(Name, AgentMarketplaceSourceKind.GitHub, "git", expectedRepo, null,
                null), "codex does not report the ref of a marketplace");
    }

    [Theory]
    [InlineData("https://github.com/o/r")]
    [InlineData("https://github.com/o/r.git/")]
    [InlineData("https://github.com/o/r/x.git")]
    [InlineData("https://GitHub.com/o/r.git")]
    [InlineData("http://github.com/o/r.git")]
    [InlineData("ssh://git@github.com/o/r.git")]
    [InlineData("git@github.com:o/r.git")]
    [InlineData("https://gitlab.com/o/r.git")]
    [InlineData("https://github.com/o/r.git\n")]
    public void ToAgentMarketplace_GitSourceOfOtherUrl_MapsToOtherWithReportedKind(string source)
    {
        // Arrange
        var marketplace = new CodexMarketplace(Name, Root, "git", source);

        // Act
        var agentMarketplace = marketplace.ToAgentMarketplace();

        // Assert
        agentMarketplace
            .Should().Be(new AgentMarketplace(Name, AgentMarketplaceSourceKind.Other, "git", null, null, null));
    }

    [Theory]
    [InlineData("/src/shade-kit", "/src/shade-kit")]
    [InlineData(@"C:\src\shade-kit", @"C:\src\shade-kit")]
    [InlineData(@"\\?\C:\src\shade-kit", @"C:\src\shade-kit")]
    [InlineData(@"\\?\UNC\server\share\kit", @"\\server\share\kit")]
    public void ToAgentMarketplace_LocalSource_MapsToDirectoryWithoutWindowsVerbatimPrefix(string source,
        string expectedPath)
    {
        // Arrange
        var marketplace = new CodexMarketplace(Name, source, "local", source);

        // Act
        var agentMarketplace = marketplace.ToAgentMarketplace();

        // Assert
        agentMarketplace
            .Should().Be(new AgentMarketplace(Name, AgentMarketplaceSourceKind.Directory, "local", null, null,
                expectedPath));
    }

    [Theory]
    [InlineData("Git", "https://github.com/o/r.git")]
    [InlineData("Local", "/src")]
    [InlineData("npm", "pkg")]
    [InlineData(null, null)]
    [InlineData("git", null)]
    [InlineData("local", null)]
    public void ToAgentMarketplace_OtherOrMissingSourceType_MapsToOtherWithReportedKind(string? sourceType,
        string? source)
    {
        // Arrange
        var marketplace = new CodexMarketplace(Name, Root, sourceType, source);

        // Act
        var agentMarketplace = marketplace.ToAgentMarketplace();

        // Assert
        agentMarketplace
            .Should().Be(new AgentMarketplace(Name, AgentMarketplaceSourceKind.Other, sourceType, null, null, null));
    }
}
