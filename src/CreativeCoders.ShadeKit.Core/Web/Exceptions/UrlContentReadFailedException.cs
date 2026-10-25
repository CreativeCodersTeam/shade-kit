using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Web.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the content of a URL cannot be read, for example because the server
/// cannot be reached, answers with an error status code, redirects too often, or sends content that is not UTF-8
/// text or is larger than 1 MiB.
/// </summary>
/// <param name="url">The URL whose content was requested.</param>
/// <param name="reason">A sentence that describes why the content cannot be read.</param>
/// <param name="innerException">
/// The exception that caused this exception, or <see langword="null" /> if there is none.
/// </param>
public sealed class UrlContentReadFailedException(Uri url, string reason, Exception? innerException = null)
    : Exception($"The content of the URL '{Ensure.NotNull(url).OriginalString}' cannot be read: {reason}",
        innerException)
{
    /// <summary>
    /// Gets the URL whose content cannot be read.
    /// </summary>
    /// <value>The URL as it was requested, before any redirect was followed.</value>
    public Uri Url { get; } = url;

    /// <summary>
    /// Gets the reason why the content cannot be read.
    /// </summary>
    /// <value>
    /// A sentence that describes the problem, for example that the server answered with status code 404 (Not Found).
    /// </value>
    public string Reason { get; } = reason;
}
