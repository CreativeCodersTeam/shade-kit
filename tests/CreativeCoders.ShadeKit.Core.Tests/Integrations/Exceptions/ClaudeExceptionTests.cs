using System.ComponentModel;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Exceptions;

// The command text rules of the shared base class are covered by GitUnavailableExceptionTests.
public class ClaudeExceptionTests
{
    [Fact]
    public void ClaudeUnavailableException_ArgumentsReasonAndInnerException_MessageNamesCommandAndReason()
    {
        // Arrange
        var innerException = new Win32Exception("Permission denied");

        // Act
        var exception = new ClaudeUnavailableException(["plugin", "list", "--json"],
            "The claude executable '/usr/bin/claude' could not be started: Permission denied.", innerException);

        // Assert
        exception.Message
            .Should().Be("The claude command 'claude plugin list --json' cannot be run: The claude executable " +
                         "'/usr/bin/claude' could not be started: Permission denied.");

        exception.CommandText
            .Should().Be("claude plugin list --json");

        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void ClaudeNotFoundException_ArgumentsAndReason_IsClaudeUnavailableExceptionWithoutInnerException()
    {
        // Act
        var exception = new ClaudeNotFoundException(["plugin", "list", "--json"],
            "The Claude Code CLI claude was not found in any absolute directory of the PATH environment variable.");

        // Assert
        exception
            .Should().BeAssignableTo<ClaudeUnavailableException>();

        exception.Message
            .Should().Be("The claude command 'claude plugin list --json' cannot be run: The Claude Code CLI claude " +
                         "was not found in any absolute directory of the PATH environment variable.");

        exception.Arguments
            .Should().Equal("plugin", "list", "--json");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public void ClaudeCommandFailedException_AllValues_MessageNamesCommandAndReasonAndKeepsClaudeDetails()
    {
        // Arrange
        var innerException = new JsonException("inner");

        // Act
        var exception = new ClaudeCommandFailedException(
            ["plugin", "update", "nope@creativecoders-shade-kit", "--scope", "user", "--json"], 1,
            "claude exited with the exit code 1 and reported the outcome 'failed': Plugin \"nope\" not found " +
            "(failure code 'not_found').", "Plugin \"nope\" not found", "not_found", innerException);

        // Assert
        exception.Message
            .Should().Be("The claude command 'claude plugin update nope@creativecoders-shade-kit --scope user " +
                         "--json' failed: claude exited with the exit code 1 and reported the outcome 'failed': " +
                         "Plugin \"nope\" not found (failure code 'not_found').");

        exception.ExitCode
            .Should().Be(1);

        exception.ClaudeMessage
            .Should().Be("Plugin \"nope\" not found");

        exception.FailureCode
            .Should().Be("not_found");

        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void ClaudeCommandFailedException_WithoutClaudeDetails_HasNullMessageFailureCodeAndInnerException()
    {
        // Act
        var exception = new ClaudeCommandFailedException(["plugin", "list", "--json"], 0,
            "The output of claude is not a JSON array.");

        // Assert
        exception
            .Should().NotBeAssignableTo<ClaudeUnavailableException>();

        exception.ClaudeMessage
            .Should().BeNull();

        exception.FailureCode
            .Should().BeNull();

        exception.InnerException
            .Should().BeNull();
    }
}
