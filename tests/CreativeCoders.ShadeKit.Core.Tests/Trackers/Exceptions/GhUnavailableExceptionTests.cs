using System.ComponentModel;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Trackers.Exceptions;

// The command text rules of the shared base class are covered by GitUnavailableExceptionTests.
public class GhUnavailableExceptionTests
{
    [Fact]
    public void Constructor_ArgumentsReasonAndInnerException_MessageNamesCommandAndReason()
    {
        // Arrange
        var innerException = new Win32Exception("No such file or directory");

        // Act
        var exception = new GhUnavailableException(["issue", "view", "https://github.com/octo/repo/issues/1"],
            "The gh executable '/usr/bin/gh' could not be started: No such file or directory.", innerException);

        // Assert
        exception.Message
            .Should().Be("The gh command 'gh issue view https://github.com/octo/repo/issues/1' cannot be run: The gh " +
                         "executable '/usr/bin/gh' could not be started: No such file or directory.");

        exception.CommandText
            .Should().Be("gh issue view https://github.com/octo/repo/issues/1");

        exception.Reason
            .Should().Be("The gh executable '/usr/bin/gh' could not be started: No such file or directory.");

        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void GhNotFoundException_ArgumentsAndReason_IsGhUnavailableExceptionWithoutInnerException()
    {
        // Act
        var exception = new GhNotFoundException(
            ["issue", "view", "https://github.com/octo/repo/issues/1", "--json", "title,body,comments"],
            "The GitHub CLI gh was not found in any absolute directory of the PATH environment variable.");

        // Assert
        exception
            .Should().BeAssignableTo<GhUnavailableException>();

        exception.Message
            .Should().Be("The gh command 'gh issue view https://github.com/octo/repo/issues/1 --json " +
                         "title,body,comments' cannot be run: The GitHub CLI gh was not found in any absolute " +
                         "directory of the PATH environment variable.");

        exception.Arguments
            .Should().Equal("issue", "view", "https://github.com/octo/repo/issues/1", "--json",
                "title,body,comments");

        exception.InnerException
            .Should().BeNull();
    }
}
