using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Processes;

public class PositionalArgumentTests
{
    private const string NullValue = null!;

    [Theory]
    [InlineData("shade-kit")]
    [InlineData("shade-kit@creativecoders-shade-kit")]
    [InlineData("owner/repo#v1.2.3")]
    [InlineData("a-b")]
    [InlineData(" -x")]
    public void Ensure_ValueNotStartingWithDash_DoesNotThrow(string value)
    {
        // Act
        var act = () => PositionalArgument.Ensure(value, "claude", "pluginId");

        // Assert
        act
            .Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void Ensure_EmptyOrWhitespaceValue_ThrowsArgumentExceptionNamingCallerParameter(string value)
    {
        // Act
        var act = () => PositionalArgument.Ensure(value, "claude", "source");

        // Assert
        act
            .Should().ThrowExactly<ArgumentException>()
            .WithParameterName("source");
    }

    [Fact]
    public void Ensure_NullValue_ThrowsArgumentExceptionNamingCallerParameter()
    {
        // Act
        var act = () => PositionalArgument.Ensure(NullValue, "codex", "name");

        // Assert
        act
            .Should().ThrowExactly<ArgumentException>()
            .WithParameterName("name");
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("copilot")]
    [InlineData("codex")]
    public void Ensure_ValueStartsWithDash_ThrowsArgumentExceptionWithToolNameInMessage(string toolName)
    {
        // Act
        var act = () => PositionalArgument.Ensure("-x", toolName, "pluginId");

        // Assert
        act
            .Should().ThrowExactly<ArgumentException>()
            .WithParameterName("pluginId")
            .Which.Message.Should().Be(
                $"The value '-x' starts with '-', so {toolName} would read it as an option. (Parameter 'pluginId')");
    }

    [Theory]
    [InlineData("-")]
    [InlineData("--json")]
    [InlineData("-x y")]
    public void Ensure_VariousValuesStartingWithDash_ThrowsArgumentExceptionNamingValue(string value)
    {
        // Act
        var act = () => PositionalArgument.Ensure(value, "copilot", "source");

        // Assert
        act
            .Should().ThrowExactly<ArgumentException>()
            .WithParameterName("source")
            .WithMessage($"The value '{value}' starts with '-', so copilot would read it as an option.*");
    }
}
