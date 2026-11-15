using System.ComponentModel;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Exceptions;

// The command text rules of the shared base class are covered by GitUnavailableExceptionTests.
public class CopilotExceptionTests
{
    [Fact]
    public void CopilotUnavailableException_ArgumentsReasonAndInnerException_MessageNamesCommandAndReason()
    {
        // Arrange
        var innerException = new Win32Exception("Permission denied");

        // Act
        var exception = new CopilotUnavailableException(["plugin", "list", "--json"],
            "The copilot executable '/usr/bin/copilot' could not be started: Permission denied.", innerException);

        // Assert
        exception
            .Should().BeAssignableTo<ExternalToolUnavailableException>();

        exception.Message
            .Should().Be("The copilot command 'copilot plugin list --json' cannot be run: The copilot executable " +
                         "'/usr/bin/copilot' could not be started: Permission denied.");

        exception.CommandText
            .Should().Be("copilot plugin list --json");

        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void CopilotNotFoundException_ArgumentsAndReason_IsCopilotUnavailableExceptionWithoutInnerException()
    {
        // Act
        var exception = new CopilotNotFoundException(["plugin", "list", "--json"],
            "The GitHub Copilot CLI copilot was not found in any absolute directory of the PATH environment " +
            "variable.");

        // Assert
        exception
            .Should().BeAssignableTo<CopilotUnavailableException>();

        exception.Message
            .Should().Be("The copilot command 'copilot plugin list --json' cannot be run: The GitHub Copilot CLI " +
                         "copilot was not found in any absolute directory of the PATH environment variable.");

        exception.Arguments
            .Should().Equal("plugin", "list", "--json");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public void CopilotCommandFailedException_AllValues_MessageNamesCommandAndReasonAndKeepsDetails()
    {
        // Arrange
        var innerException = new InvalidOperationException("inner");

        // Act
        var exception = new CopilotCommandFailedException(
            ["plugin", "install", "shade-kit@creativecoders-shade-kit"], 1,
            "copilot exited with the exit code 1: Error: Marketplace not found.", "Error: Marketplace not found",
            innerException);

        // Assert
        exception
            .Should().BeAssignableTo<ExternalToolUnavailableException>()
            .And.NotBeAssignableTo<CopilotUnavailableException>();

        exception.Message
            .Should().Be("The copilot command 'copilot plugin install shade-kit@creativecoders-shade-kit' failed: " +
                         "copilot exited with the exit code 1: Error: Marketplace not found.");

        exception.ExitCode
            .Should().Be(1);

        exception.CopilotMessage
            .Should().Be("Error: Marketplace not found");

        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void CopilotCommandFailedException_RefusedBeforeStart_HasNullExitCodeMessageAndInnerException()
    {
        // Act
        var exception = new CopilotCommandFailedException(["plugin", "marketplace", "add", "a&b"], null,
            "Argument 4 'a&b' contains the character '&', which is refused.");

        // Assert
        exception.ExitCode
            .Should().BeNull();

        exception.CopilotMessage
            .Should().BeNull();

        exception.InnerException
            .Should().BeNull();
    }
}
