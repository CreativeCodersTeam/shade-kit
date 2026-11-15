using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Processes;

public class ExternalToolRunnerTests
{
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
}
