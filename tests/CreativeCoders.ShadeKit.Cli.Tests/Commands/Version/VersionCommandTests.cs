using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Version;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Version;

public class VersionCommandTests
{
    [Fact]
    public async Task ExecuteAsync_PlainMode_WritesToolNameAndEngineVersion()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3");

        // Act
        await command.ExecuteAsync(new VersionCommandOptions());

        // Assert
        console.Output.TrimEnd()
            .Should().Be("shade-kit 1.2.3");

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC7")]
    [Trait("Requirement", "FP01-AC12")]
    public async Task ExecuteAsync_JsonMode_WritesEnvelopeWithEngineVersionAndOneFormatVersionPerFileType()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3");

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.EnumerateObject().Select(x => x.Name)
            .Should().Equal("ok", "data", "warnings", "error");

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        root.GetProperty("data").GetProperty("engineVersion").GetString()
            .Should().Be("1.2.3");

        root.GetProperty("data").GetProperty("formats").EnumerateObject()
            .Select(x => (x.Name, x.Value.GetInt32()))
            .Should().Equal(
                ("config", 1),
                ("gates", 1),
                ("feature", 1),
                ("current-feature", 1),
                ("templates-lock", 1),
                ("integrations", 1));

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        root.GetProperty("error").ValueKind
            .Should().Be(JsonValueKind.Null);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC7")]
    public async Task ExecuteAsync_JsonModeWithRaisedFormatVersion_ReportsCurrentVersionOfFileType()
    {
        // Arrange
        var registry = new FormatRegistry(
            [new FormatDefinition("config", new Dictionary<int, string> { [1] = "0.1.0", [2] = "0.2.0" },
                [A.Fake<IFormatMigration>(x => x.ConfigureFake(m =>
                {
                    A.CallTo(() => m.FormatId).Returns("config");
                    A.CallTo(() => m.FromVersion).Returns(1);
                }))])]);

        using var jsonOutput = new StringWriter();
        var command = new VersionCommand(new TestConsole(), CreateEngineVersionProvider("1.2.3"), registry,
            new CliJsonWriter(jsonOutput));

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("data").GetProperty("formats").GetProperty("config").GetInt32()
            .Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithoutFileTypes_WritesEmptyFormatsObject()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = new VersionCommand(new TestConsole(), CreateEngineVersionProvider("1.2.3"),
            new FormatRegistry([]), new CliJsonWriter(jsonOutput));

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("data").GetProperty("formats").EnumerateObject()
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_AnyMode_ReturnsSuccess(bool json)
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), TextWriter.Null, "1.2.3");

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { Json = json });

        // Assert
        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), TextWriter.Null, "1.2.3");

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Fact]
    public void VersionCommandOptions_Type_DerivesFromJsonCommandOptions()
    {
        // Assert
        typeof(VersionCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    [Fact]
    public void Ctor_NullConsole_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(null!, CreateEngineVersionProvider("1.0.0"), FormatRegistry.CreateDefault(),
            A.Fake<ICliJsonWriter>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ansiConsole");
    }

    [Fact]
    public void Ctor_NullEngineVersionProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(new TestConsole(), null!, FormatRegistry.CreateDefault(),
            A.Fake<ICliJsonWriter>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("engineVersionProvider");
    }

    [Fact]
    public void Ctor_NullFormatRegistry_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(new TestConsole(), CreateEngineVersionProvider("1.0.0"), null!,
            A.Fake<ICliJsonWriter>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formatRegistry");
    }

    [Fact]
    public void Ctor_NullJsonWriter_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(new TestConsole(), CreateEngineVersionProvider("1.0.0"),
            FormatRegistry.CreateDefault(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jsonWriter");
    }

    [Fact]
    public void VersionCommand_CliCommandAttribute_IsRegisteredAsTopLevelVersionCommand()
    {
        // Act
        var attribute = typeof(VersionCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("version");
    }

    private static VersionCommand CreateCommand(IAnsiConsole console, TextWriter jsonOutput, string engineVersion)
    {
        return new VersionCommand(console, CreateEngineVersionProvider(engineVersion), FormatRegistry.CreateDefault(),
            new CliJsonWriter(jsonOutput));
    }

    private static IEngineVersionProvider CreateEngineVersionProvider(string version)
    {
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).Returns(version);

        return engineVersionProvider;
    }
}
