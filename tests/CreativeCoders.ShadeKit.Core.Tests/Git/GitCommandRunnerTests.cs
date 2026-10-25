using System.ComponentModel;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Git;

// These tests never start the real git: the locator is faked, and the only start attempt targets a file that does
// not exist.
public class GitCommandRunnerTests
{
    private static readonly string[] BranchArguments = ["symbolic-ref", "--short", "-q", "HEAD"];

    private readonly IGitExecutableLocator _gitExecutableLocator = A.Fake<IGitExecutableLocator>();

    [Fact]
    public void Constructor_NullGitExecutableLocator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitCommandRunner(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitExecutableLocator");
    }

    [Fact]
    public async Task RunAsync_GitNotFound_ThrowsGitUnavailableException()
    {
        // Arrange
        A.CallTo(() => _gitExecutableLocator.FindGitExecutable()).Returns(null);
        var sut = new GitCommandRunner(_gitExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), BranchArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GitUnavailableException>()).Which;

        exception.Arguments
            .Should().Equal(BranchArguments);

        exception.Reason
            .Should().Be("git was not found in any absolute directory of the PATH environment variable.");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_GitLocatorReturnsEmptyPath_ThrowsGitUnavailableException()
    {
        // Arrange
        A.CallTo(() => _gitExecutableLocator.FindGitExecutable()).Returns(string.Empty);
        var sut = new GitCommandRunner(_gitExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), BranchArguments, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GitUnavailableException>()).Which.Reason
            .Should().Be("git was not found in any absolute directory of the PATH environment variable.");
    }

    [Theory]
    [InlineData("git")]
    [InlineData("./git")]
    [InlineData("bin/git")]
    public async Task RunAsync_GitLocatorReturnsRelativePath_ThrowsGitUnavailableExceptionWithoutStarting(
        string relativePath)
    {
        // Arrange
        A.CallTo(() => _gitExecutableLocator.FindGitExecutable()).Returns(relativePath);
        var sut = new GitCommandRunner(_gitExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), BranchArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GitUnavailableException>()).Which;

        exception.Reason
            .Should().Be($"The git executable path '{relativePath}' is not absolute, so it is not started.");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_CalledTwice_LocatesGitOnEveryRun()
    {
        // Arrange
        A.CallTo(() => _gitExecutableLocator.FindGitExecutable()).Returns(null);
        var sut = new GitCommandRunner(_gitExecutableLocator);

        // Act
        var firstAct = () => sut.RunAsync(Path.GetTempPath(), BranchArguments,
            TestContext.Current.CancellationToken);
        var secondAct = () => sut.RunAsync(Path.GetTempPath(), BranchArguments,
            TestContext.Current.CancellationToken);

        // Assert
        await firstAct.Should().ThrowAsync<GitUnavailableException>();
        await secondAct.Should().ThrowAsync<GitUnavailableException>();

        A.CallTo(() => _gitExecutableLocator.FindGitExecutable())
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task RunAsync_ExecutableCannotBeStarted_ThrowsGitUnavailableExceptionWithStartFailure()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var missingExecutablePath = Path.Combine(tempDirectory.RootPath, "missing", "git");

        A.CallTo(() => _gitExecutableLocator.FindGitExecutable()).Returns(missingExecutablePath);
        var sut = new GitCommandRunner(_gitExecutableLocator);

        // Act
        var act = () => sut.RunAsync(tempDirectory.RootPath, BranchArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GitUnavailableException>()).Which;

        exception.Arguments
            .Should().Equal(BranchArguments);

        exception.Reason
            .Should().StartWith("The git executable '").And.Contain("' could not be started: ").And.EndWith(".");

        exception.InnerException
            .Should().BeOfType<Win32Exception>();
    }

    [Fact]
    public async Task RunAsync_CanceledToken_ThrowsOperationCanceledExceptionWithoutLocatingGit()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var sut = new GitCommandRunner(_gitExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), BranchArguments, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(() => _gitExecutableLocator.FindGitExecutable())
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunAsync_InvalidWorkingDirectory_ThrowsArgumentException(string? workingDirectory)
    {
        // Arrange
        var sut = new GitCommandRunner(_gitExecutableLocator);

        // Act
        var act = () => sut.RunAsync(workingDirectory!, BranchArguments, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        A.CallTo(() => _gitExecutableLocator.FindGitExecutable())
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task RunAsync_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new GitCommandRunner(_gitExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("arguments");

        A.CallTo(() => _gitExecutableLocator.FindGitExecutable())
            .MustNotHaveHappened();
    }
}
