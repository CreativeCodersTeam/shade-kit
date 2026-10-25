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
/// Every read creates the named client <see cref="HttpClientName" />, whose primary handler must not follow redirects:
/// the reader follows up to <see cref="MaxRedirects" /> redirects itself, refuses a redirect from <c>https</c> to
/// <c>http</c> and to any other scheme, and accepts only a success status code (2xx).
/// </para>
/// <para>
/// The content type must be <c>text/*</c>, <c>application/json</c>, or <c>application/xml</c>, with no <c>charset</c>
/// or the <c>charset</c> <c>utf-8</c>. The body is read as a stream up to the maximum content length + 1 bytes,
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
    /// Represents the maximum number of redirects that are followed for one read.
    /// </summary>
    public const int MaxRedirects = 10;

    /// <summary>
    /// Represents the product name in the <c>User-Agent</c> header that the registered client sends.
    /// </summary>
    public const string UserAgentProductName = "shade-kit";

    private static readonly string[] AllowedApplicationMediaTypes = ["application/json", "application/xml"];

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
            using var response = await GetFollowingRedirectsAsync(httpClient, url, timeoutSource.Token)
                .ConfigureAwait(false);

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

    private static async Task<HttpResponseMessage> GetFollowingRedirectsAsync(HttpClient httpClient, Uri url,
        CancellationToken cancellationToken)
    {
        var currentUrl = url;

        // The first request and up to MaxRedirects redirected requests.
        for (var requestCount = 0; requestCount <= MaxRedirects; requestCount++)
        {
            var response = await GetAsync(httpClient, currentUrl, cancellationToken).ConfigureAwait(false);

            if (!IsRedirect(response.StatusCode))
            {
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                using (response)
                {
                    throw new UrlContentReadFailedException(url,
                        $"The server answered with the status code {FormatStatusCode(response)}, not with a " +
                        "success status code (2xx).");
                }
            }

            using (response)
            {
                currentUrl = GetRedirectTarget(url, currentUrl, response);
            }
        }

        throw new UrlContentReadFailedException(url, $"The server redirected more than {MaxRedirects} times.");
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

    private static Uri GetRedirectTarget(Uri url, Uri currentUrl, HttpResponseMessage response)
    {
        var location = response.Headers.Location;

        if (location is null)
        {
            throw new UrlContentReadFailedException(url,
                $"The server answered {currentUrl.OriginalString} with the redirect status code " +
                $"{FormatStatusCode(response)}, but without a valid Location header.");
        }

        var target = location.IsAbsoluteUri ? location : new Uri(currentUrl, location);

        if (!HttpUrls.IsHttpUrl(target))
        {
            throw new UrlContentReadFailedException(url,
                $"The server redirected to {target.OriginalString}, which is not an http or https URL.");
        }

        if (currentUrl.Scheme == Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttps)
        {
            throw new UrlContentReadFailedException(url,
                $"The server redirected from https to the insecure URL {target.OriginalString}.");
        }

        return target;
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

        if (charSet is not null && !string.Equals(charSet, "utf-8", StringComparison.OrdinalIgnoreCase))
        {
            throw new UrlContentReadFailedException(url,
                $"The response has the charset {charSet}; only utf-8 can be read.");
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
