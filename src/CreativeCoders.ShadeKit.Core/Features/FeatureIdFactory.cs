using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Derives the id, title, and source of a new feature from the input of <c>shade-kit feature new</c>.
/// </summary>
/// <remarks>
/// The creation time is read once per call and truncated to whole seconds. A file source is read through a
/// <see cref="FileStream" /> up to <see cref="MaxSourceFileSize" /> + 1 bytes, so an endless device such as
/// <c>/dev/zero</c> is refused. Known residual risk: opening a FIFO without a writer blocks.
/// </remarks>
/// <param name="timeProvider">The provider of the local time.</param>
public sealed class FeatureIdFactory(TimeProvider timeProvider) : IFeatureIdFactory
{
    /// <summary>
    /// Represents the maximum size, in bytes, of the file of a file source.
    /// </summary>
    public const int MaxSourceFileSize = 1_048_576;

    private const string TimeBasedIdFormat = "yyyyMMdd-HHmm";

    private const int MaxHeadingIndentation = 3;

    private const string FrontMatterDelimiter = "---";

    private const string FrontMatterEndDelimiter = "...";

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
            (_, reason, innerException) => new InvalidFeatureSourceException(source, reason, innerException));
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

    /// <summary>
    /// Finds the text of the first level-1 ATX heading of a Markdown text that is not empty.
    /// </summary>
    /// <remarks>
    /// Front matter and fenced code blocks are skipped; an unclosed code block runs to the end of the text.
    /// </remarks>
    /// <returns>The heading text, or <see langword="null" /> if the text has no such heading.</returns>
    private static string? FindLevelOneHeading(string text)
    {
        var lines = text.Split('\n');
        var bodyStart = GetFrontMatterLineCount(lines);
        var bodyLines = new ArraySegment<string>(lines, bodyStart, lines.Length - bodyStart);
        var (lineIndexes, _) = MarkdownCodeFences.FindLinesOutsideCodeFences(bodyLines);

        return lineIndexes
            .Select(index => ReadLevelOneHeading(bodyLines[index]))
            .FirstOrDefault(heading => heading is { Length: > 0 });
    }

    /// <summary>
    /// Gets the number of lines of a front matter block at the start of a text.
    /// </summary>
    /// <remarks>
    /// A front matter block starts with a first line <c>---</c> and ends with the next line <c>---</c> or
    /// <c>...</c>; trailing whitespace is allowed on both. Without such an end line, the text has no front matter.
    /// </remarks>
    /// <returns>The number of lines of the block, including both delimiter lines, or 0 if there is none.</returns>
    private static int GetFrontMatterLineCount(string[] lines)
    {
        if (lines[0].TrimEnd() != FrontMatterDelimiter)
        {
            return 0;
        }

        var endIndex = Array.FindIndex(lines, 1,
            line => line.TrimEnd() is FrontMatterDelimiter or FrontMatterEndDelimiter);

        return endIndex < 0 ? 0 : endIndex + 1;
    }

    /// <summary>
    /// Reads the text of a level-1 ATX heading line.
    /// </summary>
    /// <returns>
    /// The heading text without leading and trailing whitespace and without a closing sequence, which can be empty,
    /// or <see langword="null" /> if the line is no level-1 heading.
    /// </returns>
    private static string? ReadLevelOneHeading(string line)
    {
        var unindentedLine = line.TrimStart(' ');

        if (line.Length - unindentedLine.Length > MaxHeadingIndentation
            || unindentedLine.Length < 2
            || unindentedLine[0] != '#'
            || unindentedLine[1] is not (' ' or '\t'))
        {
            return null;
        }

        var heading = unindentedLine[2..].Trim();
        var withoutClosingSequence = heading.TrimEnd('#');

        // A closing sequence must be preceded by a space or tab; a heading of only '#' is a closing sequence after
        // the space or tab of the opening '#'. Otherwise the '#' belong to the text, as in "C#".
        if (withoutClosingSequence.Length == 0)
        {
            return string.Empty;
        }

        return withoutClosingSequence[^1] is ' ' or '\t'
            ? withoutClosingSequence.TrimEnd()
            : heading;
    }

    private static string GetFileReference(string filePath, string repositoryRootPath)
    {
        var relativePath = Path.GetRelativePath(Path.GetFullPath(repositoryRootPath), filePath);

        return RepositoryFilePath.IsOutsideRoot(relativePath)
            ? filePath
            : RepositoryFilePath.ToForwardSlashes(relativePath);
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
