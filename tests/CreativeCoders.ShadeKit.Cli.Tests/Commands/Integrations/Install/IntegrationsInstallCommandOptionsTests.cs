using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Integrations.Install;
using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations.Install;

public class IntegrationsInstallCommandOptionsTests
{
    [Theory]
    [InlineData("claude", "claude")]
    [InlineData("CLAUDE", "claude")]
    public void Parse_AgentWithSupportedId_SetsAgentToSupportedId(string value, string expectedAgent)
    {
        // Act
        var options = (IntegrationsInstallCommandOptions)new OptionParser(typeof(IntegrationsInstallCommandOptions))
            .Parse(["--agent", value, "--json"]);

        // Assert
        options.Agent
            .Should().Be(expectedAgent);

        options.Json
            .Should().BeTrue();
    }

    [Fact]
    public void Parse_NoAgent_LeavesAgentNull()
    {
        // Act
        var options = (IntegrationsInstallCommandOptions)new OptionParser(typeof(IntegrationsInstallCommandOptions))
            .Parse(["--json"]);

        // Assert
        options.Agent
            .Should().BeNull();
    }

    [Theory]
    [InlineData("--agent", "copilot")]
    [InlineData("--agent")]
    [InlineData("--agent", "--json")]
    public void Parse_AgentWithUnknownIdOrWithoutValue_ThrowsInvalidAgentOptionException(params string[] args)
    {
        // Arrange
        var parser = new OptionParser(typeof(IntegrationsInstallCommandOptions));

        // Act
        var act = () => parser.Parse(args);

        // Assert
        act
            .Should().Throw<InvalidAgentOptionException>();
    }
}
