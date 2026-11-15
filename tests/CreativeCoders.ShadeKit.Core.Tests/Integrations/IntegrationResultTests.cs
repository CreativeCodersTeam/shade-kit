using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations;

public class IntegrationResultTests
{
    [Fact]
    public void ForShadeKitPlugin_ReleaseSource_ReturnsResultWithPluginIdAndSourceVersion()
    {
        // Arrange
        var source = MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit", "v0.8.0");

        // Act
        var result = IntegrationResult.ForShadeKitPlugin("copilot", source, IntegrationOutcome.Updated, "0.7.0",
            false);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", "shade-kit@creativecoders-shade-kit",
                IntegrationOutcome.Updated, "0.7.0", "0.8.0", "v0.8.0", false));
    }

    [Fact]
    public void ForShadeKitPlugin_BranchSource_ReturnsResultWithRefAndWithoutKitVersion()
    {
        // Arrange
        var source = MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit", "main");

        // Act
        var result = IntegrationResult.ForShadeKitPlugin("claude", source, IntegrationOutcome.Installed, null,
            true);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", "shade-kit@creativecoders-shade-kit",
                IntegrationOutcome.Installed, null, null, "main", true));
    }

    [Fact]
    public void ForShadeKitPlugin_DirectorySource_ReturnsResultWithoutKitVersionAndRef()
    {
        // Arrange
        var source = MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "shade-kit"));

        // Act
        var result = IntegrationResult.ForShadeKitPlugin("codex", source, IntegrationOutcome.Current, null, true);

        // Assert
        result.KitVersion
            .Should().BeNull();

        result.Ref
            .Should().BeNull();
    }

    [Fact]
    public void ForShadeKitPlugin_NullAgentId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => IntegrationResult.ForShadeKitPlugin(null!, MarketplaceSource.ForGitHub("owner/repo"),
            IntegrationOutcome.Installed, null, true);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agentId");
    }

    [Fact]
    public void ForShadeKitPlugin_NullSource_ThrowsArgumentNullException()
    {
        // Act
        var act = () => IntegrationResult.ForShadeKitPlugin("claude", null!, IntegrationOutcome.Installed, null,
            true);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }
}
