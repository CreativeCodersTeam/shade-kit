using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

/// <summary>
/// Represents the exception that is thrown when an issue cannot be read from its tracker, for example because the
/// tracker tool exits with an error or returns data that SHADE kit does not expect.
/// </summary>
/// <param name="issueUrl">The URL of the issue that was requested.</param>
/// <param name="reason">A sentence that describes why the issue cannot be read.</param>
/// <param name="innerException">
/// The exception that caused this exception, or <see langword="null" /> if there is none.
/// </param>
public sealed class TrackerIssueReadFailedException(Uri issueUrl, string reason, Exception? innerException = null)
    : Exception($"The issue '{Ensure.NotNull(issueUrl).OriginalString}' cannot be read: {reason}", innerException)
{
    /// <summary>
    /// Gets the URL of the issue that cannot be read.
    /// </summary>
    /// <value>The URL as it was requested.</value>
    public Uri IssueUrl { get; } = issueUrl;

    /// <summary>
    /// Gets the reason why the issue cannot be read.
    /// </summary>
    /// <value>
    /// A sentence that describes the problem, for example that <c>gh</c> exited with the exit code 1 and the text it
    /// wrote to standard error.
    /// </value>
    public string Reason { get; } = reason;
}
