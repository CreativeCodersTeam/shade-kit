using System.ComponentModel;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Trackers;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Trackers;

// These tests never start the real gh: the locator is faked, and the only start attempt targets a file that does not
// exist.
public class GhCommandRunnerTests
{
    private const string NotFoundReason =
        "The GitHub CLI gh was not found in any absolute directory of the PATH environment variable.";

    private static readonly string[] IssueArguments =
        ["issue", "view", "https://github.com/octo/repo/issues/1", "--json", "title,body,comments"];

    private readonly IGhExecutableLocator _ghExecutableLocator = A.Fake<IGhExecutableLocator>();

    [Fact]
    public void Constructor_NullGhExecutableLocator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GhCommandRunner(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ghExecutableLocator");
    }

    [Fact]
    public void EnvironmentVariables_Always_DisablePromptsUpdateNotifierAndColors()
    {
        // Act
        var environmentVariables = GhCommandRunner.EnvironmentVariables;

        // Assert
        environmentVariables
            .Should().BeEquivalentTo(new Dictionary<string, string>
            {
                ["GH_PROMPT_DISABLED"] = "1",
                ["GH_NO_UPDATE_NOTIFIER"] = "1",
                ["NO_COLOR"] = "1"
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RunAsync_GhNotFound_ThrowsGhNotFoundException(string? ghExecutablePath)
    {
        // Arrange
        A.CallTo(() => _ghExecutableLocator.FindGhExecutable()).Returns(ghExecutablePath);
        var sut = new GhCommandRunner(_ghExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), IssueArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GhNotFoundException>()).Which;

        exception.Arguments
            .Should().Equal(IssueArguments);

        exception.Reason
            .Should().Be(NotFoundReason);

        exception.InnerException
            .Should().BeNull();
    }

    [Theory]
    [InlineData("gh")]
    [InlineData("./gh")]
    [InlineData("bin/gh")]
    [InlineData("   ")]
    [InlineData(@"C:gh")]
    [InlineData(@"\gh")]
    public async Task RunAsync_GhLocatorReturnsRelativePath_ThrowsGhNotFoundExceptionWithoutStarting(
        string relativePath)
    {
        // Arrange
        A.CallTo(() => _ghExecutableLocator.FindGhExecutable()).Returns(relativePath);
        var sut = new GhCommandRunner(_ghExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), IssueArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GhNotFoundException>()).Which;

        exception.Reason
            .Should().Be($"The gh executable path '{relativePath}' is not absolute, so it is not started.");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_ExecutableCannotBeStarted_ThrowsGhNotFoundExceptionWithStartFailure()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var missingExecutablePath = Path.Combine(tempDirectory.RootPath, "missing", "gh");

        A.CallTo(() => _ghExecutableLocator.FindGhExecutable()).Returns(missingExecutablePath);
        var sut = new GhCommandRunner(_ghExecutableLocator);

        // Act
        var act = () => sut.RunAsync(tempDirectory.RootPath, IssueArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GhNotFoundException>()).Which;

        exception.Arguments
            .Should().Equal(IssueArguments);

        exception.Reason
            .Should().StartWith($"The gh executable '{missingExecutablePath}' could not be started: ")
            .And.EndWith(".");

        exception.InnerException
            .Should().BeOfType<Win32Exception>();
    }

    [Fact]
    public async Task RunAsync_CalledTwice_LocatesGhOnEveryRun()
    {
        // Arrange
        A.CallTo(() => _ghExecutableLocator.FindGhExecutable()).Returns(null);
        var sut = new GhCommandRunner(_ghExecutableLocator);

        // Act
        var firstAct = () => sut.RunAsync(Path.GetTempPath(), IssueArguments, TestContext.Current.CancellationToken);
        var secondAct = () => sut.RunAsync(Path.GetTempPath(), IssueArguments,
            TestContext.Current.CancellationToken);

        // Assert
        await firstAct.Should().ThrowAsync<GhNotFoundException>();
        await secondAct.Should().ThrowAsync<GhNotFoundException>();

        A.CallTo(() => _ghExecutableLocator.FindGhExecutable())
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task RunAsync_CanceledToken_ThrowsOperationCanceledExceptionWithoutLocatingGh()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var sut = new GhCommandRunner(_ghExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), IssueArguments, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(() => _ghExecutableLocator.FindGhExecutable())
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunAsync_InvalidWorkingDirectory_ThrowsArgumentException(string? workingDirectory)
    {
        // Arrange
        var sut = new GhCommandRunner(_ghExecutableLocator);

        // Act
        var act = () => sut.RunAsync(workingDirectory!, IssueArguments, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        A.CallTo(() => _ghExecutableLocator.FindGhExecutable())
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task RunAsync_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new GhCommandRunner(_ghExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("arguments");

        A.CallTo(() => _ghExecutableLocator.FindGhExecutable())
            .MustNotHaveHappened();
    }
}
