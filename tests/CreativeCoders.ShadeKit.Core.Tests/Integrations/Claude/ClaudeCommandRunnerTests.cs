using System.ComponentModel;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Claude;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Claude;

// These tests never start the real claude: the locator is faked, and the only start attempt targets a file that does
// not exist.
public class ClaudeCommandRunnerTests
{
    private const string NotFoundReason =
        "The Claude Code CLI claude was not found in any absolute directory of the PATH environment variable.";

    private static readonly string[] ListArguments = ["plugin", "list", "--json"];

    private readonly IClaudeExecutableLocator _claudeExecutableLocator = A.Fake<IClaudeExecutableLocator>();

    [Fact]
    public void Constructor_NullClaudeExecutableLocator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ClaudeCommandRunner(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("claudeExecutableLocator");
    }

    [Fact]
    public void EnvironmentVariables_Always_DisableColors()
    {
        // Act
        var environmentVariables = ClaudeCommandRunner.EnvironmentVariables;

        // Assert
        environmentVariables
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["NO_COLOR"] = "1" });
    }

    [Fact]
    public void Tool_Always_UsesClaudeNameAndEnvironmentVariables()
    {
        // Act
        var tool = ClaudeCommandRunner.Tool;

        // Assert
        tool.Name
            .Should().Be("claude");

        tool.EnvironmentVariables
            .Should().BeSameAs(ClaudeCommandRunner.EnvironmentVariables);
    }

    [Fact]
    public void Tool_Always_DoesNotRunCmdShims()
    {
        // Act
        var tool = ClaudeCommandRunner.Tool;

        // Assert
        tool.RunsCmdShims
            .Should().BeFalse();

        tool.CmdShims
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RunAsync_ClaudeNotFound_ThrowsClaudeNotFoundException(string? claudeExecutablePath)
    {
        // Arrange
        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable()).Returns(claudeExecutablePath);
        var sut = new ClaudeCommandRunner(_claudeExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), ListArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeNotFoundException>()).Which;

        exception.Arguments
            .Should().Equal(ListArguments);

        exception.Reason
            .Should().Be(NotFoundReason);

        exception.InnerException
            .Should().BeNull();
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("./claude")]
    [InlineData("bin/claude")]
    [InlineData("   ")]
    [InlineData(@"C:claude.exe")]
    [InlineData(@"\claude.exe")]
    public async Task RunAsync_ClaudeLocatorReturnsRelativePath_ThrowsClaudeUnavailableExceptionWithoutStarting(
        string relativePath)
    {
        // Arrange
        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable()).Returns(relativePath);
        var sut = new ClaudeCommandRunner(_claudeExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), ListArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowExactlyAsync<ClaudeUnavailableException>()).Which;

        exception.Reason
            .Should().Be($"The claude executable path '{relativePath}' is not absolute, so it is not started.");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_ExecutableCannotBeStarted_ThrowsClaudeUnavailableExceptionWithStartFailure()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var missingExecutablePath = Path.Combine(tempDirectory.RootPath, "missing", "claude");

        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable()).Returns(missingExecutablePath);
        var sut = new ClaudeCommandRunner(_claudeExecutableLocator);

        // Act
        var act = () => sut.RunAsync(tempDirectory.RootPath, ListArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowExactlyAsync<ClaudeUnavailableException>()).Which;

        exception.Arguments
            .Should().Equal(ListArguments);

        exception.Reason
            .Should().StartWith($"The claude executable '{missingExecutablePath}' could not be started: ")
            .And.EndWith(".");

        exception.InnerException
            .Should().BeOfType<Win32Exception>();
    }

    [Fact]
    public async Task RunAsync_CalledTwice_LocatesClaudeOnEveryRun()
    {
        // Arrange
        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable()).Returns(null);
        var sut = new ClaudeCommandRunner(_claudeExecutableLocator);

        // Act
        var firstAct = () => sut.RunAsync(Path.GetTempPath(), ListArguments, TestContext.Current.CancellationToken);
        var secondAct = () => sut.RunAsync(Path.GetTempPath(), ListArguments, TestContext.Current.CancellationToken);

        // Assert
        await firstAct.Should().ThrowAsync<ClaudeNotFoundException>();
        await secondAct.Should().ThrowAsync<ClaudeNotFoundException>();

        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable())
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task RunAsync_CanceledToken_ThrowsOperationCanceledExceptionWithoutLocatingClaude()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var sut = new ClaudeCommandRunner(_claudeExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), ListArguments, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable())
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunAsync_InvalidWorkingDirectory_ThrowsArgumentException(string? workingDirectory)
    {
        // Arrange
        var sut = new ClaudeCommandRunner(_claudeExecutableLocator);

        // Act
        var act = () => sut.RunAsync(workingDirectory!, ListArguments, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable())
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task RunAsync_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new ClaudeCommandRunner(_claudeExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("arguments");

        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable())
            .MustNotHaveHappened();
    }
}
