using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;

namespace CreativeCoders.ShadeKit.Cli.Tests;

[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed class ProgramTests : IDisposable
{
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    private readonly DirectoryInfo _tempDirectory = Directory.CreateTempSubdirectory("shade-kit-program-tests-");

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

        text.Should().StartWith("{").And.EndWith("}\n").And.NotEndWith("\n\n");

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

        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".git"));
        var featuresPath = Directory.CreateDirectory(
            Path.Combine(_tempDirectory.FullName, ".shadekit", "features")).FullName;
        File.SetUnixFileMode(featuresPath, UnixFileMode.None);
        Directory.SetCurrentDirectory(_tempDirectory.FullName);

        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            // Act
            var exitCode = await Program.RunAsync(["features", "list", "--json"], output, error);

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
    public async Task RunAsync_FeaturesListJsonInRepositoryWithoutKitRoot_WritesKitRootNotFoundEnvelopeAndReturns10()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".git"));
        Directory.SetCurrentDirectory(_tempDirectory.FullName);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["features", "list", "--json"], output, error);

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
    public async Task RunAsync_FeaturesListJsonInSubdirectoryOfInitialisedRepository_ListsFeaturesOfRepositoryRoot()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".git"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".shadekit", "features", "auth"));
        var nestedPath = Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, "src", "app")).FullName;
        Directory.SetCurrentDirectory(nestedPath);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["features", "list", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("data").GetProperty("features").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("auth");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public async Task RunAsync_FeaturesListPlainInRepositoryWithoutKitRoot_PrintsInitHintAndReturns10()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".git"));
        Directory.SetCurrentDirectory(_tempDirectory.FullName);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["features", "list"], output, error);

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

        text.Should().StartWith("{").And.EndWith("}\n").And.NotEndWith("\n\n");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    public async Task RunAsync_FeaturesListJsonWithoutFeaturesDirectory_WritesFeaturesDirEnvelopeAndReturns11()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".git"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".shadekit"));
        Directory.SetCurrentDirectory(_tempDirectory.FullName);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["features", "list", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.FeaturesDirectoryNotFound);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.FeaturesDirectoryNotFound);

        error.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_FeaturesListPlainInInitialisedRepository_PrintsNamesInOrdinalOrder()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".git"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".shadekit", "features", "login"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".shadekit", "features", "Billing"));
        Directory.SetCurrentDirectory(_tempDirectory.FullName);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["features", "list"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Should().Equal("Billing", "login");
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task RunAsync_UnexpectedExceptionPlain_ReturnsHostErrorWithDetailsOnStandardErrorAndNoEnvelope()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a directory unreadable.");
        Assert.SkipWhen(Environment.IsPrivilegedProcess,
            "A privileged process (root) can read a directory with file mode 000.");

        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, ".git"));
        var featuresPath = Directory.CreateDirectory(
            Path.Combine(_tempDirectory.FullName, ".shadekit", "features")).FullName;
        File.SetUnixFileMode(featuresPath, UnixFileMode.None);
        Directory.SetCurrentDirectory(_tempDirectory.FullName);

        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            // Act
            var exitCode = await Program.RunAsync(["features", "list"], output, error);

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

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        _tempDirectory.Delete(true);
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
}
