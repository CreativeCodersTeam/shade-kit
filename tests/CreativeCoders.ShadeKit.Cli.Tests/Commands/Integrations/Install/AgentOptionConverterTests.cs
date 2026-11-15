using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Integrations.Install;
using CreativeCoders.ShadeKit.Core;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.SysConsole.Cli.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations.Install;

public class AgentOptionConverterTests
{
    private static readonly OptionParameterAttribute AgentAttribute = new OptionParameterAttribute("agent");

    [Theory]
    [InlineData("claude")]
    [InlineData("Claude")]
    [InlineData("CLAUDE")]
    public void Convert_SupportedIdInAnyCase_ReturnsIdOfSupportedAgent(string value)
    {
        // Arrange
        var converter = new AgentOptionConverter();

        // Act
        var result = converter.Convert(value, typeof(string), AgentAttribute);

        // Assert
        result
            .Should().Be("claude");
    }

    [Theory]
    [InlineData("copilot")]
    [InlineData("")]
    [InlineData(" claude")]
    public void Convert_UnknownId_ThrowsInvalidAgentOptionExceptionWithValueAndSupportedAgents(string value)
    {
        // Arrange
        var converter = new AgentOptionConverter();

        // Act
        var act = () => converter.Convert(value, typeof(string), AgentAttribute);

        // Assert
        var exception = act.Should().Throw<InvalidAgentOptionException>().Which;

        exception.Value
            .Should().Be(value);

        exception.SupportedAgentIds
            .Should().Equal("claude");
    }

    [Fact]
    public void Convert_NullValue_ThrowsInvalidAgentOptionExceptionWithoutValue()
    {
        // Arrange
        var converter = new AgentOptionConverter();

        // Act
        var act = () => converter.Convert(null, typeof(string), AgentAttribute);

        // Assert
        act.Should().Throw<InvalidAgentOptionException>()
            .Which.Value.Should().BeNull();
    }

    [Fact]
    public void SupportedAgentIds_ComparedWithRegisteredIntegrationService_AreEqual()
    {
        // Arrange
        using var serviceProvider = new ServiceCollection()
            .AddShadeKitCore()
            .BuildServiceProvider();

        // Act
        var agentIds = serviceProvider.GetRequiredService<IIntegrationService>().AgentIds;

        // Assert
        AgentOptionConverter.SupportedAgentIds
            .Should().Equal(agentIds);
    }
}
