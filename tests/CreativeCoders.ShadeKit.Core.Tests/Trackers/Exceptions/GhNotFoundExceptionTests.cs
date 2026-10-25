using System.ComponentModel;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Trackers.Exceptions;

public class GhNotFoundExceptionTests
{
    [Fact]
    public void Constructor_ArgumentsAndReason_MessageNamesCommandAndReason()
    {
        // Act
        var exception = new GhNotFoundException(
            ["issue", "view", "https://github.com/octo/repo/issues/1", "--json", "title,body,comments"],
            "The GitHub CLI gh was not found in any absolute directory of the PATH environment variable.");

        // Assert
        exception.Message
            .Should().Be("The gh command 'gh issue view https://github.com/octo/repo/issues/1 --json " +
                         "title,body,comments' cannot be run: The GitHub CLI gh was not found in any absolute " +
                         "directory of the PATH environment variable.");

        exception.CommandText
            .Should().Be("gh issue view https://github.com/octo/repo/issues/1 --json title,body,comments");

        exception.Arguments
            .Should().Equal("issue", "view", "https://github.com/octo/repo/issues/1", "--json",
                "title,body,comments");

        exception.Reason
            .Should().Be("The GitHub CLI gh was not found in any absolute directory of the PATH environment variable.");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public void Constructor_InnerException_KeepsInnerException()
    {
        // Arrange
        var innerException = new Win32Exception("No such file or directory");

        // Act
        var exception = new GhNotFoundException(["issue", "view"],
            "The gh executable '/usr/bin/gh' could not be started: No such file or directory.", innerException);

        // Assert
        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void Constructor_ArgumentsWithWhitespaceOrEmpty_CommandTextQuotesThem()
    {
        // Act
        var exception = new GhNotFoundException(["issue", "view", "a b", ""], "Reason.");

        // Assert
        exception.CommandText
            .Should().Be("gh issue view \"a b\" \"\"");
    }

    [Fact]
    public void Constructor_NoArguments_CommandTextIsGh()
    {
        // Act
        var exception = new GhNotFoundException([], "Reason.");

        // Assert
        exception.CommandText
            .Should().Be("gh");
    }

    [Fact]
    public void Constructor_ArgumentsChangedAfterwards_KeepsCopyOfArguments()
    {
        // Arrange
        var arguments = new List<string> { "issue", "view" };

        // Act
        var exception = new GhNotFoundException(arguments, "Reason.");
        arguments.Add("changed");

        // Assert
        exception.Arguments
            .Should().Equal("issue", "view");
    }

    [Fact]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GhNotFoundException(null!, "Reason.");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
