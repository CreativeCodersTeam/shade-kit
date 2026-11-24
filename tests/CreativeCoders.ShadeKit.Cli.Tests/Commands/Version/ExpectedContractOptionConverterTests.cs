using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Version;
using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Version;

public class ExpectedContractOptionConverterTests
{
    private static readonly OptionParameterAttribute ExpectContractAttribute =
        new OptionParameterAttribute("expect-contract");

    [Fact]
    public void Convert_Null_ThrowsExpectedContractInvalidException()
    {
        // Arrange
        var converter = new ExpectedContractOptionConverter();

        // Act
        var act = () => converter.Convert(null, typeof(int?), ExpectContractAttribute);

        // Assert
        act
            .Should().Throw<ExpectedContractInvalidException>()
            .Which.Value.Should().BeNull();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("01")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1.0")]
    [InlineData("2147483648")]
    [InlineData("")]
    [InlineData("١")]
    public void Convert_InvalidText_ThrowsExpectedContractInvalidExceptionWithValue(string value)
    {
        // Arrange
        var converter = new ExpectedContractOptionConverter();

        // Act
        var act = () => converter.Convert(value, typeof(int?), ExpectContractAttribute);

        // Assert
        act
            .Should().Throw<ExpectedContractInvalidException>()
            .Which.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("42", 42)]
    [InlineData("2147483647", int.MaxValue)]
    public void Convert_ValidText_ReturnsInt(string value, int expected)
    {
        // Arrange
        var converter = new ExpectedContractOptionConverter();

        // Act
        var result = converter.Convert(value, typeof(int?), ExpectContractAttribute);

        // Assert
        result
            .Should().BeOfType<int>()
            .Which.Should().Be(expected);
    }
}
