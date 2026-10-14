using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands;

public class JsonCommandOptionsTests
{
    [Fact]
    public void Parse_JsonSwitch_SetsJsonToTrue()
    {
        // Act
        var options = (JsonCommandOptions)new OptionParser(typeof(JsonCommandOptions)).Parse(["--json"]);

        // Assert
        options.Json
            .Should().BeTrue();
    }

    [Fact]
    public void Parse_NoArguments_LeavesJsonFalse()
    {
        // Act
        var options = (JsonCommandOptions)new OptionParser(typeof(JsonCommandOptions)).Parse([]);

        // Assert
        options.Json
            .Should().BeFalse();
    }

    [Fact]
    public void Parse_DerivedOptionsWithOwnOption_BindsJsonAndOwnOption()
    {
        // Act
        var options = (DerivedOptions)new OptionParser(typeof(DerivedOptions)).Parse(["--name", "auth", "--json"]);

        // Assert
        options.Json
            .Should().BeTrue();

        options.Name
            .Should().Be("auth");
    }

    public sealed class DerivedOptions : JsonCommandOptions
    {
        [OptionParameter("name")]
        public string? Name { get; set; }
    }
}
