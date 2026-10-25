using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Reads the text content of a URL whose host is not a known tracker.
/// </summary>
public interface IUrlContentReader
{
    /// <summary>
    /// Reads the content of the specified URL as UTF-8 text.
    /// </summary>
    /// <param name="url">The absolute <c>http</c> or <c>https</c> URL to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read. The task result contains the decoded text of the content,
    /// without a leading UTF-8 byte order mark.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    /// <exception cref="UrlContentReadFailedException">
    /// The URL is not an absolute <c>http</c> or <c>https</c> URL, or the content cannot be read or used, for example
    /// because the request fails or times out, or the content is not UTF-8 text.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<string> ReadAsync(Uri url, CancellationToken cancellationToken = default);
}
