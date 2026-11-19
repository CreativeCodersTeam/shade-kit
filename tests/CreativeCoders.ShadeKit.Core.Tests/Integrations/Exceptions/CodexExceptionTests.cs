using System.ComponentModel;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Exceptions;

// The command text rules of the shared base class are covered by GitUnavailableExceptionTests.
public class CodexExceptionTests
{
    [Fact]
    public void CodexUnavailableException_ArgumentsReasonAndInnerException_MessageNamesCommandAndReason()
    {
        // Arrange
        var innerException = new Win32Exception("Permission denied");

        // Act
        var exception = new CodexUnavailableException(["plugin", "list", "--json"],
            "The codex executable '/usr/bin/codex' could not be started: Permission denied.", innerException);

        // Assert
        exception
            .Should().BeAssignableTo<ExternalToolException>();

        exception.Message
            .Should().Be("The codex command 'codex plugin list --json' cannot be run: The codex executable " +
                         "'/usr/bin/codex' could not be started: Permission denied.");

        exception.CommandText
            .Should().Be("codex plugin list --json");

        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void CodexNotFoundException_ArgumentsAndReason_IsCodexUnavailableExceptionWithoutInnerException()
    {
        // Act
        var exception = new CodexNotFoundException(["plugin", "list", "--json"],
            "The OpenAI Codex CLI codex was not found in any absolute directory of the PATH environment " +
            "variable.");

        // Assert
        exception
            .Should().BeAssignableTo<CodexUnavailableException>();

        exception.Message
            .Should().Be("The codex command 'codex plugin list --json' cannot be run: The OpenAI Codex CLI " +
                         "codex was not found in any absolute directory of the PATH environment variable.");

        exception.Arguments
            .Should().Equal("plugin", "list", "--json");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public void CodexCommandFailedException_AllValues_MessageNamesCommandAndReasonAndKeepsDetails()
    {
        // Arrange
        var innerException = new InvalidOperationException("inner");

        // Act
        var exception = new CodexCommandFailedException(
            ["plugin", "add", "shade-kit@creativecoders-shade-kit", "--json"], 1,
            "codex exited with the exit code 1: Error: Marketplace not found.", "Error: Marketplace not found",
            innerException);

        // Assert
        exception
            .Should().BeAssignableTo<ExternalToolException>()
            .And.NotBeAssignableTo<CodexUnavailableException>();

        exception.Message
            .Should().Be("The codex command 'codex plugin add shade-kit@creativecoders-shade-kit --json' failed: " +
                         "codex exited with the exit code 1: Error: Marketplace not found.");

        exception.ExitCode
            .Should().Be(1);

        exception.CodexMessage
            .Should().Be("Error: Marketplace not found");

        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void CodexCommandFailedException_RefusedBeforeStart_HasNullExitCodeMessageAndInnerException()
    {
        // Act
        var exception = new CodexCommandFailedException(["plugin", "marketplace", "add", "a&b"], null,
            "Argument 4 'a&b' contains the character '&', which is refused.");

        // Assert
        exception.ExitCode
            .Should().BeNull();

        exception.CodexMessage
            .Should().BeNull();

        exception.InnerException
            .Should().BeNull();
    }
}
