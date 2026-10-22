using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Derives the id, title, and source of a new feature from the input of <c>shade-kit feature new</c>.
/// </summary>
/// <remarks>
/// <para>
/// The creation time is read once per call with <see cref="TimeProvider.GetLocalNow" /> and truncated to whole
/// seconds; it is used both for a time-based id and for <see cref="FeatureIdentity.CreatedAt" />.
/// </para>
/// <para>
/// Every path for which <see cref="File.Exists" /> is <see langword="true" /> is a file source; on Unix this includes
/// FIFOs, devices, sockets, and symbolic links whose target does not exist. The file is read through a
/// <see cref="FileStream" />, at most <see cref="MaxSourceFileSize" /> + 1 bytes, without relying on the reported
/// length, because devices such as <c>/dev/zero</c> report a length of 0. A file with more bytes is refused, so a
/// device that never ends or a huge file is not read completely. An entry that cannot be opened or read, such as a
/// socket or a symbolic link whose target does not exist, is refused with the reason of the IO error; an entry that
/// reads no bytes, such as <c>/dev/null</c>, is an empty file.
/// </para>
/// <para>
/// A FIFO (named pipe) is not detected: opening a FIFO that no process writes to blocks until a writer opens it, so
/// the command waits. This is a known residual risk.
/// </para>
/// </remarks>
/// <param name="timeProvider">The provider of the local time.</param>
public sealed class FeatureIdFactory(TimeProvider timeProvider) : IFeatureIdFactory
{
    /// <summary>
    /// Represents the maximum size, in bytes, of the file of a file source.
    /// </summary>
    /// <remarks>
    /// The limit is 1 MiB (1,048,576 bytes). A file source with more bytes is refused with an
    /// <see cref="InvalidFeatureSourceException" />.
    /// </remarks>
    public const int MaxSourceFileSize = 1_048_576;

    private const string TimeBasedIdFormat = "yyyyMMdd-HHmm";

    private const int MaxHeadingIndentation = 3;

    private const string ExplicitTitleOrigin = "passed with --title";

    private readonly TimeProvider _timeProvider = Ensure.NotNull(timeProvider);

    /// <inheritdoc />
    public async Task<FeatureIdentity> CreateAsync(string source, string? title, string workingDirectory,
        IReadOnlyDictionary<string, string> trackers, string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(source);
        Ensure.IsNotNullOrWhitespace(workingDirectory);
        Ensure.NotNull(trackers);
        Ensure.IsNotNullOrWhitespace(repositoryRootPath);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidFeatureSourceException(source,
                "The source is empty. Pass an issue URL, the path of an existing UTF-8 text file, or the request " +
                "text.");
        }

        var explicitTitle = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        var createdAt = TruncateToSeconds(_timeProvider.GetLocalNow());

        if (TryGetHttpUrl(source, out var url))
        {
            return CreateFromUrl(source, url, explicitTitle, trackers, createdAt);
        }

        var filePath = FindSourceFilePath(source, workingDirectory);

        if (filePath is not null)
        {
            return await CreateFromFileAsync(source, filePath, explicitTitle, repositoryRootPath, createdAt,
                cancellationToken).ConfigureAwait(false);
        }

        return CreateFromText(source, explicitTitle, createdAt);
    }

    private static FeatureIdentity CreateFromUrl(string source, Uri url, string? explicitTitle,
        IReadOnlyDictionary<string, string> trackers, DateTimeOffset createdAt)
    {
        var trackerPrefix = FindTrackerPrefix(trackers, url.Host);
        string? issueNumber = null;

        if (trackerPrefix is not null)
        {
            issueNumber = GetIssueNumber(url)
                          ?? throw new InvalidFeatureSourceException(source,
                              $"The host '{url.Host}' is a tracker in config.json, but the last segment of the URL " +
                              "path is not an issue number, as in '/issues/142'.");
        }

        if (explicitTitle is null)
        {
            throw new InvalidFeatureTitleException(
                "A URL source has no title from which the feature id can be built. Pass the title with --title.");
        }

        EnsureNoControlCharacter(explicitTitle, ExplicitTitleOrigin);

        var slug = CreateSlug(explicitTitle);
        var id = trackerPrefix is null
            ? CreateTimeBasedId(createdAt, slug)
            : $"{trackerPrefix}-{issueNumber}-{slug}";

        return CreateIdentity(id, explicitTitle, new FeatureSource { Kind = FeatureSourceKind.Url, Value = source },
            source, createdAt);
    }

    private static async Task<FeatureIdentity> CreateFromFileAsync(string source, string filePath,
        string? explicitTitle, string repositoryRootPath, DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var text = await ReadSourceFileAsync(source, filePath, cancellationToken).ConfigureAwait(false);
        var title = explicitTitle;
        var titleOrigin = ExplicitTitleOrigin;

        if (title is null)
        {
            title = FindLevelOneHeading(text);
            titleOrigin = "taken from the first level-1 heading of the file";
        }

        if (title is null)
        {
            title = Path.GetFileNameWithoutExtension(filePath);
            titleOrigin = "taken from the file name";
        }

        EnsureNoControlCharacter(title, titleOrigin);

        var id = CreateTimeBasedId(createdAt, CreateSlug(title));
        var featureSource = new FeatureSource
        {
            Kind = FeatureSourceKind.File,
            Value = GetFileReference(filePath, repositoryRootPath)
        };

        return CreateIdentity(id, title, featureSource, text, createdAt);
    }

    private static FeatureIdentity CreateFromText(string source, string? explicitTitle, DateTimeOffset createdAt)
    {
        // The source is not whitespace only, so it has at least one non-empty line.
        var title = explicitTitle ?? source.Split('\n').Select(x => x.Trim()).First(x => x.Length > 0);

        EnsureNoControlCharacter(title,
            explicitTitle is null ? "taken from the first line of the text" : ExplicitTitleOrigin);

        var id = CreateTimeBasedId(createdAt, CreateSlug(title));

        return CreateIdentity(id, title, new FeatureSource { Kind = FeatureSourceKind.Text }, source, createdAt);
    }

    private static FeatureIdentity CreateIdentity(string id, string title, FeatureSource source,
        string originalInput, DateTimeOffset createdAt)
    {
        FeatureIds.EnsureValid(id);

        return new FeatureIdentity(id, title, source, originalInput, createdAt);
    }

    private static bool TryGetHttpUrl(string source, [NotNullWhen(true)] out Uri? url)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var parsedUrl)
            && (parsedUrl.Scheme == Uri.UriSchemeHttp || parsedUrl.Scheme == Uri.UriSchemeHttps)
            && parsedUrl.Host.Length > 0)
        {
            url = parsedUrl;

            return true;
        }

        url = null;

        return false;
    }

    private static string? FindSourceFilePath(string source, string workingDirectory)
    {
        var fullPath = TryGetFullPath(source, workingDirectory);

        return fullPath is not null && File.Exists(fullPath)
            ? fullPath
            : null;
    }

    private static string? TryGetFullPath(string source, string workingDirectory)
    {
        try
        {
            return Path.GetFullPath(source, Path.GetFullPath(workingDirectory));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            // Text that is not a valid path, for example because it contains a null character, is no file.
            return null;
        }
    }

    private static async Task<string> ReadSourceFileAsync(string source, string filePath,
        CancellationToken cancellationToken)
    {
        // One byte more than the limit is read, so a larger file is detected without reading it completely.
        var content = new byte[MaxSourceFileSize + 1];
        int length;

        try
        {
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0);

            await using (stream.ConfigureAwait(false))
            {
                // Reads until the buffer is full or the stream ends. The length of the stream is not used, because
                // devices such as /dev/zero report a length of 0.
                length = await stream
                    .ReadAtLeastAsync(content, content.Length, throwOnEndOfStream: false, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidFeatureSourceException(source, $"The file cannot be read: {exception.Message}",
                exception);
        }

        if (length > MaxSourceFileSize)
        {
            throw new InvalidFeatureSourceException(source,
                $"The file is larger than {MaxSourceFileSize.ToString("N0", CultureInfo.InvariantCulture)} bytes " +
                "(1 MiB), the size limit of a file source. Shorten the file, or pass a shorter request as text.");
        }

        return DecodeUtf8(source, content.AsSpan(0, length));
    }

    private static string? FindTrackerPrefix(IReadOnlyDictionary<string, string> trackers, string host)
    {
        return trackers
            .Where(x => string.Equals(x.Key, host, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value)
            .FirstOrDefault();
    }

    private static string? GetIssueNumber(Uri url)
    {
        var lastSegment = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        return lastSegment is not null && lastSegment.All(char.IsAsciiDigit)
            ? lastSegment
            : null;
    }

    private static string DecodeUtf8(string source, ReadOnlySpan<byte> content)
    {
        return TextFileEncoding.DecodeUtf8(content,
            (reason, innerException) => new InvalidFeatureSourceException(source, reason, innerException));
    }

    private static void EnsureNoControlCharacter(string title, string titleOrigin)
    {
        if (!title.Any(char.IsControl))
        {
            return;
        }

        var controlCharacter = title.First(char.IsControl);

        throw new InvalidFeatureTitleException(
            $"The title {titleOrigin} contains the control character U+{(int)controlCharacter:X4}; a title must be " +
            "a single line without tabs or other control characters. Pass such a title with --title.");
    }

    private static string? FindLevelOneHeading(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var unindentedLine = line.TrimStart(' ');

            if (line.Length - unindentedLine.Length > MaxHeadingIndentation
                || unindentedLine.Length < 2
                || unindentedLine[0] != '#'
                || unindentedLine[1] is not (' ' or '\t'))
            {
                continue;
            }

            var heading = unindentedLine[2..].Trim();

            if (heading.Length > 0)
            {
                return heading;
            }
        }

        return null;
    }

    private static string GetFileReference(string filePath, string repositoryRootPath)
    {
        var relativePath = Path.GetRelativePath(Path.GetFullPath(repositoryRootPath), filePath);
        var isOutsideRepository = Path.IsPathRooted(relativePath)
                                  || relativePath == ".."
                                  || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        return isOutsideRepository
            ? filePath
            : relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string CreateSlug(string title)
    {
        var slug = FeatureSlug.Create(title);

        return slug.Length > 0
            ? slug
            : throw new InvalidFeatureTitleException(
                $"The title '{title}' contains no letter or digit from which the slug of the feature id can be " +
                "built. Pass a title with letters or digits with --title.");
    }

    private static string CreateTimeBasedId(DateTimeOffset createdAt, string slug)
    {
        return $"{createdAt.ToString(TimeBasedIdFormat, CultureInfo.InvariantCulture)}-{slug}";
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset time)
    {
        return time.AddTicks(-(time.Ticks % TimeSpan.TicksPerSecond));
    }
}
