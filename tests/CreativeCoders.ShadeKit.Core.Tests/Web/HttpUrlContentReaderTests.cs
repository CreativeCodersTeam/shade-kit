using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Web;
using CreativeCoders.ShadeKit.Core.Web.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Web;

// These tests never use the network: the client factory is faked and returns clients over a stub handler.
public class HttpUrlContentReaderTests
{
    private const string RequestUrl = "https://example.com/docs/request.md";

    private const int MaxContentLength = 1_048_576;

    private readonly IHttpClientFactory _httpClientFactory = A.Fake<IHttpClientFactory>();

    [Fact]
    public void Constructor_NullHttpClientFactory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new HttpUrlContentReader(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClientFactory");
    }

    [Fact]
    public async Task ReadAsync_NullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new HttpUrlContentReader(_httpClientFactory);

        // Act
        var act = () => sut.ReadAsync(null!, MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("url");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NonAuthoritativeInformation)]
    [InlineData((HttpStatusCode)299)]
    public async Task ReadAsync_SuccessStatusCode_ReturnsBodyText(HttpStatusCode statusCode)
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ =>
            CreateResponse("# Login with SSO\n\nUsers sign in with Ümlaut.\n"u8.ToArray(), statusCode: statusCode));
        var sut = CreateSut(handler);

        // Act
        var text = await sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        text
            .Should().Be("# Login with SSO\n\nUsers sign in with Ümlaut.\n");

        handler.Requests
            .Should().ContainSingle()
            .Which.Should().Match<HttpRequestMessage>(x =>
                x.Method == HttpMethod.Get && x.RequestUri == new Uri(RequestUrl));

        A.CallTo(() => _httpClientFactory.CreateClient(HttpUrlContentReader.HttpClientName))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReadAsync_BodyWithUtf8Bom_ReturnsTextWithoutBom()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ =>
            CreateResponse([.. TestText.Utf8Bom, .. "Login with SSO\n"u8.ToArray()]));
        var sut = CreateSut(handler);

        // Act
        var text = await sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        text
            .Should().Be("Login with SSO\n");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "404 (Not Found)")]
    [InlineData(HttpStatusCode.Forbidden, "403 (Forbidden)")]
    [InlineData(HttpStatusCode.InternalServerError, "500 (Internal Server Error)")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "503 (Service Unavailable)")]
    [InlineData(HttpStatusCode.NotModified, "304 (Not Modified)")]
    [InlineData(HttpStatusCode.MultipleChoices, "300 (Multiple Choices)")]
    [InlineData(HttpStatusCode.Continue, "100 (Continue)")]
    public async Task ReadAsync_NonSuccessStatusCode_ThrowsUrlContentReadFailedException(HttpStatusCode statusCode,
        string expectedStatusText)
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => CreateResponse("Error"u8.ToArray(), statusCode: statusCode));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which;

        exception.Url
            .Should().Be(new Uri(RequestUrl));

        exception.Reason
            .Should().Be($"The server answered with the status code {expectedStatusText}, not with a success " +
                         "status code (2xx).");

        exception.Message
            .Should().Be($"The content of the URL '{RequestUrl}' cannot be read: {exception.Reason}");
    }

    [Fact]
    public async Task ReadAsync_StatusCodeWithoutReasonPhrase_ReasonContainsOnlyStatusCode()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ =>
        {
            var response = CreateResponse("Error"u8.ToArray(), statusCode: (HttpStatusCode)599);
            response.ReasonPhrase = null;

            return response;
        });
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be("The server answered with the status code 599, not with a success status code (2xx).");
    }

    [Fact]
    public async Task ReadAsync_ServerDoesNotAnswerWithinTimeout_ThrowsUrlContentReadFailedException()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);

            return CreateResponse([]);
        });
        var sut = CreateSut(handler, TimeSpan.FromMilliseconds(100));

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which;

        exception.Reason
            .Should().Be("The server did not answer completely within 0.1 seconds.");

        exception.InnerException
            .Should().BeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadAsync_HandlerThrowsTimeoutCancellation_ThrowsUrlContentReadFailedException()
    {
        // Arrange
        var timeoutException = new TaskCanceledException("Timed out.", new TimeoutException());
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromException<HttpResponseMessage>(
            timeoutException));
        var sut = CreateSut(handler, TimeSpan.FromSeconds(30));

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which;

        exception.Reason
            .Should().Be("The server did not answer completely within 30 seconds.");

        exception.InnerException
            .Should().BeSameAs(timeoutException);
    }

    [Fact]
    public async Task ReadAsync_BodyNotCompleteWithinTimeout_ThrowsUrlContentReadFailedException()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => CreateResponse(new StreamContent(new TestStream(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);

                return 0;
            }))));
        var sut = CreateSut(handler, TimeSpan.FromMilliseconds(100));

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be("The server did not answer completely within 0.1 seconds.");
    }

    [Fact]
    public async Task ReadAsync_CallerCancelsDuringRequest_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        using var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await cancellationSource.CancelAsync().ConfigureAwait(false);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);

            return CreateResponse([]);
        });
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, cancellationSource.Token);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().NotBeOfType<UrlContentReadFailedException>();
    }

    [Fact]
    public async Task ReadAsync_CallerCancelsDuringBodyRead_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        using var handler = new StubHttpMessageHandler(_ => CreateResponse(new StreamContent(new TestStream(
            async (_, cancellationToken) =>
            {
                await cancellationSource.CancelAsync().ConfigureAwait(false);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);

                return 0;
            }))));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, cancellationSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadAsync_CallerTokenAlreadyCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => CreateResponse("Text"u8.ToArray()));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, new CancellationToken(canceled: true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        handler.Requests
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_RequestFails_ThrowsUrlContentReadFailedException()
    {
        // Arrange
        var requestException = new HttpRequestException("No such host is known.");
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(requestException));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which;

        exception.Reason
            .Should().Be("The request failed: No such host is known.");

        exception.InnerException
            .Should().BeSameAs(requestException);
    }

    [Fact]
    public async Task ReadAsync_BodyReadFails_ThrowsUrlContentReadFailedException()
    {
        // Arrange
        var ioException = new IOException("The connection was reset.");
        using var handler = new StubHttpMessageHandler(_ => CreateResponse(new StreamContent(new TestStream(
            (_, _) => ValueTask.FromException<int>(ioException)))));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which;

        exception.Reason
            .Should().Be("The request failed: The connection was reset.");

        exception.InnerException
            .Should().BeSameAs(ioException);
    }

    [Theory]
    [InlineData(ContentLengthHeader.Correct)]
    [InlineData(ContentLengthHeader.Missing)]
    [InlineData(ContentLengthHeader.TooSmall)]
    [InlineData(ContentLengthHeader.TooLarge)]
    public async Task ReadAsync_BodyExactlyAtSizeLimit_ReturnsBodyText(ContentLengthHeader contentLengthHeader)
    {
        // Arrange
        var body = CreateAsciiBody(MaxContentLength);
        using var handler = new StubHttpMessageHandler(_ => CreateResponse(body, contentLengthHeader));
        var sut = CreateSut(handler);

        // Act
        var text = await sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        text.Length
            .Should().Be(MaxContentLength);
    }

    [Theory]
    [InlineData(ContentLengthHeader.Correct)]
    [InlineData(ContentLengthHeader.Missing)]
    [InlineData(ContentLengthHeader.TooSmall)]
    public async Task ReadAsync_BodyLargerThanSizeLimit_ThrowsUrlContentReadFailedException(
        ContentLengthHeader contentLengthHeader)
    {
        // Arrange
        var body = CreateAsciiBody(MaxContentLength + 1);
        using var handler = new StubHttpMessageHandler(_ => CreateResponse(body, contentLengthHeader));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be("The response body is larger than 1,048,576 bytes.");
    }

    [Fact]
    public async Task ReadAsync_BodyExactlyAtSizeLimitInSmallChunks_ReturnsCompleteBodyText()
    {
        // Arrange
        var body = CreateNumberedBody(MaxContentLength);
        using var handler = new StubHttpMessageHandler(_ => CreateResponse(new StreamContent(CreateChunkedStream(body))));
        var sut = CreateSut(handler);

        // Act
        var text = await sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        text
            .Should().Be(Encoding.ASCII.GetString(body));
    }

    [Fact]
    public async Task ReadAsync_BodyLargerThanSizeLimitInSmallChunks_ThrowsUrlContentReadFailedException()
    {
        // Arrange
        var body = CreateNumberedBody(MaxContentLength + 1);
        using var handler = new StubHttpMessageHandler(_ => CreateResponse(new StreamContent(CreateChunkedStream(body))));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be("The response body is larger than 1,048,576 bytes.");
    }

    [Fact]
    public async Task ReadAsync_EmptyBody_ReturnsEmptyText()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => CreateResponse([]));
        var sut = CreateSut(handler);

        // Act
        var text = await sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        text
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_MultiByteBodyExactlyAtSizeLimit_ReturnsBodyText()
    {
        // Arrange
        var expectedText = new string('ä', MaxContentLength / 2);
        using var handler = new StubHttpMessageHandler(_ => CreateResponse(Encoding.UTF8.GetBytes(expectedText)));
        var sut = CreateSut(handler);

        // Act
        var text = await sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        text
            .Should().Be(expectedText);
    }

    [Fact]
    public async Task ReadAsync_NoContentResponse_ThrowsUrlContentReadFailedException()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be("The response has no content type, so it is not known to be text.");
    }

    [Fact]
    public async Task ReadAsync_TwoReads_CreatesClientPerRead()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => CreateResponse("Text"u8.ToArray()));
        var sut = CreateSut(handler);

        // Act
        await sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);
        await sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _httpClientFactory.CreateClient(HttpUrlContentReader.HttpClientName))
            .MustHaveHappenedTwiceExactly();
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/markdown")]
    [InlineData("text/html")]
    [InlineData("TEXT/PLAIN")]
    [InlineData("application/json")]
    [InlineData("Application/JSON")]
    [InlineData("application/xml")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("text/markdown; charset=UTF-8")]
    [InlineData("application/json; charset=\"utf-8\"")]
    [InlineData("text/plain; charset=utf8")]
    [InlineData("text/plain; charset=UTF8")]
    [InlineData("application/json; charset=us-ascii")]
    [InlineData("text/plain; charset=US-ASCII")]
    public async Task ReadAsync_AllowedContentType_ReturnsBodyText(string contentType)
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ =>
            CreateResponse("{\"title\": \"Login\"}"u8.ToArray(), contentType: contentType));
        var sut = CreateSut(handler);

        // Act
        var text = await sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        text
            .Should().Be("{\"title\": \"Login\"}");
    }

    [Theory]
    [InlineData("application/octet-stream")]
    [InlineData("application/pdf")]
    [InlineData("application/xhtml+xml")]
    [InlineData("image/png")]
    public async Task ReadAsync_RefusedContentType_ThrowsUrlContentReadFailedException(string contentType)
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ =>
            CreateResponse("Text"u8.ToArray(), contentType: contentType));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be($"The response has the content type {contentType}, which is not text; only text/*, " +
                         "application/json, and application/xml can be read.");
    }

    [Fact]
    public async Task ReadAsync_MissingContentType_ThrowsUrlContentReadFailedException()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => CreateResponse("Text"u8.ToArray(), contentType: null));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be("The response has no content type, so it is not known to be text.");
    }

    [Theory]
    [InlineData("text/plain; charset=iso-8859-1", "iso-8859-1")]
    [InlineData("text/plain; charset=utf-16", "utf-16")]
    [InlineData("text/plain; charset=windows-1252", "windows-1252")]
    public async Task ReadAsync_CharsetOtherThanUtf8_ThrowsUrlContentReadFailedException(string contentType,
        string expectedCharset)
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ =>
            CreateResponse("Text"u8.ToArray(), contentType: contentType));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be($"The response has the charset {expectedCharset}; only utf-8, utf8, and us-ascii can be read.");
    }

    [Theory]
    [InlineData(new byte[] { 0x4C, 0xC3, 0x28 }, "an unknown encoding with bytes that are not valid UTF-8", true)]
    [InlineData(new byte[] { 0x4C, 0xC3 }, "an unknown encoding with bytes that are not valid UTF-8", true)]
    [InlineData(new byte[] { 0x4C, 0x00, 0x6F, 0x00 }, "UTF-16 or UTF-32 without byte order mark", false)]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x4C, 0x00 }, "UTF-16 LE", false)]
    public async Task ReadAsync_BodyNotUtf8_ThrowsUrlContentReadFailedException(byte[] body,
        string expectedEncodingName, bool expectsDecoderException)
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => CreateResponse(body));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which;

        exception.Reason
            .Should().Be($"The response body is not UTF-8 text (found: {expectedEncodingName}).");

        if (expectsDecoderException)
        {
            exception.InnerException
                .Should().BeOfType<DecoderFallbackException>();
        }
        else
        {
            exception.InnerException
                .Should().BeNull();
        }
    }

    [Fact]
    public async Task ReadAsync_RedirectFromHttpsToHttp_ThrowsUrlContentReadFailedException()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ =>
            CreateRedirect(HttpStatusCode.Found, "http://example.com/docs/request.md"));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be("The server redirected from https to the insecure URL http://example.com/docs/request.md.");

        handler.Requests
            .Should().ContainSingle();
    }

    [Theory]
    [InlineData("ftp://example.com/request.md")]
    [InlineData("file:///etc/passwd")]
    [InlineData("mailto:team@example.com")]
    public async Task ReadAsync_RedirectToNonHttpScheme_ThrowsUrlContentReadFailedException(string location)
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => CreateRedirect(HttpStatusCode.Found, location));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be($"The server redirected to {location}, which is not an http or https URL.");

        handler.Requests
            .Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://bad url")]
    public async Task ReadAsync_RedirectWithoutValidLocation_ThrowsUrlContentReadFailedException(string? location)
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => CreateRedirect(HttpStatusCode.Found, location));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which.Reason
            .Should().Be($"The server answered {RequestUrl} with the redirect status code 302 (Found), but without " +
                         "a valid Location header.");
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently, "301 (Moved Permanently)")]
    [InlineData(HttpStatusCode.Found, "302 (Found)")]
    [InlineData(HttpStatusCode.SeeOther, "303 (See Other)")]
    [InlineData(HttpStatusCode.TemporaryRedirect, "307 (Temporary Redirect)")]
    [InlineData(HttpStatusCode.PermanentRedirect, "308 (Permanent Redirect)")]
    public async Task ReadAsync_RedirectNotFollowedByClient_ThrowsUrlContentReadFailedExceptionWithoutFollowing(
        HttpStatusCode statusCode, string expectedStatusCode)
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ =>
            CreateRedirect(statusCode, "https://docs.example.org/moved/request.md"));
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which;

        exception.Reason
            .Should().Be($"The server answered {RequestUrl} with the redirect status code {expectedStatusCode} to " +
                         "https://docs.example.org/moved/request.md, and the redirect was not followed, for example " +
                         "because the server redirected too many times.");

        exception.Url
            .Should().Be(new Uri(RequestUrl));

        handler.Requests
            .Should().ContainSingle();
    }

    [Fact]
    public async Task ReadAsync_RedirectNotFollowedAfterRedirects_ResolvesLocationAgainstLastRequestUrl()
    {
        // Arrange: the client followed redirects to the URL of the request that the response carries.
        using var handler = new StubHttpMessageHandler(_ =>
        {
            var response = CreateRedirect(HttpStatusCode.Found, "c.md");
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://other.example.org/a/b.md");

            return response;
        });
        var sut = CreateSut(handler);

        // Act
        var act = () => sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which;

        exception.Reason
            .Should().Be("The server answered https://other.example.org/a/b.md with the redirect status code 302 " +
                         "(Found) to https://other.example.org/a/c.md, and the redirect was not followed, for " +
                         "example because the server redirected too many times.");

        exception.Url
            .Should().Be(new Uri(RequestUrl));
    }

    [Theory]
    [InlineData("ftp://example.com/request.md")]
    [InlineData("file:///tmp/request.md")]
    [InlineData("docs/request.md")]
    public async Task ReadAsync_UrlNotHttpOrHttps_ThrowsUrlContentReadFailedException(string url)
    {
        // Arrange
        var sut = new HttpUrlContentReader(_httpClientFactory);

        // Act
        var act = () => sut.ReadAsync(new Uri(url, UriKind.RelativeOrAbsolute), MaxContentLength,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UrlContentReadFailedException>()).Which;

        exception.Reason
            .Should().Be("Only absolute http and https URLs can be read.");

        exception.Message
            .Should().Be($"The content of the URL '{url}' cannot be read: Only absolute http and https URLs can be read.");

        A.CallTo(() => _httpClientFactory.CreateClient(A<string>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("1.3.0", "shade-kit/1.3.0")]
    [InlineData("1.3.0-beta.1", "shade-kit/1.3.0-beta.1")]
    [InlineData("0.1.0-feature-rework-new-feature.5", "shade-kit/0.1.0-feature-rework-new-feature.5")]
    [InlineData("", "shade-kit")]
    [InlineData("1.0 beta", "shade-kit")]
    [InlineData("1.0/2", "shade-kit")]
    public void CreateUserAgent_EngineVersion_ReturnsShadeKitProduct(string engineVersion, string expectedUserAgent)
    {
        // Act
        var userAgent = HttpUrlContentReader.CreateUserAgent(engineVersion);

        // Assert
        userAgent.ToString()
            .Should().Be(expectedUserAgent);
    }

    [Fact]
    public async Task ReadAsync_ClientWithUserAgent_SendsUserAgent()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler(_ => CreateResponse("Text\n"u8.ToArray()));

        A.CallTo(() => _httpClientFactory.CreateClient(HttpUrlContentReader.HttpClientName))
            .ReturnsLazily(() =>
            {
                var httpClient = handler.CreateClient();
                httpClient.DefaultRequestHeaders.UserAgent.Add(HttpUrlContentReader.CreateUserAgent("1.3.0"));

                return httpClient;
            });
        var sut = new HttpUrlContentReader(_httpClientFactory);

        // Act
        await sut.ReadAsync(new Uri(RequestUrl), MaxContentLength, TestContext.Current.CancellationToken);

        // Assert
        handler.Requests.Should().ContainSingle()
            .Which.Headers.UserAgent.ToString().Should().Be("shade-kit/1.3.0");
    }

    public enum ContentLengthHeader
    {
        Correct,
        Missing,
        TooSmall,
        TooLarge
    }

    private HttpUrlContentReader CreateSut(StubHttpMessageHandler handler, TimeSpan? timeout = null)
    {
        A.CallTo(() => _httpClientFactory.CreateClient(HttpUrlContentReader.HttpClientName))
            .ReturnsLazily(() =>
            {
                var httpClient = handler.CreateClient();
                httpClient.Timeout = timeout ?? HttpUrlContentReader.RequestTimeout;

                return httpClient;
            });

        return new HttpUrlContentReader(_httpClientFactory);
    }

    private static byte[] CreateAsciiBody(int length)
    {
        var body = new byte[length];
        Array.Fill(body, (byte)'a');

        return body;
    }

    // Repeats the digits 0 to 9, so that a lost or repeated chunk changes the text.
    private static byte[] CreateNumberedBody(int length)
    {
        var body = new byte[length];

        for (var i = 0; i < body.Length; i++)
        {
            body[i] = (byte)('0' + i % 10);
        }

        return body;
    }

    // Returns at most 7 bytes per read, so the size limit is reached in the middle of a chunk.
    private static TestStream CreateChunkedStream(byte[] body)
    {
        var position = 0;

        return new TestStream((buffer, _) =>
        {
            var count = Math.Min(Math.Min(7, buffer.Length), body.Length - position);
            body.AsSpan(position, count).CopyTo(buffer.Span);
            position += count;

            return ValueTask.FromResult(count);
        });
    }

    private static HttpResponseMessage CreateResponse(byte[] body,
        ContentLengthHeader contentLengthHeader = ContentLengthHeader.Correct, string? contentType = "text/plain",
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var content = new ByteArrayContent(body);

        content.Headers.ContentLength = contentLengthHeader switch
        {
            ContentLengthHeader.Missing => null,
            ContentLengthHeader.TooSmall => 10,
            ContentLengthHeader.TooLarge => body.Length + 1000L,
            _ => body.Length
        };

        return CreateResponse(content, contentType, statusCode);
    }

    private static HttpResponseMessage CreateResponse(HttpContent content, string? contentType = "text/plain",
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        if (contentType is not null)
        {
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }

        return new HttpResponseMessage(statusCode) { Content = content };
    }

    private static HttpResponseMessage CreateRedirect(HttpStatusCode statusCode, string? location)
    {
        var response = new HttpResponseMessage(statusCode);

        // The raw header is parsed like one received from a server; a Uri created here would make a path that starts
        // with '/' an absolute file URI on Unix.
        if (location is not null)
        {
            response.Headers.TryAddWithoutValidation("Location", location);
        }

        return response;
    }

    // A read-only stream whose reads are answered by a function, to simulate a body that blocks or fails.
    private sealed class TestStream(Func<Memory<byte>, CancellationToken, ValueTask<int>> readAsync) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return readAsync(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return readAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            // Nothing to flush in a read-only stream.
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
