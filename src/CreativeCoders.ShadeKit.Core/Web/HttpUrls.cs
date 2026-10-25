using System.Diagnostics.CodeAnalysis;

namespace CreativeCoders.ShadeKit.Core.Web;

/// <summary>
/// Provides the rule by which SHADE kit accepts a URL as an <c>http</c> or <c>https</c> URL.
/// </summary>
internal static class HttpUrls
{
    /// <summary>
    /// Determines whether the specified URL is an absolute <c>http</c> or <c>https</c> URL with a host.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="url" /> is absolute, has the scheme <c>http</c> or <c>https</c>, and
    /// has a host; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsHttpUrl(Uri url)
    {
        return url.IsAbsoluteUri
               && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
               && url.Host.Length > 0;
    }

    /// <summary>
    /// Parses the specified text as an absolute <c>http</c> or <c>https</c> URL with a host.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="url">
    /// When this method returns <see langword="true" />, the parsed URL; otherwise, <see langword="null" />.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="text" /> is such a URL; otherwise, <see langword="false" />.
    /// </returns>
    public static bool TryParse(string text, [NotNullWhen(true)] out Uri? url)
    {
        url = Uri.TryCreate(text, UriKind.Absolute, out var parsedUrl) && IsHttpUrl(parsedUrl) ? parsedUrl : null;

        return url is not null;
    }
}
