using System.Reflection;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Version;
using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Version;

public class VersionCommandOptionsTests
{
    [Fact]
    public void Options_ExpectOption_DoesNotExist()
    {
        // Act
        var longNames = typeof(VersionCommandOptions).GetProperties()
            .Select(x => x.GetCustomAttribute<OptionParameterAttribute>())
            .OfType<OptionParameterAttribute>()
            .Select(x => x.LongName);

        // Assert
        longNames
            .Should().NotContain("expect");
    }

    [Fact]
    public void Options_ExpectContract_HasConverterAndLongName()
    {
        // Act
        var attribute = typeof(VersionCommandOptions)
            .GetProperty(nameof(VersionCommandOptions.ExpectContract))!
            .GetCustomAttribute<OptionParameterAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.LongName
            .Should().Be("expect-contract");

        attribute.Converter
            .Should().Be<ExpectedContractOptionConverter>();
    }

    [Theory]
    [InlineData("--expect-contract")]
    [InlineData("--json", "--expect-contract")]
    [InlineData("--expect-contract", "--json")]
    public void Parse_ExpectContractWithoutValue_ThrowsExpectedContractInvalidExceptionWithoutValue(
        params string[] args)
    {
        // Arrange
        var parser = new OptionParser(typeof(VersionCommandOptions));

        // Act
        var act = () => parser.Parse(args);

        // Assert
        act
            .Should().Throw<ExpectedContractInvalidException>()
            .Which.Value.Should().BeNull();
    }

    [Fact]
    public void Parse_NoExpectContract_LeavesExpectContractNull()
    {
        // Act
        var options = (VersionCommandOptions)new OptionParser(typeof(VersionCommandOptions)).Parse(["--json"]);

        // Assert
        options.ExpectContract
            .Should().BeNull();

        options.Json
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("999", 999)]
    public void Parse_ExpectContractWithValue_SetsExpectContractToNumber(string value, int expected)
    {
        // Act
        var options = (VersionCommandOptions)new OptionParser(typeof(VersionCommandOptions))
            .Parse(["--expect-contract", value, "--json"]);

        // Assert
        options.ExpectContract
            .Should().Be(expected);

        options.Json
            .Should().BeTrue();
    }

    [Fact]
    public void Parse_ExpectContractNotANumber_ThrowsExpectedContractInvalidExceptionWithValue()
    {
        // Arrange
        var parser = new OptionParser(typeof(VersionCommandOptions));

        // Act
        var act = () => parser.Parse(["--expect-contract", "abc"]);

        // Assert
        act
            .Should().Throw<ExpectedContractInvalidException>()
            .Which.Value.Should().Be("abc");
    }
}
