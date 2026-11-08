using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Version;
using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Version;

public class VersionCommandOptionsTests
{
    [Theory]
    [Trait("Requirement", "FP06-1-AC5")]
    [InlineData("--expect")]
    [InlineData("--json", "--expect")]
    [InlineData("--expect", "--json")]
    public void Parse_ExpectWithoutValue_ThrowsExpectedVersionMissingException(params string[] args)
    {
        // Arrange
        var parser = new OptionParser(typeof(VersionCommandOptions));

        // Act
        var act = () => parser.Parse(args);

        // Assert
        act
            .Should().Throw<ExpectedVersionMissingException>();
    }

    [Fact]
    public void Parse_NoExpect_LeavesExpectNull()
    {
        // Act
        var options = (VersionCommandOptions)new OptionParser(typeof(VersionCommandOptions)).Parse(["--json"]);

        // Assert
        options.Expect
            .Should().BeNull();

        options.Json
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("")]
    public void Parse_ExpectWithValue_SetsExpectToValue(string value)
    {
        // Act
        var options = (VersionCommandOptions)new OptionParser(typeof(VersionCommandOptions))
            .Parse(["--expect", value, "--json"]);

        // Assert
        options.Expect
            .Should().Be(value);

        options.Json
            .Should().BeTrue();
    }
}
