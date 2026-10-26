using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Trackers;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Loads the content of the source of a new feature: the issue of a tracker URL, the content of another URL, or the
/// content of a file.
/// </summary>
public interface IFeatureSourceReader
{
    /// <summary>
    /// Reads the issue at the specified tracker URL.
    /// </summary>
    /// <param name="source">The source as it was passed, which a failure names.</param>
    /// <param name="issueUrl">The absolute <c>http</c> or <c>https</c> URL of the issue.</param>
    /// <param name="repositoryRootPath">The root directory of the repository, in which the tracker tool runs.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read. The task result contains the issue.</returns>
    /// <exception cref="InvalidFeatureSourceException">
    /// The issue cannot be read, with the problem <see cref="FeatureSourceProblem.GhUnavailable" /> or
    /// <see cref="FeatureSourceProblem.IssueReadFailed" />; the exception of the reader is the inner exception.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<TrackerIssue> ReadIssueAsync(string source, Uri issueUrl, string repositoryRootPath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the content of the specified URL, whose host is not a tracker, as UTF-8 text of at most
    /// <see cref="FeatureSourceReader.MaxSourceLength" /> bytes.
    /// </summary>
    /// <param name="source">The source as it was passed, which a failure names.</param>
    /// <param name="url">The absolute <c>http</c> or <c>https</c> URL to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read. The task result contains the content without a leading UTF-8
    /// byte order mark.
    /// </returns>
    /// <exception cref="InvalidFeatureSourceException">
    /// The content cannot be read, with the problem <see cref="FeatureSourceProblem.UrlReadFailed" />; the exception
    /// of the reader is the inner exception.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<string> ReadUrlAsync(string source, Uri url, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the content of the specified file as UTF-8 text of at most
    /// <see cref="FeatureSourceReader.MaxSourceLength" /> bytes.
    /// </summary>
    /// <param name="source">The source as it was passed, which a failure names.</param>
    /// <param name="filePath">The full path of the file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read. The task result contains the content without a leading UTF-8
    /// byte order mark and with its line endings.
    /// </returns>
    /// <exception cref="InvalidFeatureSourceException">
    /// The file cannot be read, is larger than <see cref="FeatureSourceReader.MaxSourceLength" /> bytes, or is not
    /// UTF-8 text.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<string> ReadFileAsync(string source, string filePath, CancellationToken cancellationToken);
}
