using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Trackers.Exceptions;

public class TrackerIssueReadFailedExceptionTests
{
    [Fact]
    public void Constructor_IssueUrlAndReason_MessageNamesIssueUrlAndReason()
    {
        // Arrange
        var issueUrl = new Uri("https://github.com/octo/repo/issues/42");

        // Act
        var exception = new TrackerIssueReadFailedException(issueUrl, "The gh output has no title string.");

        // Assert
        exception.Message
            .Should().Be("The issue 'https://github.com/octo/repo/issues/42' cannot be read: The gh output has no " +
                         "title string.");

        exception.IssueUrl
            .Should().BeSameAs(issueUrl);

        exception.Reason
            .Should().Be("The gh output has no title string.");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public void Constructor_InnerException_KeepsInnerException()
    {
        // Arrange
        var innerException = new JsonException("Unexpected end of data.");

        // Act
        var exception = new TrackerIssueReadFailedException(new Uri("https://github.com/octo/repo/issues/42"),
            "The gh output is not valid JSON: Unexpected end of data.", innerException);

        // Assert
        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void Constructor_RelativeIssueUrl_MessageNamesOriginalString()
    {
        // Act
        var exception = new TrackerIssueReadFailedException(new Uri("issues/42", UriKind.Relative), "Reason.");

        // Assert
        exception.Message
            .Should().Be("The issue 'issues/42' cannot be read: Reason.");
    }

    [Fact]
    public void Constructor_NullIssueUrl_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TrackerIssueReadFailedException(null!, "Reason.");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
