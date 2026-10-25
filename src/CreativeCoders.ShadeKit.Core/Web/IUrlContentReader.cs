using CreativeCoders.ShadeKit.Core.Web.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Web;

/// <summary>
/// Reads the text content of a URL.
/// </summary>
public interface IUrlContentReader
{
    /// <summary>
    /// Reads the content of the specified URL as UTF-8 text.
    /// </summary>
    /// <param name="url">The absolute <c>http</c> or <c>https</c> URL to read.</param>
    /// <param name="maxContentLength">The maximum number of bytes of the content.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read. The task result contains the decoded text of the content,
    /// without a leading UTF-8 byte order mark.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxContentLength" /> is negative.</exception>
    /// <exception cref="UrlContentReadFailedException">
    /// The URL is not an absolute <c>http</c> or <c>https</c> URL, or the content cannot be read or used, for example
    /// because the request fails or times out, or the content is longer than
    /// <paramref name="maxContentLength" /> bytes or not UTF-8 text.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<string> ReadAsync(Uri url, int maxContentLength, CancellationToken cancellationToken = default);
}
