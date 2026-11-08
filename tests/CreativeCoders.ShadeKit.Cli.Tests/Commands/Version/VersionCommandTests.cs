using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Version;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Versions;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Version;

public class VersionCommandTests
{
    private const string OlderEngineMessage =
        "The SHADE kit engine 1.2.3 does not fit the kit skills 1.3.0: the major and minor versions must be equal. " +
        "Update the engine to 1.3, then run the skill again.";

    private const string NewerEngineMessage =
        "The SHADE kit engine 1.2.3 does not fit the kit skills 1.1.0: the major and minor versions must be equal. " +
        "Update the SHADE kit skills to 1.2 by hand, then run the skill again.";

    private const string PrereleaseWarningMessage =
        "The prerelease engine 1.2.3-beta.1 does not fit the kit skills 1.3.0 (the major and minor versions differ); " +
        "it is used anyway because it is a prerelease.";

    [Fact]
    [Trait("Requirement", "FP06-1-AC6")]
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

        console.Lines
            .Should().Equal("shade-kit 1.2.3");

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
    [Trait("Requirement", "FP06-1-AC6")]
    public async Task ExecuteAsync_JsonModeWithoutExpect_WritesEnvelopeWithoutExpectedVersionUnchanged()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3");

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        jsonOutput.ToString()
            .Should().Be(
                """
                {
                  "ok": true,
                  "data": {
                    "engineVersion": "1.2.3",
                    "formats": {
                      "config": 1,
                      "gates": 1,
                      "feature": 1,
                      "current-feature": 1,
                      "templates-lock": 1,
                      "integrations": 1
                    }
                  },
                  "warnings": [],
                  "error": null
                }
                """.ReplaceLineEndings("\n") + "\n");
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC6")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_WithoutExpect_DoesNotCheckEngineVersion(bool json)
    {
        // Arrange
        var checker = A.Fake<IEngineVersionChecker>();
        var command = new VersionCommand(TestShadeKitCommandContext.Create(new TestConsole()),
            CreateEngineVersionProvider("1.2.3"), FormatRegistry.CreateDefault(), checker);

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { Json = json });

        // Assert
        result.ExitCode
            .Should().Be(0);

        A.CallTo(checker)
            .MustNotHaveHappened();
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC3")]
    [InlineData("1.2.3")]
    [InlineData("1.2.0")]
    [InlineData("1.2.9")]
    [InlineData("1.2.0-rc.1")]
    [InlineData("1.2.0+build.5")]
    public async Task ExecuteAsync_JsonModeExpectWithEqualMajorAndMinor_WritesSuccessWithExpectedVersionAndNoWarning(
        string expect)
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3");

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { Json = true, Expect = expect });

        // Assert
        result.ExitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        root.GetProperty("data").EnumerateObject().Select(x => x.Name)
            .Should().Equal("engineVersion", "formats", "expectedVersion");

        root.GetProperty("data").GetProperty("engineVersion").GetString()
            .Should().Be("1.2.3");

        root.GetProperty("data").GetProperty("expectedVersion").GetString()
            .Should().Be(expect);

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        root.GetProperty("error").ValueKind
            .Should().Be(JsonValueKind.Null);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP06-1-AC3")]
    public async Task ExecuteAsync_PlainModeExpectWithDifferentPatch_WritesOnlyToolNameAndEngineVersion()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3");

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { Expect = "1.2.0" });

        // Assert
        result.ExitCode
            .Should().Be(0);

        console.Lines
            .Should().Equal("shade-kit 1.2.3");

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC1")]
    [Trait("Requirement", "FP06-1-AC2")]
    [InlineData("1.3.0", OlderEngineMessage)]
    [InlineData("1.1.0", NewerEngineMessage)]
    public async Task ExecuteAsync_JsonModeReleaseEngineMismatch_WritesVersionMismatchEnvelopeAndAbortsWith50(
        string expect, string message)
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3");

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { Json = true, Expect = expect });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.VersionMismatch);

        exception.PrintMessage
            .Should().BeFalse();

        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("data").ValueKind
            .Should().Be(JsonValueKind.Null);

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.VersionMismatch);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be(message);

        console.Output
            .Should().BeEmpty();
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC1")]
    [Trait("Requirement", "FP06-1-AC2")]
    [InlineData("1.3.0", OlderEngineMessage)]
    [InlineData("1.1.0", NewerEngineMessage)]
    public async Task ExecuteAsync_PlainModeReleaseEngineMismatch_AbortsWith50AndMessageWithoutOutput(
        string expect, string message)
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3");

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { Expect = expect });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.VersionMismatch);

        exception.PrintMessage
            .Should().BeTrue();

        exception.Message
            .Should().Be(message);

        console.Output
            .Should().BeEmpty();

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC1")]
    [InlineData("2.0.0")]
    [InlineData("0.9.0")]
    public async Task ExecuteAsync_ReleaseEngineWithDifferentMajor_AbortsWith50(string expect)
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), TextWriter.Null, "1.2.3");

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { Json = true, Expect = expect });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.VersionMismatch);
    }

    [Fact]
    [Trait("Requirement", "FP06-1-AC4")]
    public async Task ExecuteAsync_JsonModePrereleaseEngineMismatch_WritesSuccessEnvelopeWithWarning()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3-beta.1");

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { Json = true, Expect = "1.3.0" });

        // Assert
        result.ExitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        root.GetProperty("data").GetProperty("engineVersion").GetString()
            .Should().Be("1.2.3-beta.1");

        root.GetProperty("data").GetProperty("expectedVersion").GetString()
            .Should().Be("1.3.0");

        root.GetProperty("warnings").EnumerateArray()
            .Select(x => (x.GetProperty("code").GetString(), x.GetProperty("message").GetString()))
            .Should().Equal((ShadeKitCliWarningCodes.EnginePrereleaseVersionMismatch, PrereleaseWarningMessage));

        root.GetProperty("error").ValueKind
            .Should().Be(JsonValueKind.Null);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP06-1-AC4")]
    public async Task ExecuteAsync_PlainModePrereleaseEngineMismatch_WritesWarningBeforeToolNameAndEngineVersion()
    {
        // Arrange
        // A wide console keeps the long warning on one line.
        var console = new TestConsole().Width(1000);
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3-beta.1");

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { Expect = "1.3.0" });

        // Assert
        result.ExitCode
            .Should().Be(0);

        console.Lines
            .Should().Equal($"Warning: {PrereleaseWarningMessage}", "shade-kit 1.2.3-beta.1");

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("v1.2.3")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    public async Task ExecuteAsync_JsonModeInvalidExpect_WritesInvalidArgumentsEnvelopeAndAbortsWith2(string expect)
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3");

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { Json = true, Expect = expect });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        exception.PrintMessage
            .Should().BeFalse();

        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidArguments);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be(CreateInvalidExpectMessage(expect));

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP06-1-AC5")]
    public async Task ExecuteAsync_PlainModeInvalidExpect_AbortsWith2AndMessageWithoutOutput()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3");

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { Expect = "abc" });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        exception.PrintMessage
            .Should().BeTrue();

        exception.Message
            .Should().Be(CreateInvalidExpectMessage("abc"));

        console.Output
            .Should().BeEmpty();

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ExpectWithInvalidEngineVersion_ThrowsInvalidOperationExceptionAndWritesNothing(
        bool json)
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "not-a-version");

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { Json = json, Expect = "1.2.3" });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        console.Output
            .Should().BeEmpty();

        jsonOutput.ToString()
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
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3", registry);

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
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3", new FormatRegistry([]));

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
    public void Ctor_NullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(null!, CreateEngineVersionProvider("1.0.0"), FormatRegistry.CreateDefault(),
            A.Fake<IEngineVersionChecker>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Ctor_NullEngineVersionProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(TestShadeKitCommandContext.Create(new TestConsole()), null!,
            FormatRegistry.CreateDefault(), A.Fake<IEngineVersionChecker>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("engineVersionProvider");
    }

    [Fact]
    public void Ctor_NullFormatRegistry_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(TestShadeKitCommandContext.Create(new TestConsole()),
            CreateEngineVersionProvider("1.0.0"), null!, A.Fake<IEngineVersionChecker>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formatRegistry");
    }

    [Fact]
    public void Ctor_NullEngineVersionChecker_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(TestShadeKitCommandContext.Create(new TestConsole()),
            CreateEngineVersionProvider("1.0.0"), FormatRegistry.CreateDefault(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("engineVersionChecker");
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

    private static VersionCommand CreateCommand(IAnsiConsole console, TextWriter jsonOutput, string engineVersion,
        IFormatRegistry? formatRegistry = null)
    {
        var engineVersionProvider = CreateEngineVersionProvider(engineVersion);

        return new VersionCommand(TestShadeKitCommandContext.Create(console, jsonOutput: jsonOutput),
            engineVersionProvider, formatRegistry ?? FormatRegistry.CreateDefault(),
            new EngineVersionChecker(engineVersionProvider));
    }

    private static IEngineVersionProvider CreateEngineVersionProvider(string version)
    {
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).Returns(version);

        return engineVersionProvider;
    }

    private static string CreateInvalidExpectMessage(string expect)
    {
        return $"Invalid arguments: '{expect}' is not a SemVer version such as 1.2.3 or 1.2.3-beta.1. Run " +
               "'shade-kit --help' for usage.";
    }
}
