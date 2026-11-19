using System.ComponentModel;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Codex;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Codex;

// These tests never start the real codex: the locator is faked, and the only start attempt targets a file that does
// not exist. The .cmd shim rules are covered by ExternalToolRunnerTests and CmdShimCommandLineTests.
public class CodexCommandRunnerTests
{
    private const string NotFoundReason =
        "The OpenAI Codex CLI codex was not found in any absolute directory of the PATH environment variable.";

    private const string ComSpec = @"C:\Windows\System32\cmd.exe";

    private static readonly string[] ListArguments = ["plugin", "list", "--json"];

    private readonly ICodexExecutableLocator _codexExecutableLocator = A.Fake<ICodexExecutableLocator>();

    [Fact]
    public void Constructor_NullCodexExecutableLocator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CodexCommandRunner(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("codexExecutableLocator");
    }

    [Fact]
    public void EnvironmentVariables_Always_DisableColors()
    {
        // Act
        var environmentVariables = CodexCommandRunner.EnvironmentVariables;

        // Assert
        environmentVariables
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["NO_COLOR"] = "1" });
    }

    [Fact]
    public void Tool_Always_UsesCodexNameEnvironmentVariablesAndRunsCmdShims()
    {
        // Act
        var tool = CodexCommandRunner.Tool;

        // Assert
        tool.Name
            .Should().Be("codex");

        tool.EnvironmentVariables
            .Should().BeSameAs(CodexCommandRunner.EnvironmentVariables);

        tool.RunsCmdShims
            .Should().BeTrue();

        tool.NotFoundReason
            .Should().Be(NotFoundReason);
    }

    [Fact]
    public void Tool_CmdShimArgumentRefused_ThrowsCodexCommandFailedWithoutExitCode()
    {
        // Act
        var act = () => ExternalToolRunner.CreateProcessStart(CodexCommandRunner.Tool, @"C:\npm\codex.cmd",
            ["plugin", "marketplace", "add", "a&b"], static () => ComSpec);

        // Assert
        var exception = act.Should().Throw<CodexCommandFailedException>().Which;

        exception.Reason
            .Should().StartWith("Argument 4 'a&b' contains the character '&', which is refused: ");

        exception.ExitCode
            .Should().BeNull();

        exception.Arguments
            .Should().Equal("plugin", "marketplace", "add", "a&b");
    }

    [Fact]
    public void Tool_CmdShimWithoutComSpec_ThrowsCodexUnavailable()
    {
        // Act
        var act = () => ExternalToolRunner.CreateProcessStart(CodexCommandRunner.Tool, @"C:\npm\codex.cmd",
            ListArguments, static () => null);

        // Assert
        act.Should().ThrowExactly<CodexUnavailableException>();
    }

    [Fact]
    public void Tool_CmdShimWithSafeArguments_StartsComSpecWithShimCommandLine()
    {
        // Act
        var start = ExternalToolRunner.CreateProcessStart(CodexCommandRunner.Tool, @"C:\npm\codex.cmd",
            ListArguments, static () => ComSpec);

        // Assert
        start.ExecutablePath
            .Should().Be(ComSpec);

        start.VerbatimArguments
            .Should().Contain(@"C:\npm\codex.cmd").And.Contain("plugin list --json");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RunAsync_CodexNotFound_ThrowsCodexNotFoundException(string? codexExecutablePath)
    {
        // Arrange
        A.CallTo(() => _codexExecutableLocator.FindCodexExecutable()).Returns(codexExecutablePath);
        var sut = new CodexCommandRunner(_codexExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), ListArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexNotFoundException>()).Which;

        exception.Arguments
            .Should().Equal(ListArguments);

        exception.Reason
            .Should().Be(NotFoundReason);

        exception.InnerException
            .Should().BeNull();
    }

    [Theory]
    [InlineData("codex")]
    [InlineData("./codex")]
    [InlineData("bin/codex")]
    [InlineData("   ")]
    public async Task RunAsync_CodexLocatorReturnsRelativePath_ThrowsCodexUnavailableExceptionWithoutStarting(
        string relativePath)
    {
        // Arrange
        A.CallTo(() => _codexExecutableLocator.FindCodexExecutable()).Returns(relativePath);
        var sut = new CodexCommandRunner(_codexExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), ListArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowExactlyAsync<CodexUnavailableException>()).Which;

        exception.Reason
            .Should().Be($"The codex executable path '{relativePath}' is not absolute, so it is not started.");
    }

    [Fact]
    public async Task RunAsync_ExecutableCannotBeStarted_ThrowsCodexUnavailableExceptionWithStartFailure()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var missingExecutablePath = Path.Combine(tempDirectory.RootPath, "missing", "codex");

        A.CallTo(() => _codexExecutableLocator.FindCodexExecutable()).Returns(missingExecutablePath);
        var sut = new CodexCommandRunner(_codexExecutableLocator);

        // Act
        var act = () => sut.RunAsync(tempDirectory.RootPath, ListArguments, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowExactlyAsync<CodexUnavailableException>()).Which;

        exception.Arguments
            .Should().Equal(ListArguments);

        exception.Reason
            .Should().StartWith($"The codex executable '{missingExecutablePath}' could not be started: ")
            .And.EndWith(".");

        exception.InnerException
            .Should().BeOfType<Win32Exception>();
    }

    [Fact]
    public async Task RunAsync_CanceledToken_ThrowsOperationCanceledExceptionWithoutLocatingCodex()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var sut = new CodexCommandRunner(_codexExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), ListArguments, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(() => _codexExecutableLocator.FindCodexExecutable())
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunAsync_InvalidWorkingDirectory_ThrowsArgumentException(string? workingDirectory)
    {
        // Arrange
        var sut = new CodexCommandRunner(_codexExecutableLocator);

        // Act
        var act = () => sut.RunAsync(workingDirectory!, ListArguments, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        A.CallTo(() => _codexExecutableLocator.FindCodexExecutable())
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task RunAsync_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new CodexCommandRunner(_codexExecutableLocator);

        // Act
        var act = () => sut.RunAsync(Path.GetTempPath(), null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("arguments");

        A.CallTo(() => _codexExecutableLocator.FindCodexExecutable())
            .MustNotHaveHappened();
    }
}
