using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Web.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Web.Exceptions;

public class UrlContentReadFailedExceptionTests
{
    [Fact]
    public void Constructor_UrlAndReason_MessageNamesUrlAndReason()
    {
        // Arrange
        var url = new Uri("https://example.com/docs/request.md");

        // Act
        var exception = new UrlContentReadFailedException(url, "The server redirected more than 10 times.");

        // Assert
        exception.Message
            .Should().Be("The content of the URL 'https://example.com/docs/request.md' cannot be read: The server " +
                         "redirected more than 10 times.");

        exception.Url
            .Should().BeSameAs(url);

        exception.Reason
            .Should().Be("The server redirected more than 10 times.");

        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public void Constructor_InnerException_KeepsInnerException()
    {
        // Arrange
        var innerException = new HttpRequestException("No such host is known.");

        // Act
        var exception = new UrlContentReadFailedException(new Uri("https://example.com/"),
            "The request failed: No such host is known.", innerException);

        // Assert
        exception.InnerException
            .Should().BeSameAs(innerException);
    }

    [Fact]
    public void Constructor_NullUrl_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UrlContentReadFailedException(null!, "Reason.");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
