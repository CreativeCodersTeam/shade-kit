using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Processes;

public class ExternalToolTests
{
    [Fact]
    public void NotFoundReason_NameDiffersFromDisplayName_StartsWithDisplayName()
    {
        // Arrange
        var tool = new ExternalTool("tool", "The Example CLI tool", null,
            static (_, _) => throw new NotSupportedException(),
            static (_, _, _) => throw new NotSupportedException());

        // Act
        var reason = tool.NotFoundReason;

        // Assert
        reason
            .Should().Be(
                "The Example CLI tool was not found in any absolute directory of the PATH environment variable.");
    }

    [Fact]
    public void RunsCmdShims_WithoutCmdShimPolicy_IsFalse()
    {
        // Arrange
        var tool = new ExternalTool("tool", "The Example CLI tool", null,
            static (_, _) => throw new NotSupportedException(),
            static (_, _, _) => throw new NotSupportedException());

        // Act
        var runsCmdShims = tool.RunsCmdShims;

        // Assert
        runsCmdShims
            .Should().BeFalse();
    }

    [Fact]
    public void RunsCmdShims_WithCmdShimPolicy_IsTrue()
    {
        // Arrange
        var tool = new ExternalTool("tool", "The Example CLI tool", null,
            static (_, _) => throw new NotSupportedException(),
            static (_, _, _) => throw new NotSupportedException(),
            new CmdShimPolicy(static (_, _) => throw new NotSupportedException()));

        // Act
        var runsCmdShims = tool.RunsCmdShims;

        // Assert
        runsCmdShims
            .Should().BeTrue();
    }
}
