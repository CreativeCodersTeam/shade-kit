using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations;
using static CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations.IntegrationsCommandTestData;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations;

public class IntegrationRunDataTests
{
    [Fact]
    public void From_NothingInstalledWithoutSource_ReturnsNullRefAndSourceAndEmptyIntegrations()
    {
        // Act
        var data = IntegrationRunData.From(CreateNothingInstalledRun());

        // Assert
        data
            .Should().BeEquivalentTo(new IntegrationRunData(null, null, []));
    }

    [Fact]
    public void From_SourceWithoutResults_ReturnsRefAndSourceAndEmptyIntegrations()
    {
        // Act
        var data = IntegrationRunData.From(CreateRun());

        // Assert
        data
            .Should().BeEquivalentTo(new IntegrationRunData("v0.8.0", "CreativeCodersTeam/shade-kit#v0.8.0", []));
    }

    [Fact]
    public void From_GitHubSourceWithoutRef_ReturnsNullRefAndRepositoryAsSource()
    {
        // Act
        var data = IntegrationRunData.From(CreateRun([], [], MarketplaceSource.ForGitHub("owner/fork")));

        // Assert
        data.Ref
            .Should().BeNull();

        data.Source
            .Should().Be("owner/fork");
    }

    [Fact]
    public void From_NullResult_ThrowsArgumentNullException()
    {
        // Act
        var act = () => IntegrationRunData.From(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("result");
    }
}
