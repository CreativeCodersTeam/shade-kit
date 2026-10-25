using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Reads an issue from the tracker that hosts it.
/// </summary>
public interface ITrackerIssueReader
{
    /// <summary>
    /// Reads the title, the description, and the comments of the issue at the specified URL.
    /// </summary>
    /// <param name="issueUrl">The absolute <c>http</c> or <c>https</c> URL of the issue.</param>
    /// <param name="workingDirectory">
    /// The directory in which the tracker tool runs, usually the root of the repository, so it uses the configuration
    /// and the authentication of that repository.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read. The task result contains the issue.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="issueUrl" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="workingDirectory" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="GhNotFoundException">The tracker tool <c>gh</c> cannot be found or started.</exception>
    /// <exception cref="TrackerIssueReadFailedException">
    /// The URL is not an absolute <c>http</c> or <c>https</c> URL, the tracker tool fails, or it returns data that
    /// cannot be used.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<TrackerIssue> ReadIssueAsync(Uri issueUrl, string workingDirectory,
        CancellationToken cancellationToken = default);
}
