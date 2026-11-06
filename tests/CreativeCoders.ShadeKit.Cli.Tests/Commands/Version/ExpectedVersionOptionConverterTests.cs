using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Version;
using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Version;

public class ExpectedVersionOptionConverterTests
{
    private static readonly OptionParameterAttribute ExpectAttribute = new OptionParameterAttribute("expect");

    [Fact]
    [Trait("Requirement", "FP06-1-AC5")]
    public void Convert_NullValue_ThrowsExpectedVersionMissingException()
    {
        // Arrange
        var converter = new ExpectedVersionOptionConverter();

        // Act
        var act = () => converter.Convert(null, typeof(string), ExpectAttribute);

        // Assert
        act
            .Should().Throw<ExpectedVersionMissingException>();
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("abc")]
    [InlineData("")]
    public void Convert_Value_ReturnsValueUnchanged(string value)
    {
        // Arrange
        var converter = new ExpectedVersionOptionConverter();

        // Act
        var result = converter.Convert(value, typeof(string), ExpectAttribute);

        // Assert
        result
            .Should().Be(value);
    }
}
