using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Git;

// The git runner is faked, so these tests never start git or reach the network.
public class GitRemoteTagCheckerTests
{
    private const string WorkingDirectory = "/home/me";

    private const string RemoteUrl = "https://github.com/CreativeCodersTeam/shade-kit.git";

    private static readonly string[] LsRemoteArguments =
    [
        "-c", "credential.helper=", "-c", "protocol.allow=never", "-c", "protocol.https.allow=always",
        "ls-remote", "--tags", RemoteUrl, "refs/tags/v0.8.0"
    ];

    private readonly IGitCommandRunner _gitCommandRunner = A.Fake<IGitCommandRunner>();

    [Fact]
    public void Constructor_NullGitCommandRunner_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitRemoteTagChecker(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitCommandRunner");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidWorkingDirectory_ThrowsArgumentException(string? workingDirectory)
    {
        // Act
        var act = () => new GitRemoteTagChecker(_gitCommandRunner, workingDirectory!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(workingDirectory));
    }

    [Fact]
    public void Constructor_DefaultWorkingDirectory_UsesExistingHomeOrTemporaryDirectory()
    {
        // Act
        var sut = new GitRemoteTagChecker(_gitCommandRunner);

        // Assert
        sut.WorkingDirectory
            .Should().BeOneOf(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Path.GetTempPath());
    }

    [Fact]
    public async Task CheckTagAsync_Always_RunsLsRemoteWithoutCredentialHelpersAndOnlyHttpsInWorkingDirectory()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        _gitCommandRunner.SetUpResult(LsRemoteArguments, new ExternalProcessResult(0, string.Empty, string.Empty));
        var sut = CreateSut();

        // Act
        await sut.CheckTagAsync(RemoteUrl, "v0.8.0", cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(WorkingDirectory,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(LsRemoteArguments), cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("4a1b2c3d\trefs/tags/v0.8.0\n")]
    [InlineData("4a1b2c3d\trefs/tags/v0.8.0\r\n")]
    [InlineData("4a1b2c3d\trefs/tags/v0.8.0\n9f8e7d6c\trefs/tags/v0.8.0^{}\n")]
    [InlineData("9f8e7d6c\trefs/tags/v0.8.0^{}")]
    public async Task CheckTagAsync_GitListsTag_ReturnsFound(string standardOutput)
    {
        // Arrange
        _gitCommandRunner.SetUpResult(LsRemoteArguments, new ExternalProcessResult(0, standardOutput, string.Empty));
        var sut = CreateSut();

        // Act
        var state = await sut.CheckTagAsync(RemoteUrl, "v0.8.0", TestContext.Current.CancellationToken);

        // Assert
        state
            .Should().Be(GitRemoteTagState.Found);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("4a1b2c3d\trefs/tags/v0.8.0-beta.1\n")]
    [InlineData("4a1b2c3d\trefs/heads/refs/tags/v0.8.0\n")]
    [InlineData("4a1b2c3d refs/tags/v0.8.0\n")]
    public async Task CheckTagAsync_GitListsNoMatchingTag_ReturnsMissing(string standardOutput)
    {
        // Arrange
        _gitCommandRunner.SetUpResult(LsRemoteArguments, new ExternalProcessResult(0, standardOutput, string.Empty));
        var sut = CreateSut();

        // Act
        var state = await sut.CheckTagAsync(RemoteUrl, "v0.8.0", TestContext.Current.CancellationToken);

        // Assert
        state
            .Should().Be(GitRemoteTagState.Missing);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(128)]
    [InlineData(-1)]
    public async Task CheckTagAsync_GitFails_ReturnsUnknown(int exitCode)
    {
        // Arrange
        _gitCommandRunner.SetUpResult(LsRemoteArguments, new ExternalProcessResult(exitCode,
            "4a1b2c3d\trefs/tags/v0.8.0\n", "fatal: unable to access the repository"));
        var sut = CreateSut();

        // Act
        var state = await sut.CheckTagAsync(RemoteUrl, "v0.8.0", TestContext.Current.CancellationToken);

        // Assert
        state
            .Should().Be(GitRemoteTagState.Unknown);
    }

    [Fact]
    public async Task CheckTagAsync_GitNotFound_ReturnsUnknown()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(new GitNotFoundException(LsRemoteArguments, "git was not found."));
        var sut = CreateSut();

        // Act
        var state = await sut.CheckTagAsync(RemoteUrl, "v0.8.0", TestContext.Current.CancellationToken);

        // Assert
        state
            .Should().Be(GitRemoteTagState.Unknown);
    }

    [Fact]
    public async Task CheckTagAsync_GitCannotBeStarted_ReturnsUnknown()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(new GitUnavailableException(LsRemoteArguments, "git could not be started."));
        var sut = CreateSut();

        // Act
        var state = await sut.CheckTagAsync(RemoteUrl, "v0.8.0", TestContext.Current.CancellationToken);

        // Assert
        state
            .Should().Be(GitRemoteTagState.Unknown);
    }

    [Fact]
    public async Task CheckTagAsync_CanceledWhileGitRuns_PropagatesCancellationInsteadOfUnknown()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ReturnsLazily(async (string _, IReadOnlyList<string> _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return new ExternalProcessResult(0, string.Empty, string.Empty);
            });
        var sut = CreateSut();

        // Act
        var task = sut.CheckTagAsync(RemoteUrl, "v0.8.0", cancellationTokenSource.Token);
        await cancellationTokenSource.CancelAsync();

        // Assert
        await task.Awaiting(x => x)
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CheckTagAsync_Canceled_PropagatesCancellation()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());
        var sut = CreateSut();

        // Act
        var act = () => sut.CheckTagAsync(RemoteUrl, "v0.8.0", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CheckTagAsync_UnexpectedFailure_PropagatesFailure()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("Disk"));
        var sut = CreateSut();

        // Act
        var act = () => sut.CheckTagAsync(RemoteUrl, "v0.8.0", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task CheckTagAsync_NullRemoteUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.CheckTagAsync(null!, "v0.8.0", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("remoteUrl");
    }

    [Fact]
    public async Task CheckTagAsync_NullTagName_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.CheckTagAsync(RemoteUrl, null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("tagName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("github.com/CreativeCodersTeam/shade-kit.git")]
    [InlineData("http://github.com/CreativeCodersTeam/shade-kit.git")]
    [InlineData("git@github.com:CreativeCodersTeam/shade-kit.git")]
    [InlineData("--upload-pack=x")]
    public async Task CheckTagAsync_RemoteUrlNotHttps_ThrowsArgumentExceptionWithoutRunningGit(string remoteUrl)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.CheckTagAsync(remoteUrl, "v0.8.0", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName(nameof(remoteUrl));

        A.CallTo(_gitCommandRunner)
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("")]
    [InlineData("-v0.8.0")]
    [InlineData("v0.8 .0")]
    [InlineData("v0.*")]
    [InlineData("v0.8.?")]
    [InlineData("v[0]")]
    [InlineData("v0\\8")]
    [InlineData("v0.8.0^{}")]
    [InlineData("v0.8.0~1")]
    [InlineData("v0:8")]
    public async Task CheckTagAsync_InvalidTagName_ThrowsArgumentExceptionWithoutRunningGit(string tagName)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.CheckTagAsync(RemoteUrl, tagName, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName(nameof(tagName));

        A.CallTo(_gitCommandRunner)
            .MustNotHaveHappened();
    }

    private GitRemoteTagChecker CreateSut()
    {
        return new GitRemoteTagChecker(_gitCommandRunner, WorkingDirectory);
    }
}
