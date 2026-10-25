using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Git;

public class GitRepositoryInfoTests
{
    private const string RepositoryRootPath = "/repo";

    private static readonly string[] CurrentBranchArguments = ["symbolic-ref", "--short", "-q", "HEAD"];

    private static readonly string[] UserNameArguments = ["config", "user.name"];

    private static readonly string[] UserEmailArguments = ["config", "user.email"];

    private readonly IGitCommandRunner _gitCommandRunner = A.Fake<IGitCommandRunner>();

    [Fact]
    public void Constructor_NullGitCommandRunner_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitRepositoryInfo(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitCommandRunner");
    }

    [Fact]
    public async Task GetCurrentBranchAsync_BranchCheckedOut_ReturnsTrimmedBranchName()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(CurrentBranchArguments, new ExternalProcessResult(0, "feature/gh-142-login-sso\n", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var branch = await sut.GetCurrentBranchAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        branch
            .Should().Be("feature/gh-142-login-sso");
    }

    [Fact]
    public async Task GetCurrentBranchAsync_BranchCheckedOut_RunsSymbolicRefInRepositoryRoot()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(CurrentBranchArguments, new ExternalProcessResult(0, "main\n", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        await sut.GetCurrentBranchAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(RepositoryRootPath,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(CurrentBranchArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetCurrentBranchAsync_DetachedHead_ReturnsNull()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(CurrentBranchArguments, new ExternalProcessResult(1, "", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var branch = await sut.GetCurrentBranchAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        branch
            .Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n")]
    public async Task GetCurrentBranchAsync_SuccessWithoutOutput_ReturnsNull(string standardOutput)
    {
        // Arrange
        _gitCommandRunner.SetUpResult(CurrentBranchArguments, new ExternalProcessResult(0, standardOutput, ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var branch = await sut.GetCurrentBranchAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        branch
            .Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentBranchAsync_UnexpectedExitCode_ThrowsGitUnavailableExceptionWithStandardError()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(CurrentBranchArguments,
            new ExternalProcessResult(128, "", "fatal: not a git repository (or any of the parent directories): .git\n"));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetCurrentBranchAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GitUnavailableException>()).Which;

        exception.Arguments
            .Should().Equal(CurrentBranchArguments);

        exception.Reason
            .Should().Be("git exited with the unexpected exit code 128: " +
                         "fatal: not a git repository (or any of the parent directories): .git.");
    }

    [Fact]
    public async Task GetCurrentBranchAsync_UnexpectedExitCodeWithoutStandardError_ReasonNamesExitCode()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(CurrentBranchArguments, new ExternalProcessResult(-1, "", "  "));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetCurrentBranchAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GitUnavailableException>()).Which;

        exception.Reason
            .Should().Be("git exited with the unexpected exit code -1.");
    }

    [Fact]
    public async Task GetCurrentBranchAsync_RunnerThrowsGitUnavailableException_PropagatesException()
    {
        // Arrange
        var runnerException = new GitUnavailableException(CurrentBranchArguments, "git could not be started.");
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(runnerException);
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetCurrentBranchAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GitUnavailableException>()).Which
            .Should().BeSameAs(runnerException);
    }

    [Fact]
    public async Task GetCurrentBranchAsync_RunnerCanceled_PropagatesOperationCanceledException()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetCurrentBranchAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetCurrentBranchAsync_CancellationToken_PassesTokenToRunner()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _gitCommandRunner.SetUpResult(CurrentBranchArguments, new ExternalProcessResult(0, "main\n", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        await sut.GetCurrentBranchAsync(RepositoryRootPath, cancellationToken);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, cancellationToken))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCurrentBranchAsync_InvalidRepositoryRootPath_ThrowsArgumentException(
        string? repositoryRootPath)
    {
        // Arrange
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetCurrentBranchAsync(repositoryRootPath!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        A.CallTo(_gitCommandRunner)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetIdentityAsync_NameAndEmailSet_ReturnsTrimmedIdentity()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(0, " Jane Doe \n", ""));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(0, "jane@example.com\r\n", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var identity = await sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        identity
            .Should().Be(new GitIdentity("Jane Doe", "jane@example.com"));
    }

    [Fact]
    public async Task GetIdentityAsync_NameAndEmailSet_RunsGitConfigInRepositoryRoot()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(0, "Jane Doe\n", ""));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(0, "jane@example.com\n", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        await sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(RepositoryRootPath,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(UserNameArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _gitCommandRunner.RunAsync(RepositoryRootPath,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(UserEmailArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetIdentityAsync_CancellationToken_PassesTokenToEveryRun()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(0, "Jane Doe\n", ""));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(0, "jane@example.com\n", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        await sut.GetIdentityAsync(RepositoryRootPath, cancellationToken);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, cancellationToken))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task GetIdentityAsync_NameNotSet_ThrowsGitIdentityNotConfiguredExceptionForName()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(1, "", ""));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(0, "jane@example.com\n", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GitIdentityNotConfiguredException>()).Which;

        exception.MissingKeys
            .Should().Equal("user.name");

        exception.RepositoryRootPath
            .Should().Be(RepositoryRootPath);
    }

    [Fact]
    public async Task GetIdentityAsync_EmailNotSet_ThrowsGitIdentityNotConfiguredExceptionForEmail()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(0, "Jane Doe\n", ""));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(1, "", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GitIdentityNotConfiguredException>()).Which.MissingKeys
            .Should().Equal("user.email");
    }

    [Fact]
    public async Task GetIdentityAsync_NameAndEmailNotSet_ThrowsGitIdentityNotConfiguredExceptionForBoth()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(1, "", ""));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(1, "", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GitIdentityNotConfiguredException>()).Which.MissingKeys
            .Should().Equal("user.name", "user.email");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n")]
    public async Task GetIdentityAsync_EmptyValues_ThrowsGitIdentityNotConfiguredExceptionForBoth(
        string standardOutput)
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(0, standardOutput, ""));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(0, standardOutput, ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GitIdentityNotConfiguredException>()).Which.MissingKeys
            .Should().Equal("user.name", "user.email");
    }

    [Fact]
    public async Task GetIdentityAsync_UnexpectedExitCodeForName_ThrowsGitUnavailableException()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(3, "", "error: invalid config file .git/config\n"));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(0, "jane@example.com\n", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GitUnavailableException>()).Which;

        exception.Arguments
            .Should().Equal(UserNameArguments);

        exception.Reason
            .Should().Be("git exited with the unexpected exit code 3: error: invalid config file .git/config.");
    }

    [Fact]
    public async Task GetIdentityAsync_UnexpectedExitCodeForEmail_ThrowsGitUnavailableException()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(0, "Jane Doe\n", ""));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(128, "", "fatal: unable to read config\n"));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GitUnavailableException>()).Which.Arguments
            .Should().Equal(UserEmailArguments);
    }

    [Fact]
    public async Task GetIdentityAsync_UnexpectedExitCodeForNameAndEmail_ThrowsGitUnavailableExceptionForName()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(128, "", "fatal: unable to read config\n"));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(3, "", "error: invalid config file .git/config\n"));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<GitUnavailableException>()).Which;

        exception.Arguments
            .Should().Equal(UserNameArguments);

        exception.Reason
            .Should().Be("git exited with the unexpected exit code 128: fatal: unable to read config.");
    }

    [Fact]
    public async Task GetIdentityAsync_NameReadNotCompleted_StartsEmailReadWithoutWaiting()
    {
        // Arrange
        var nameResult =
            new TaskCompletionSource<ExternalProcessResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(UserNameArguments), A<CancellationToken>._))
            .Returns(nameResult.Task);
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(0, "jane@example.com\n", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var identityTask = sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(RepositoryRootPath,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(UserEmailArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        identityTask.IsCompleted
            .Should().BeFalse();

        nameResult.SetResult(new ExternalProcessResult(0, "Jane Doe\n", ""));

        (await identityTask)
            .Should().Be(new GitIdentity("Jane Doe", "jane@example.com"));
    }

    [Fact]
    public async Task GetIdentityAsync_NameWhitespaceAndEmailSet_ThrowsGitIdentityNotConfiguredExceptionForName()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(0, " \n", ""));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(0, "jane@example.com\n", ""));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GitIdentityNotConfiguredException>()).Which.MissingKeys
            .Should().Equal("user.name");
    }

    [Fact]
    public async Task GetIdentityAsync_ExitCode1WithOutput_TreatsKeyAsNotSet()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(UserNameArguments, new ExternalProcessResult(0, "Jane Doe\n", ""));
        _gitCommandRunner.SetUpResult(UserEmailArguments, new ExternalProcessResult(1, "unexpected\n", "some warning\n"));
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GitIdentityNotConfiguredException>()).Which.MissingKeys
            .Should().Equal("user.email");
    }

    [Fact]
    public async Task GetIdentityAsync_RunnerThrowsGitUnavailableException_PropagatesException()
    {
        // Arrange
        var runnerException = new GitUnavailableException(UserNameArguments, "git could not be started.");
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(runnerException);
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GitUnavailableException>()).Which
            .Should().BeSameAs(runnerException);
    }

    [Fact]
    public async Task GetIdentityAsync_RunnerCanceled_PropagatesOperationCanceledException()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(RepositoryRootPath, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetIdentityAsync_InvalidRepositoryRootPath_ThrowsArgumentException(string? repositoryRootPath)
    {
        // Arrange
        var sut = new GitRepositoryInfo(_gitCommandRunner);

        // Act
        var act = () => sut.GetIdentityAsync(repositoryRootPath!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        A.CallTo(_gitCommandRunner)
            .MustNotHaveHappened();
    }
}
