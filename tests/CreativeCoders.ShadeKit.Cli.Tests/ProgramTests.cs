using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Tests.Shared;
using CreativeCoders.SysConsole.Cli.Parsing;
using CreativeCoders.SysConsole.Cli.Parsing.Exceptions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests;

[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed class ProgramTests : IDisposable
{
    // CliJsonWriter ends every JSON document with LF on every platform, not with Environment.NewLine.
    private const string JsonLineEnd = "\n";

    private const string StackTraceLinePrefix = "   at ";

    private readonly CurrentDirectoryScope _currentDirectory = new CurrentDirectoryScope();

    private readonly TempKitDirectory _tempDirectory = new TempKitDirectory("shade-kit-program-tests-");

    [Fact]
    [Trait("Requirement", "FP01-AC7")]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_VersionJson_WritesOnlyOneEnvelopeToStandardOutput()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        document.RootElement.GetProperty("data").GetProperty("formats").EnumerateObject().Select(x => x.Name)
            .Should().Equal("config", "gates", "feature", "current-feature", "templates-lock", "integrations");

        error.ToString()
            .Should().BeNullOrWhiteSpace("the host writes only its empty footer line to standard error");
    }

    [Fact]
    public async Task RunAsync_VersionPlain_WritesToolNameAndVersion()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        output.ToString()
            .Should().StartWith("shade-kit ");
    }

    [Fact]
    public async Task RunAsync_ServiceRegisteredByHook_ReplacesDefaultService()
    {
        // Arrange
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).Returns("9.8.7-test");

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "--json"], output, error,
            x => x.AddSingleton(engineVersionProvider));

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("data").GetProperty("engineVersion").GetString()
            .Should().Be("9.8.7-test");
    }

    [Fact]
    public async Task RunAsync_HookWithoutRegistrations_UsesDefaultServices()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();
        var hookCalls = 0;

        // Act
        var exitCode = await Program.RunAsync(["version", "--json"], output, error, _ => hookCalls++);

        // Assert
        exitCode
            .Should().Be(0);

        hookCalls
            .Should().Be(1);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("data").GetProperty("engineVersion").GetString()
            .Should().Be(new EngineVersionProvider().Version);
    }

    [Fact]
    public async Task RunAsync_HookRegistersConsole_KeepsConsoleOfTheRun()
    {
        // Arrange
        using var hookConsole = new TestConsole();
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version"], output, error,
            x => x.AddSingleton<IAnsiConsole>(hookConsole));

        // Assert
        exitCode
            .Should().Be(0);

        output.ToString()
            .Should().StartWith("shade-kit ");

        hookConsole.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_HookRegistersJsonWriter_KeepsJsonWriterOfTheRunAndWritesOneEnvelope()
    {
        // Arrange
        var hookJsonWriter = A.Fake<ICliJsonWriter>();
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "--json"], output, error,
            x => x.AddSingleton(hookJsonWriter));

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        A.CallTo(hookJsonWriter)
            .MustNotHaveHappened();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    public async Task RunAsync_HookThrowsInJsonMode_WritesHostErrorEnvelopeAndDetailsToStandardError()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "--json"], output, error,
            _ => throw new InvalidOperationException("hook failed"));

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.HostError);

        error.ToString()
            .Should().Contain("hook failed");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_UnknownCommandJson_WritesHostErrorEnvelopeAndReturns255()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["unknown-command", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;

        root.EnumerateObject().Select(x => x.Name)
            .Should().Equal("ok", "data", "warnings", "error");

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.HostError);

        error.ToString()
            .Should().Contain("unknown-command");
    }

    [Theory]
    [InlineData("--json", "false")]
    [InlineData("--json", "False")]
    [InlineData("--json=false")]
    [InlineData("--json", "not-a-boolean")]
    [InlineData("--json", "yes")]
    [InlineData("--json", "1")]
    public async Task RunAsync_JsonSwitchWithValueTheParserReadsAsFalse_UsesPlainModeOnStandardOutput(
        params string[] jsonArgs)
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", .. jsonArgs], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        output.ToString()
            .Should().StartWith("shade-kit ")
            .And.NotContain("\"ok\"");
    }

    [Fact]
    public async Task RunAsync_JsonSwitchBeforeCommand_TakesCommandAsSwitchValueAndReportsUnknownCommandAsText()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["--json", "version"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        output.ToString()
            .Should().Contain("No command found")
            .And.NotContain("\"ok\"");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_JsonSwitchWithExtraLeadingDash_UsesJsonModeLikeTheParser()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "---json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();
    }

    [Theory]
    [Trait("Requirement", "FP01-AC8")]
    [Trait("Requirement", "FP01-AC12")]
    [InlineData("version", "--json=true")]
    [InlineData("version", "--json=yes")]
    public async Task RunAsync_JsonAssignmentTheParserDoesNotRead_WritesHostErrorEnvelope(params string[] args)
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(args, output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.HostError);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_FalseAssignmentBeforeBareJsonSwitch_FollowsBareSwitchLikeTheParser()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "--json=false", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        var text = output.ToString();

        text.Should().StartWith("{").And.EndWith("}" + JsonLineEnd).And.NotEndWith(JsonLineEnd + JsonLineEnd);

        using var document = JsonDocument.Parse(text);

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_JsonSwitchTwice_WritesSuccessEnvelope()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "--json", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_JsonSwitchWithTrueValue_UsesJsonMode()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "--json", "true"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    [UnsupportedOSPlatform("windows")]
    public async Task RunAsync_UnexpectedExceptionJson_WritesHostErrorEnvelopeAndDetailsToStandardError()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a directory unreadable.");
        Assert.SkipWhen(Environment.IsPrivilegedProcess,
            "A privileged process (root) can read a directory with file mode 000.");

        _tempDirectory.CreateGitRoot();
        var featuresPath = _tempDirectory.CreateDirectory(".shadekit", "features");
        File.SetUnixFileMode(featuresPath, UnixFileMode.None);
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            // Act
            var exitCode = await Program.RunAsync(["feature", "list", "--json"], output, error);

            // Assert
            exitCode
                .Should().Be(ShadeKitCliExitCodes.HostError);

            using var document = JsonDocument.Parse(output.ToString());

            document.RootElement.GetProperty("ok").GetBoolean()
                .Should().BeFalse();

            document.RootElement.GetProperty("error").GetProperty("code").GetString()
                .Should().Be(ShadeKitCliErrorCodes.HostError);

            error.ToString()
                .Should().Contain(nameof(UnauthorizedAccessException));
        }
        finally
        {
            File.SetUnixFileMode(featuresPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task RunAsync_UnknownCommandPlain_ReturnsHostErrorWithoutJson()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["unknown-command"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        output.ToString()
            .Should().Contain("unknown-command")
            .And.NotContain("\"ok\"");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    [Trait("Requirement", "FP01-AC8")]
    public async Task RunAsync_FeatureListJsonInRepositoryWithoutKitRoot_WritesKitRootNotFoundEnvelopeAndReturns10()
    {
        // Arrange
        _tempDirectory.CreateGitRoot();
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "list", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.KitRootNotFound);

        document.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("shade-kit init");

        error.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC1")]
    public async Task RunAsync_FeatureListJsonInSubdirectoryOfInitialisedRepository_ListsFeaturesOfRepositoryRoot()
    {
        // Arrange
        _tempDirectory.CreateGitRoot();
        WriteFeatureFile("auth");
        var nestedPath = _tempDirectory.CreateDirectory("src", "app");
        Directory.SetCurrentDirectory(nestedPath);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "list", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("data").GetProperty("features").EnumerateArray()
            .Select(x => x.GetProperty("id").GetString())
            .Should().Equal("auth");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public async Task RunAsync_FeatureListPlainInRepositoryWithoutKitRoot_PrintsInitHintAndReturns10()
    {
        // Arrange
        _tempDirectory.CreateGitRoot();
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "list"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        output.ToString()
            .Should().Contain("shade-kit init");
    }

    [Theory]
    [Trait("Requirement", "FP01-AC12")]
    [InlineData("version", "--help", "--json")]
    [InlineData("help", "--json")]
    public async Task RunAsync_HelpInJsonMode_WritesHostErrorEnvelopeAndHelpToStandardError(params string[] args)
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(args, output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.HostError);

        error.ToString()
            .Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    public async Task RunAsync_JsonSwitchWithoutCommand_WritesHostErrorEnvelope()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.HostError);
    }

    [Fact]
    public async Task RunAsync_JsonWithUnknownOption_IgnoresOptionAndWritesSuccessEnvelope()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "--json", "--bogus"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_VersionJson_WritesExactlyOneDocumentFollowedByOneLineBreak()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        await Program.RunAsync(["version", "--json"], output, error);

        // Assert
        var text = output.ToString();

        text.Should().StartWith("{").And.EndWith("}" + JsonLineEnd).And.NotEndWith(JsonLineEnd + JsonLineEnd);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_FeatureListJsonWithoutFeaturesDirectory_WritesEmptyFeatureListAndReturns0()
    {
        // Arrange
        _tempDirectory.CreateGitRoot();
        _tempDirectory.CreateDirectory(".shadekit");
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "list", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        document.RootElement.GetProperty("data").GetProperty("features").GetArrayLength()
            .Should().Be(0);

        error.ToString()
            .Should().BeNullOrWhiteSpace("the host writes only its empty footer line to standard error");
    }

    [Fact]
    public async Task RunAsync_FeatureListPlainWithoutFeaturesDirectory_PrintsNoFeaturesHintAndReturns0()
    {
        // Arrange
        _tempDirectory.CreateGitRoot();
        _tempDirectory.CreateDirectory(".shadekit");
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "list"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        output.ToString()
            .Should().Contain("No features found.");
    }

    [Fact]
    public async Task RunAsync_FeatureListPlainInInitialisedRepository_PrintsFeaturesInOrdinalOrder()
    {
        // Arrange
        _tempDirectory.CreateGitRoot();
        WriteFeatureFile("login");
        WriteFeatureFile("9-billing");
        WriteFeatureFile("10-billing");
        WriteFeatureFile("Billing");
        _tempDirectory.CreateDirectory(".shadekit", "features", ".login.1a2b3c4d.tmp");
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "list"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // The console wraps the long warning line, so its parts are joined again with single spaces.
        string.Join(' ', lines[..^3].Select(x => x.Trim()))
            .Should().Be(
                "Warning: The folder '.shadekit/features/Billing/' is not a valid feature id and was skipped.");

        lines[^3..]
            .Should().Equal(
                "10-billing  active  feature  new  Title of 10-billing",
                "9-billing  active  feature  new  Title of 9-billing",
                "login  active  feature  new  Title of login");
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task RunAsync_UnexpectedExceptionPlain_ReturnsHostErrorWithDetailsOnStandardErrorAndNoEnvelope()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a directory unreadable.");
        Assert.SkipWhen(Environment.IsPrivilegedProcess,
            "A privileged process (root) can read a directory with file mode 000.");

        _tempDirectory.CreateGitRoot();
        var featuresPath = _tempDirectory.CreateDirectory(".shadekit", "features");
        File.SetUnixFileMode(featuresPath, UnixFileMode.None);
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            // Act
            var exitCode = await Program.RunAsync(["feature", "list"], output, error);

            // Assert
            exitCode
                .Should().Be(ShadeKitCliExitCodes.HostError);

            error.ToString()
                .Should().StartWith("The command failed unexpectedly: ")
                .And.Contain(nameof(UnauthorizedAccessException));

            output.ToString()
                .Should().NotContain("\"ok\"");
        }
        finally
        {
            File.SetUnixFileMode(featuresPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    public async Task RunAsync_JsonModeWithBrokenStandardOutput_ReturnsHostErrorWithUnwrappedCauseOnStandardError()
    {
        // Arrange
        await using var output = new BrokenTextWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        error.ToString()
            .Should().StartWith($"The command failed unexpectedly: {BrokenTextWriter.Message}")
            .And.Contain("The JSON result could not be written");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    public async Task RunAsync_JsonModeFailingAfterSuccessEnvelope_ReturnsZeroToMatchTheEnvelope()
    {
        // Arrange
        using var output = new StringWriter();
        await using var error = new BrokenTextWriter();

        // Act
        var exitCode = await Program.RunAsync(["version", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_PlainModeWithBrokenStandardOutputAndError_ReturnsHostError()
    {
        // Arrange
        await using var output = new BrokenTextWriter();
        await using var error = new BrokenTextWriter();

        // Act
        var exitCode = await Program.RunAsync(["version"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);
    }

    [Fact]
    public async Task RunAsync_FeatureShowWithoutIdPlain_WritesOneUsageLineToStandardErrorAndReturns2()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "show"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        error.ToString()
            .Should().Be("Invalid arguments: the required argument <FeatureId> is missing. Run 'shade-kit feature " +
                         "show --help' for usage." + Environment.NewLine);

        output.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_FeatureShowWithoutIdJson_WritesOneInvalidArgumentsEnvelopeAndReturns2()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "show", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        AssertOnlyInvalidArgumentsEnvelope(output.ToString(), "<FeatureId>", "shade-kit feature show --help");

        error.ToString()
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData]
    [InlineData("--title", "Login")]
    public async Task RunAsync_FeatureNewWithoutSourcePlain_WritesOneUsageLineToStandardErrorAndReturns2(
        params string[] extraArgs)
    {
        // Arrange
        var gitCommandRunner = A.Fake<IGitCommandRunner>();

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "new", ..extraArgs], output, error,
            x => x.AddSingleton(gitCommandRunner));

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        error.ToString()
            .Should().Be("Invalid arguments: the required option '--source' is missing. Run 'shade-kit feature new " +
                         "--help' for usage." + Environment.NewLine);

        output.ToString()
            .Should().BeEmpty();

        A.CallTo(gitCommandRunner)
            .MustNotHaveHappened();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_FeatureNewWithoutSourceJson_WritesOneInvalidArgumentsEnvelopeAndReturns2()
    {
        // Arrange
        var gitCommandRunner = A.Fake<IGitCommandRunner>();

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "new", "--title", "Login", "--json"], output, error,
            x => x.AddSingleton(gitCommandRunner));

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        AssertOnlyInvalidArgumentsEnvelope(output.ToString(), "'--source'", "shade-kit feature new --help");

        error.ToString()
            .Should().BeEmpty();

        A.CallTo(gitCommandRunner)
            .MustNotHaveHappened();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task RunAsync_ArgumentsMatchNoOptionJson_WritesOneInvalidArgumentsEnvelopeAndReturns2()
    {
        // Arrange
        // No options type of the CLI sets AllArgsMustMatch, so the parser does not report unmatched arguments of the
        // real commands; the hook throws its exception inside the run instead.
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "list", "--bogus", "--json"], output, error,
            _ => throw new NotAllArgumentsMatchException(
                [new OptionArgument { Kind = OptionArgumentKind.LongName, OptionName = "bogus" }]));

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        AssertOnlyInvalidArgumentsEnvelope(output.ToString(), "'--bogus'", "shade-kit feature list --help");

        error.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_ArgumentsMatchNoOptionPlain_WritesOneUsageLineToStandardErrorAndReturns2()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "list", "extra"], output, error,
            _ => throw new NotAllArgumentsMatchException(
                [new OptionArgument { Kind = OptionArgumentKind.Value, Value = "extra" }]));

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        error.ToString()
            .Should().Be("Invalid arguments: the argument 'extra' matches no option. Run 'shade-kit feature list " +
                         "--help' for usage." + Environment.NewLine);

        output.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_ParserExceptionWrappedInTargetInvocationPlain_WritesUsageLineWithoutStackTrace()
    {
        // Arrange
        var property = typeof(FeatureShowCommandOptions).GetProperty(nameof(FeatureShowCommandOptions.FeatureId))!;
        var parserException = new RequiredArgumentMissingException(property,
            property.GetCustomAttribute<OptionBaseAttribute>()!);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "show"], output, error,
            _ => throw new TargetInvocationException(parserException));

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        error.ToString()
            .Should().StartWith("Invalid arguments: the required argument <FeatureId> is missing.")
            .And.NotContain(StackTraceLinePrefix)
            .And.NotContain(nameof(TargetInvocationException));
    }

    [Theory]
    [InlineData("--json=true")]
    [InlineData("--json", "true")]
    public async Task RunAsync_ArgumentErrorWithJsonSwitchValue_WritesOneInvalidArgumentsEnvelope(
        params string[] jsonArgs)
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "show", ..jsonArgs], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        AssertOnlyInvalidArgumentsEnvelope(output.ToString(), "<FeatureId>", "shade-kit feature show --help");
    }

    [Fact]
    public async Task RunAsync_ArgumentErrorWithJsonSwitchFalse_WritesUsageLineToStandardErrorOnly()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "show", "--json", "false"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        error.ToString()
            .Should().StartWith("Invalid arguments: the required argument <FeatureId> is missing.")
            .And.NotContain(StackTraceLinePrefix);

        output.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    public async Task RunAsync_ArgumentErrorJsonWithBrokenStandardOutput_ReturnsHostError()
    {
        // Arrange
        await using var output = new BrokenTextWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "show", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        error.ToString()
            .Should().Be($"The JSON result could not be written: {BrokenTextWriter.Message}" + Environment.NewLine);
    }

    [Fact]
    public async Task RunAsync_ArgumentErrorPlainWithBrokenStandardError_StillReturns2()
    {
        // Arrange
        using var output = new StringWriter();
        await using var error = new BrokenTextWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "show"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);
    }

    [Fact]
    public async Task RunAsync_OptionCreationFailedJson_StaysHostErrorWithDetailsOnStandardError()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "show", "--json"], output, error,
            _ => throw new OptionCreationFailedException(typeof(FeatureShowCommandOptions)));

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.HostError);

        error.ToString()
            .Should().Contain(nameof(OptionCreationFailedException));
    }

    public void Dispose()
    {
        _currentDirectory.Dispose();
        _tempDirectory.Dispose();
    }

    private static void AssertOnlyInvalidArgumentsEnvelope(string output, string argumentText, string helpCommand)
    {
        output
            .Should().EndWith("}" + JsonLineEnd).And.NotEndWith(JsonLineEnd + JsonLineEnd)
            .And.NotContain(StackTraceLinePrefix);

        // JsonDocument.Parse rejects a second top-level value, so this also proves that only one envelope was written.
        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;

        root.EnumerateObject().Select(x => x.Name)
            .Should().Equal("ok", "data", "warnings", "error");

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidArguments);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().StartWith("Invalid arguments: ")
            .And.Contain(argumentText)
            .And.EndWith($"Run '{helpCommand}' for usage.");
    }

    /// <summary>
    /// A writer whose reading end is gone, like standard output piped to a process that has exited.
    /// </summary>
    private sealed class BrokenTextWriter : TextWriter
    {
        public const string Message = "Broken pipe";

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            throw new IOException(Message);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            throw new IOException(Message);
        }

        public override void Write(string? value)
        {
            throw new IOException(Message);
        }

        public override void Flush()
        {
            throw new IOException(Message);
        }
    }

    private void WriteFeatureFile(string featureId)
    {
        _tempDirectory.WriteFile(
            $$"""
              {
                "schemaVersion": 1,
                "minEngineVersion": "0.1.0",
                "id": "{{featureId}}",
                "title": "Title of {{featureId}}",
                "profile": "feature",
                "source": { "kind": "text" },
                "createdAt": "2026-10-20T15:30:00+02:00",
                "createdBy": { "name": "Jane Doe", "email": "jane@example.com" },
                "status": "active"
              }
              """,
            ".shadekit", "features", featureId, "feature.json");
    }
}
