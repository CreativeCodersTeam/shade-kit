using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Processes;

public class ExternalToolRunnerTests
{
    private const string ComSpec = @"C:\Windows\System32\cmd.exe";

    private const string ShimPath = @"C:\Users\Jane Doe\AppData\Roaming\npm\copilot.cmd";

    // A tool that runs .cmd shims; the Claude exceptions stand in for the exceptions of a real tool.
    private static readonly ExternalTool ShimTool = new("tool", "The Example CLI tool", null,
        static (arguments, reason) => new ClaudeNotFoundException(arguments, reason),
        static (arguments, reason, innerException) =>
            new ClaudeUnavailableException(arguments, reason, innerException),
        static (arguments, reason) => new ClaudeCommandFailedException(arguments, 0, reason));

    // A tool without the .cmd opt-in, like git, gh, and claude.
    private static readonly ExternalTool DirectTool = ShimTool with { CreateCommandFailedException = null };

    [Fact]
    public async Task RunAsync_ExecutableNotFound_PassesNotFoundReasonToTheToolException()
    {
        // Arrange
        var tool = new ExternalTool("tool", "The Example CLI tool", null,
            static (arguments, reason) => new GitNotFoundException(arguments, reason),
            static (_, _, _) => throw new NotSupportedException());

        // Act
        var act = () => ExternalToolRunner.RunAsync(tool, static () => null, Path.GetTempPath(), ["x"],
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act
            .Should().ThrowExactlyAsync<GitNotFoundException>()).Which;

        exception.Reason
            .Should().Be(tool.NotFoundReason);

        exception.Arguments
            .Should().Equal("x");
    }

    [Fact]
    public async Task RunAsync_CmdShimWithRefusedArgument_ThrowsCommandFailedBeforeStarting()
    {
        // Arrange
        // A host-qualified path that does not exist: if the runner started it, the start would fail with a
        // ClaudeUnavailableException instead.
        var shimPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "copilot.cmd");

        // Act
        var act = () => ExternalToolRunner.RunAsync(ShimTool, () => shimPath, static () => ComSpec,
            Path.GetTempPath(), ["plugin", "a&b"], TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act
            .Should().ThrowExactlyAsync<ClaudeCommandFailedException>()).Which;

        exception.Reason
            .Should().StartWith("Argument 2 'a&b' contains the character '&', which is refused: ");

        exception.Arguments
            .Should().Equal("plugin", "a&b");
    }

    [Fact]
    public async Task RunAsync_CmdShimWithoutComSpec_ThrowsUnavailableBeforeStarting()
    {
        // Arrange
        var shimPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "copilot.cmd");

        // Act
        var act = () => ExternalToolRunner.RunAsync(ShimTool, () => shimPath, static () => null,
            Path.GetTempPath(), ["plugin", "list"], TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act
            .Should().ThrowExactlyAsync<ClaudeUnavailableException>()).Which;

        exception.Reason
            .Should().Be($"The ComSpec environment variable is not set, so the .cmd shim '{shimPath}' cannot be " +
                         "run through cmd.exe.");
    }

    [Fact]
    public async Task RunAsync_CmdShimWithWindowsComSpecOnUnix_ThrowsUnavailableBeforeStarting()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(),
            "On Windows the ComSpec path is absolute, so cmd.exe would really be started.");

        var shimPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "copilot.cmd");

        // Act
        var act = () => ExternalToolRunner.RunAsync(ShimTool, () => shimPath, static () => ComSpec,
            Path.GetTempPath(), ["plugin", "list"], TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act
            .Should().ThrowExactlyAsync<ClaudeUnavailableException>()).Which;

        exception.Reason
            .Should().Be($"The command interpreter path '{ComSpec}' of the tool .cmd shim '{shimPath}' is not " +
                         "absolute on this operating system, so it is not started.");
    }

    [Fact]
    public void CreateProcessStart_ExeWithShimTool_StartsExeDirectlyWithoutReadingComSpec()
    {
        // Arrange
        var comSpecReads = 0;

        // Act
        var start = ExternalToolRunner.CreateProcessStart(ShimTool, @"C:\npm\copilot.exe", ["plugin", "a&b"], () =>
        {
            comSpecReads++;

            return ComSpec;
        });

        // Assert
        start.ExecutablePath
            .Should().Be(@"C:\npm\copilot.exe");

        start.Arguments
            .Should().Equal("plugin", "a&b");

        start.VerbatimArguments
            .Should().BeNull();

        comSpecReads
            .Should().Be(0);
    }

    [Fact]
    public void CreateProcessStart_UnixExecutableWithShimTool_StartsItDirectly()
    {
        // Act
        var start = ExternalToolRunner.CreateProcessStart(ShimTool, "/usr/local/bin/copilot", ["plugin", "list"],
            static () => null);

        // Assert
        start.ExecutablePath
            .Should().Be("/usr/local/bin/copilot");

        start.Arguments
            .Should().Equal("plugin", "list");

        start.VerbatimArguments
            .Should().BeNull();
    }

    [Fact]
    public void CreateProcessStart_CmdWithToolWithoutOptIn_StartsItDirectlyAsBefore()
    {
        // Act
        var start = ExternalToolRunner.CreateProcessStart(DirectTool, ShimPath, ["a&b"], static () => null);

        // Assert
        start.ExecutablePath
            .Should().Be(ShimPath);

        start.Arguments
            .Should().Equal("a&b");

        start.VerbatimArguments
            .Should().BeNull();
    }

    [Theory]
    [InlineData(@"C:\Users\Jane Doe\AppData\Roaming\npm\copilot.cmd")]
    [InlineData(@"C:\Users\Jane Doe\AppData\Roaming\npm\COPILOT.CMD")]
    public void CreateProcessStart_CmdShimWithValidComSpec_StartsComSpecWithVerbatimCommandLine(string shimPath)
    {
        // Act
        var start = ExternalToolRunner.CreateProcessStart(ShimTool, shimPath,
            ["plugin", "marketplace", "add", @"C:\my plugins\kit"], static () => ComSpec);

        // Assert
        start.ExecutablePath
            .Should().Be(ComSpec);

        start.Arguments
            .Should().BeEmpty();

        start.VerbatimArguments
            .Should().Be($"/d /s /c \"\"{shimPath}\" plugin marketplace add \"C:\\my plugins\\kit\"\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("cmd.exe")]
    [InlineData(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe")]
    public void CreateProcessStart_CmdShimWithUnusableComSpec_ThrowsTheToolsUnavailableException(string? comSpec)
    {
        // Arrange
        CmdShimCommandLine.IsValidComSpec(comSpec, ShimPath, out var expectedReason);

        // Act
        var act = () => ExternalToolRunner.CreateProcessStart(ShimTool, ShimPath, ["plugin", "list"],
            () => comSpec);

        // Assert
        var exception = act
            .Should().ThrowExactly<ClaudeUnavailableException>().Which;

        exception.Reason
            .Should().Be(expectedReason);

        exception.Arguments
            .Should().Equal("plugin", "list");
    }

    [Fact]
    public void CreateProcessStart_CmdShimWithRefusedArgument_ThrowsTheToolsCommandFailedException()
    {
        // Act
        var act = () => ExternalToolRunner.CreateProcessStart(ShimTool, ShimPath, ["plugin", "install", "%x%"],
            static () => ComSpec);

        // Assert
        var exception = act
            .Should().ThrowExactly<ClaudeCommandFailedException>().Which;

        exception.Reason
            .Should().StartWith("Argument 3 '%x%' contains the character '%', which is refused: ");

        exception.Arguments
            .Should().Equal("plugin", "install", "%x%");
    }

    [Fact]
    public void CreateProcessStart_CmdShimPathWithRefusedCharacter_ThrowsTheToolsCommandFailedException()
    {
        // Arrange
        const string shimPath = @"C:\Program Files (x86)\npm\copilot.cmd";

        // Act
        var act = () => ExternalToolRunner.CreateProcessStart(ShimTool, shimPath, ["plugin", "list"],
            static () => ComSpec);

        // Assert
        act.Should().ThrowExactly<ClaudeCommandFailedException>()
            .Which.Reason
            .Should().StartWith($"The path of the .cmd shim '{shimPath}' contains the character '('");
    }

    [Fact]
    public void CreateProcessStart_UnusableComSpecAndRefusedArgument_ReportsTheComSpecFirst()
    {
        // Act
        var act = () => ExternalToolRunner.CreateProcessStart(ShimTool, ShimPath, ["a&b"], static () => null);

        // Assert
        act.Should().ThrowExactly<ClaudeUnavailableException>();
    }
}
