using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Web.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Web;

/// <summary>
/// Reads the text content of a URL with an HTTP <c>GET</c> request.
/// </summary>
/// <remarks>
/// <para>
/// Every read creates the named client <see cref="HttpClientName" /> and sends one request; redirects are followed by
/// the handlers of the client, not by the reader. Only a success status code (2xx) is accepted. A redirect status code
/// that reaches the reader is a redirect the client did not follow, for example one from <c>https</c> to
/// <c>http</c>, one to another scheme, or one beyond the redirect limit of the client; the failure names the reason.
/// </para>
/// <para>
/// The content type must be <c>text/*</c>, <c>application/json</c>, or <c>application/xml</c>, with no <c>charset</c>
/// or one of the <c>charset</c> labels <c>utf-8</c>, <c>utf8</c>, and <c>us-ascii</c>, which all mean text that
/// decodes as UTF-8. The body is read as a stream up to the maximum content length + 1 bytes,
/// without trusting the <c>Content-Length</c> header, and decoded strictly as UTF-8. The whole read, redirects and body included, is limited by the
/// <see cref="HttpClient.Timeout" /> of the client.
/// </para>
/// </remarks>
/// <param name="httpClientFactory">The factory that creates the named client <see cref="HttpClientName" />.</param>
public sealed class HttpUrlContentReader(IHttpClientFactory httpClientFactory) : IUrlContentReader
{
    /// <summary>
    /// Represents the name of the <see cref="HttpClient" /> that the reader creates.
    /// </summary>
    public const string HttpClientName = "CreativeCoders.ShadeKit.UrlContentReader";

    /// <summary>
    /// Represents the product name in the <c>User-Agent</c> header that the registered client sends.
    /// </summary>
    public const string UserAgentProductName = "shade-kit";

    private static readonly string[] AllowedApplicationMediaTypes = ["application/json", "application/xml"];

    // US-ASCII is a subset of UTF-8, so text with one of these labels decodes as UTF-8.
    private static readonly string[] AllowedCharSets = ["utf-8", "utf8", "us-ascii"];

    private readonly IHttpClientFactory _httpClientFactory = Ensure.NotNull(httpClientFactory);

    /// <summary>
    /// Gets the time after which a read is canceled when the client is registered by
    /// <see cref="ShadeKitCoreServiceCollectionExtensions.AddShadeKitCore" />.
    /// </summary>
    /// <value>30 seconds.</value>
    public static TimeSpan RequestTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates the <c>User-Agent</c> product that the client registered by
    /// <see cref="ShadeKitCoreServiceCollectionExtensions.AddShadeKitCore" /> sends.
    /// </summary>
    /// <param name="engineVersion">The version of the running engine, for example <c>1.3.0-beta.1</c>.</param>
    /// <returns>
    /// <c>shade-kit/</c> followed by <paramref name="engineVersion" />, or <c>shade-kit</c> alone if the version is
    /// empty or is not a valid product version token.
    /// </returns>
    internal static ProductInfoHeaderValue CreateUserAgent(string engineVersion)
    {
        return !string.IsNullOrEmpty(engineVersion)
               && ProductInfoHeaderValue.TryParse($"{UserAgentProductName}/{engineVersion}", out var userAgent)
            ? userAgent
            : new ProductInfoHeaderValue(UserAgentProductName, null);
    }

    /// <inheritdoc />
    public async Task<string> ReadAsync(Uri url, int maxContentLength, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(url);
        ArgumentOutOfRangeException.ThrowIfNegative(maxContentLength);

        cancellationToken.ThrowIfCancellationRequested();

        if (!HttpUrls.IsHttpUrl(url))
        {
            throw new UrlContentReadFailedException(url,
                "Only absolute http and https URLs can be read.");
        }

        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        // HttpClient.Timeout does not cover the body of a response that is read as a stream, so the whole read is
        // limited by a token of its own.
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(httpClient.Timeout);

        try
        {
            using var response = await GetAsync(httpClient, url, timeoutSource.Token).ConfigureAwait(false);

            EnsureSuccessStatusCode(url, response);
            EnsureUsableContentType(url, response);

            return await ReadBodyTextAsync(url, response, maxContentLength, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UrlContentReadFailedException(url,
                $"The server did not answer completely within {FormatSeconds(httpClient.Timeout)} seconds.",
                exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new UrlContentReadFailedException(url, $"The request failed: {exception.Message}", exception);
        }
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient httpClient, Uri url,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        return await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
    }

    private static void EnsureSuccessStatusCode(Uri url, HttpResponseMessage response)
    {
        if (IsRedirect(response.StatusCode))
        {
            throw new UrlContentReadFailedException(url, CreateRedirectNotFollowedReason(url, response));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UrlContentReadFailedException(url,
                $"The server answered with the status code {FormatStatusCode(response)}, not with a success status " +
                "code (2xx).");
        }
    }

    // The handlers of the client follow the redirects they accept, so a redirect that reaches the reader was refused;
    // the reason is derived from its target. The request of the response carries the URL of the last request.
    private static string CreateRedirectNotFollowedReason(Uri url, HttpResponseMessage response)
    {
        var currentUrl = response.RequestMessage?.RequestUri is { IsAbsoluteUri: true } requestUrl ? requestUrl : url;
        var location = response.Headers.Location;

        if (location is null)
        {
            return $"The server answered {currentUrl.OriginalString} with the redirect status code " +
                   $"{FormatStatusCode(response)}, but without a valid Location header.";
        }

        var target = location.IsAbsoluteUri ? location : new Uri(currentUrl, location);

        if (!HttpUrls.IsHttpUrl(target))
        {
            return $"The server redirected to {target.OriginalString}, which is not an http or https URL.";
        }

        if (currentUrl.Scheme == Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttps)
        {
            return $"The server redirected from https to the insecure URL {target.OriginalString}.";
        }

        return $"The server answered {currentUrl.OriginalString} with the redirect status code " +
               $"{FormatStatusCode(response)} to {target.OriginalString}, and the redirect was not followed, for " +
               "example because the server redirected too many times.";
    }

    private static void EnsureUsableContentType(Uri url, HttpResponseMessage response)
    {
        var contentType = response.Content.Headers.ContentType;

        if (contentType?.MediaType is not { } mediaType)
        {
            throw new UrlContentReadFailedException(url,
                "The response has no content type, so it is not known to be text.");
        }

        if (!IsTextMediaType(mediaType))
        {
            throw new UrlContentReadFailedException(url,
                $"The response has the content type {mediaType}, which is not text; only text/*, " +
                "application/json, and application/xml can be read.");
        }

        // The charset may be a quoted string, which means the same as the token.
        var charSet = contentType.CharSet?.Trim('"');

        if (charSet is not null && !AllowedCharSets.Contains(charSet, StringComparer.OrdinalIgnoreCase))
        {
            throw new UrlContentReadFailedException(url,
                $"The response has the charset {charSet}; only utf-8, utf8, and us-ascii can be read.");
        }
    }

    private static bool IsTextMediaType(string mediaType)
    {
        return mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
               || AllowedApplicationMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadBodyTextAsync(Uri url, HttpResponseMessage response,
        int maxContentLength, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            return await LimitedUtf8Reader
                .ReadAsync(stream, maxContentLength,
                    () => new UrlContentReadFailedException(url,
                        "The response body is larger than " +
                        $"{maxContentLength.ToString("N0", CultureInfo.InvariantCulture)} bytes."),
                    (encodingName, _, innerException) => new UrlContentReadFailedException(url,
                        $"The response body is not UTF-8 text (found: {encodingName}).", innerException),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string FormatStatusCode(HttpResponseMessage response)
    {
        var statusCode = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);

        return string.IsNullOrEmpty(response.ReasonPhrase)
            ? statusCode
            : $"{statusCode} ({response.ReasonPhrase})";
    }

    private static string FormatSeconds(TimeSpan timeout)
    {
        return timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
