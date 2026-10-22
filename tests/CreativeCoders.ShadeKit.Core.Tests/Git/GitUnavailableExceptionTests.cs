using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git;

namespace CreativeCoders.ShadeKit.Core.Tests.Git;

public class GitUnavailableExceptionTests
{
    [Fact]
    public void Constructor_ArgumentsAndReason_MessageNamesCommandAndReason()
    {
        // Act
        var exception = new GitUnavailableException(["symbolic-ref", "--short", "-q", "HEAD"],
            "git exited with the unexpected exit code 128.");

        // Assert
        exception.Message
            .Should().Be("The git command 'git symbolic-ref --short -q HEAD' failed: " +
                         "git exited with the unexpected exit code 128.");

        exception.CommandText
            .Should().Be("git symbolic-ref --short -q HEAD");

        exception.Arguments
            .Should().Equal("symbolic-ref", "--short", "-q", "HEAD");

        exception.Reason
            .Should().Be("git exited with the unexpected exit code 128.");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public void Constructor_InnerException_KeepsInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("inner");

        // Act
        var exception = new GitUnavailableException(["config", "user.name"], "git could not be started: inner",
            innerException);

        // Assert
        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void Constructor_ArgumentsWithWhitespaceOrEmpty_CommandTextQuotesThem()
    {
        // Act
        var exception = new GitUnavailableException(["config", "user name", ""], "reason");

        // Assert
        exception.CommandText
            .Should().Be("git config \"user name\" \"\"");
    }

    [Fact]
    public void Constructor_NoArguments_CommandTextIsGit()
    {
        // Act
        var exception = new GitUnavailableException([], "reason");

        // Assert
        exception.CommandText
            .Should().Be("git");
    }

    [Fact]
    public void Constructor_ArgumentsChangedAfterwards_KeepsCopyOfArguments()
    {
        // Arrange
        var arguments = new List<string> { "config", "user.name" };

        // Act
        var exception = new GitUnavailableException(arguments, "reason");
        arguments.Add("changed");

        // Assert
        exception.Arguments
            .Should().Equal("config", "user.name");
    }

    [Fact]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitUnavailableException(null!, "reason");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
