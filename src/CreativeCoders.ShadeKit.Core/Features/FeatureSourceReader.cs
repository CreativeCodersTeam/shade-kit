using System.Globalization;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Trackers;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;
using CreativeCoders.ShadeKit.Core.Web;
using CreativeCoders.ShadeKit.Core.Web.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Loads the content of the source of a new feature with the <see cref="ITrackerIssueReader" />, the
/// <see cref="IUrlContentReader" />, or from the file system.
/// </summary>
/// <remarks>
/// <para>
/// A failure of a reader is reported as an <see cref="InvalidFeatureSourceException" /> whose
/// <see cref="InvalidFeatureSourceException.Problem" /> names the kind of failure; a cancellation is not wrapped.
/// </para>
/// <para>
/// A file is read through a <see cref="FileStream" /> up to <see cref="MaxSourceLength" /> + 1 bytes, so an endless
/// device such as <c>/dev/zero</c> is refused. Known residual risk: opening a FIFO without a writer blocks.
/// </para>
/// </remarks>
/// <param name="trackerIssueReader">The reader of the issue of a URL source whose host is a tracker.</param>
/// <param name="urlContentReader">The reader of the content of a URL source whose host is not a tracker.</param>
public sealed class FeatureSourceReader(ITrackerIssueReader trackerIssueReader, IUrlContentReader urlContentReader)
    : IFeatureSourceReader
{
    /// <summary>
    /// Represents the maximum size, in bytes, of the content of a file or URL source.
    /// </summary>
    public const int MaxSourceLength = 1_048_576;

    private readonly ITrackerIssueReader _trackerIssueReader = Ensure.NotNull(trackerIssueReader);

    private readonly IUrlContentReader _urlContentReader = Ensure.NotNull(urlContentReader);

    /// <inheritdoc />
    public async Task<TrackerIssue> ReadIssueAsync(string source, Uri issueUrl, string repositoryRootPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _trackerIssueReader.ReadIssueAsync(issueUrl, repositoryRootPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GhUnavailableException exception)
        {
            // The reason names gh already, for example "The GitHub CLI gh was not found in ...".
            throw new InvalidFeatureSourceException(source, $"The issue cannot be read: {exception.Reason}", exception,
                FeatureSourceProblem.GhUnavailable);
        }
        catch (TrackerIssueReadFailedException exception)
        {
            throw new InvalidFeatureSourceException(source, $"The issue cannot be read: {exception.Reason}", exception,
                FeatureSourceProblem.IssueReadFailed);
        }
    }

    /// <inheritdoc />
    public async Task<string> ReadUrlAsync(string source, Uri url, CancellationToken cancellationToken)
    {
        try
        {
            return await _urlContentReader.ReadAsync(url, MaxSourceLength, cancellationToken).ConfigureAwait(false);
        }
        catch (UrlContentReadFailedException exception)
        {
            throw new InvalidFeatureSourceException(source,
                $"The content of the URL cannot be read: {exception.Reason}", exception,
                FeatureSourceProblem.UrlReadFailed);
        }
    }

    /// <inheritdoc />
    public async Task<string> ReadFileAsync(string source, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0);

            await using (stream.ConfigureAwait(false))
            {
                return await LimitedUtf8Reader
                    .ReadAsync(stream, MaxSourceLength,
                        () => new InvalidFeatureSourceException(source,
                            $"The file is larger than {MaxSourceLength.ToString("N0", CultureInfo.InvariantCulture)} " +
                            "bytes (1 MiB), the size limit of a file source. Shorten the file, or pass a shorter " +
                            "request as text."),
                        (_, reason, innerException) => new InvalidFeatureSourceException(source, reason, innerException),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidFeatureSourceException(source, $"The file cannot be read: {exception.Message}",
                exception);
        }
    }
}
