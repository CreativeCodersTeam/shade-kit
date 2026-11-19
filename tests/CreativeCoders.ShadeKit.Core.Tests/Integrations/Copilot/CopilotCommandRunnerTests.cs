using System.ComponentModel;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Copilot;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Copilot;

// These tests never start the real copilot: the locator is faked, and the only start attempt targets a file that does
// not exist. The .cmd shim rules are covered by ExternalToolRunnerTests and CmdShimCommandLineTests.
public class CopilotCommandRunnerTests
{
    private const string NotFoundReason =
        "The GitHub Copilot CLI copilot was not found in any absolute directory of the PATH environment variable.";

    private const string ComSpec = @"C:\Windows\System32\cmd.exe";

    private static readonly string[] ListArguments = ["plugin", "list", "--json"];

    private readonly ICopilotExecutableLocator _copilotExecutableLocator = A.Fake<ICopilotExecutableLocator>();

    [Fact]
    public void Constructor_NullCopilotExecutableLocator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CopilotCommandRunner(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("copilotExecutableLocator");
    }

    [Fact]
    public void EnvironmentVariables_Always_DisableColors()
    {
        // Act
        var environmentVariables = CopilotCommandRunner.EnvironmentVariables;

        // Assert
        environmentVariables
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["NO_COLOR"] = "1" });
    }

    [Fact]
    public void Tool_Always_UsesCopilotNameEnvironmentVariablesAndRunsCmdShims()
    {
        // Act
        var tool = CopilotCommandRunner.Tool;

        // Assert
        tool.Name
            .Should().Be("copilot");

        tool.EnvironmentVariables
            .Should().BeSameAs(CopilotCommandRunner.EnvironmentVariables);

        tool.RunsCmdShims
            .Should().BeTrue();

        tool.NotFoundReason
            .Should().Be(NotFoundReason);
    }

    [Fact]
    public void Tool_CmdShimArgumentRefused_ThrowsCopilotCommandFailedWithoutExitCode()
    {
        // Act
        var act = () => ExternalToolRunner.CreateProcessStart(CopilotCommandRunner.Tool, @"C:\npm\copilot.cmd",
            ["plugin", "marketplace", "add", "a&b"], static () => ComSpec);

        // Assert
        var exception = act.Should().Throw<CopilotCommandFailedException>().Which;

        exception.Reason
            .Should().StartWith("Argument 4 'a&b' contains the character '&', which is refused: ");

        exception.ExitCode
            .Should().BeNull();

        exception.Arguments
            .Should().Equal("plugin", "marketplace", "add", "a&b");
    }

    [Fact]
    public void Tool_CmdShimWithoutComSpec_ThrowsCopilotUnavailable()
    {
        // Act
        var act = () => ExternalToolRunner.CreateProcessStart(CopilotCommandRunner.Tool, @"C:\npm\copilot.cmd",
            ListArguments, static () => null);

        // Assert
        act.Should().ThrowExactly<CopilotUnavailableException>();
    }

    [Fact]
    public void Tool_CmdShimWithSafeArguments_StartsComSpecWithShimCommandLine()
    {
        // Act
        var start = ExternalToolRunner.CreateProcessStart(CopilotCommandRunner.Tool, @"C:\npm\copilot.cmd",
            ListArguments, static () => ComSpec);

        // Assert
        start.ExecutablePath
            .Should().Be(ComSpec);

        start.VerbatimArguments
            .Should().Contain(@"C:\npm\copilot.cmd").And.Contain("plugin list --json");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RunAsync_CopilotNotFound_ThrowsCopilotNotFoundException(string? copilotExecutablePath)
    {
        // Arrange
        A.CallTo(() => _copilotExecutableLocator.FindCopilotExecutable()).Returns(copilotExecutablePath);
        var sut = new CopilotCommandRunner(_copilotExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), ListArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotNotFoundException>()).Which;

        exception.Arguments
            .Should().Equal(ListArguments);

        exception.Reason
            .Should().Be(NotFoundReason);

        exception.InnerException
            .Should().BeNull();
    }

    [Theory]
    [InlineData("copilot")]
    [InlineData("./copilot")]
    [InlineData("bin/copilot")]
    [InlineData("   ")]
    public async Task RunAsync_CopilotLocatorReturnsRelativePath_ThrowsCopilotUnavailableExceptionWithoutStarting(
        string relativePath)
    {
        // Arrange
        A.CallTo(() => _copilotExecutableLocator.FindCopilotExecutable()).Returns(relativePath);
        var sut = new CopilotCommandRunner(_copilotExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), ListArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowExactlyAsync<CopilotUnavailableException>()).Which;

        exception.Reason
            .Should().Be($"The copilot executable path '{relativePath}' is not absolute, so it is not started.");
    }

    [Fact]
    public async Task RunAsync_ExecutableCannotBeStarted_ThrowsCopilotUnavailableExceptionWithStartFailure()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var missingExecutablePath = Path.Combine(tempDirectory.RootPath, "missing", "copilot");

        A.CallTo(() => _copilotExecutableLocator.FindCopilotExecutable()).Returns(missingExecutablePath);
        var sut = new CopilotCommandRunner(_copilotExecutableLocator);

        // Act
        var act = () => sut.RunAsync(tempDirectory.RootPath, ListArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowExactlyAsync<CopilotUnavailableException>()).Which;

        exception.Arguments
            .Should().Equal(ListArguments);

        exception.Reason
            .Should().StartWith($"The copilot executable '{missingExecutablePath}' could not be started: ")
            .And.EndWith(".");

        exception.InnerException
            .Should().BeOfType<Win32Exception>();
    }

    [Fact]
    public async Task RunAsync_CanceledToken_ThrowsOperationCanceledExceptionWithoutLocatingCopilot()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var sut = new CopilotCommandRunner(_copilotExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), ListArguments, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(() => _copilotExecutableLocator.FindCopilotExecutable())
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunAsync_InvalidWorkingDirectory_ThrowsArgumentException(string? workingDirectory)
    {
        // Arrange
        var sut = new CopilotCommandRunner(_copilotExecutableLocator);

        // Act
        var act = () => sut.RunAsync(workingDirectory!, ListArguments, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        A.CallTo(() => _copilotExecutableLocator.FindCopilotExecutable())
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task RunAsync_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new CopilotCommandRunner(_copilotExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("arguments");

        A.CallTo(() => _copilotExecutableLocator.FindCopilotExecutable())
            .MustNotHaveHappened();
    }
}
