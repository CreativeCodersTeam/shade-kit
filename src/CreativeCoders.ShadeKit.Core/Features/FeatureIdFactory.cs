using System.Globalization;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Web;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Derives the id, title, and source of a new feature from the input of <c>shade-kit feature new</c>.
/// </summary>
/// <remarks>
/// <para>
/// The creation time is read once per call and truncated to whole seconds.
/// </para>
/// <para>
/// The content of every source is loaded with the <see cref="IFeatureSourceReader" />, before the feature store writes
/// anything, so a source that cannot be loaded leaves no feature behind. The issue of a URL whose host is a tracker is
/// read in the repository root after the issue number of the URL was checked; the content of any other URL is read
/// after the title was checked.
/// </para>
/// </remarks>
/// <param name="timeProvider">The provider of the local time.</param>
/// <param name="featureSourceReader">The reader that loads the content of the source.</param>
public sealed class FeatureIdFactory(TimeProvider timeProvider, IFeatureSourceReader featureSourceReader)
    : IFeatureIdFactory
{
    private const string TimeBasedIdFormat = "yyyyMMdd-HHmm";

    private const int MaxHeadingIndentation = 3;

    private const string FrontMatterDelimiter = "---";

    private const string FrontMatterEndDelimiter = "...";

    private const string ExplicitTitleOrigin = "passed with --title";

    private const string IssueTitleOrigin = "taken from the issue title";

    private readonly TimeProvider _timeProvider = Ensure.NotNull(timeProvider);

    private readonly IFeatureSourceReader _featureSourceReader = Ensure.NotNull(featureSourceReader);

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

        if (HttpUrls.TryParse(source, out var url))
        {
            return await CreateFromUrlAsync(source, url, explicitTitle, trackers, repositoryRootPath, createdAt,
                cancellationToken).ConfigureAwait(false);
        }

        var filePath = FindSourceFilePath(source, workingDirectory);

        if (filePath is not null)
        {
            return await CreateFromFileAsync(source, filePath, explicitTitle, repositoryRootPath, createdAt,
                cancellationToken).ConfigureAwait(false);
        }

        return CreateFromText(source, explicitTitle, createdAt);
    }

    private async Task<FeatureIdentity> CreateFromUrlAsync(string source, Uri url, string? explicitTitle,
        IReadOnlyDictionary<string, string> trackers, string repositoryRootPath, DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var trackerPrefix = FindTrackerPrefix(trackers, url.Host);

        return trackerPrefix is null
            ? await CreateFromWebUrlAsync(source, url, explicitTitle, createdAt, cancellationToken)
                .ConfigureAwait(false)
            : await CreateFromIssueUrlAsync(source, url, trackerPrefix, explicitTitle, repositoryRootPath, createdAt,
                cancellationToken).ConfigureAwait(false);
    }

    private async Task<FeatureIdentity> CreateFromIssueUrlAsync(string source, Uri url, string trackerPrefix,
        string? explicitTitle, string repositoryRootPath, DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var issueNumber = GetIssueNumber(url)
                          ?? throw new InvalidFeatureSourceException(source,
                              $"The host '{url.Host}' is a tracker in config.json, but the last segment of the URL " +
                              "path is not an issue number, as in '/issues/142'.");

        // A title passed with --title is checked before gh runs, so a title that cannot be used costs no gh call.
        var explicitSlug = explicitTitle is null ? null : CreateCheckedSlug(explicitTitle, ExplicitTitleOrigin);

        var issue = await _featureSourceReader.ReadIssueAsync(source, url, repositoryRootPath, cancellationToken)
            .ConfigureAwait(false);

        var title = explicitTitle ?? issue.Title.Trim();
        var slug = explicitSlug ?? CreateCheckedSlug(title, IssueTitleOrigin);

        return CreateIdentity($"{trackerPrefix}-{issueNumber}-{slug}", title, CreateUrlSource(source),
            TrackerIssueMarkdown.Format(issue), createdAt);
    }

    private async Task<FeatureIdentity> CreateFromWebUrlAsync(string source, Uri url, string? explicitTitle,
        DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        // The title is checked before the request is sent, so a missing or unusable title costs no network call.
        if (explicitTitle is null)
        {
            throw new InvalidFeatureTitleException(
                "A URL source whose host is not a tracker in config.json has no title from which the feature id can " +
                "be built. Pass the title with --title.");
        }

        var slug = CreateCheckedSlug(explicitTitle, ExplicitTitleOrigin);

        var content = await _featureSourceReader.ReadUrlAsync(source, url, cancellationToken).ConfigureAwait(false);

        return CreateIdentity(CreateTimeBasedId(createdAt, slug), explicitTitle, CreateUrlSource(source), content,
            createdAt);
    }

    private async Task<FeatureIdentity> CreateFromFileAsync(string source, string filePath, string? explicitTitle,
        string repositoryRootPath, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        var text = await _featureSourceReader.ReadFileAsync(source, filePath, cancellationToken).ConfigureAwait(false);

        var heading = explicitTitle is null ? FindLevelOneHeading(text) : null;

        var (title, titleOrigin) = (explicitTitle, heading) switch
        {
            ({ } explicitValue, _) => (explicitValue, ExplicitTitleOrigin),
            (_, { } headingValue) => (headingValue, "taken from the first level-1 heading of the file"),
            _ => (Path.GetFileNameWithoutExtension(filePath), "taken from the file name")
        };

        var id = CreateTimeBasedId(createdAt, CreateCheckedSlug(title, titleOrigin));
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

        var id = CreateTimeBasedId(createdAt, CreateCheckedSlug(title,
            explicitTitle is null ? "taken from the first line of the text" : ExplicitTitleOrigin));

        return CreateIdentity(id, title, new FeatureSource { Kind = FeatureSourceKind.Text, Value = source }, source,
            createdAt);
    }

    private static FeatureSource CreateUrlSource(string source)
    {
        return new FeatureSource { Kind = FeatureSourceKind.Url, Value = source };
    }

    private static FeatureIdentity CreateIdentity(string id, string title, FeatureSource source,
        string requestContent, DateTimeOffset createdAt)
    {
        FeatureIds.EnsureValid(id);

        return new FeatureIdentity(id, title, source, requestContent, createdAt);
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

    private static void EnsureNoControlCharacter(string title, string titleOrigin)
    {
        var controlCharacter = title.Where(char.IsControl).Select(x => (char?)x).FirstOrDefault();

        if (controlCharacter is null)
        {
            return;
        }

        throw new InvalidFeatureTitleException(
            $"The title {titleOrigin} contains the control character U+{(int)controlCharacter.Value:X4}; a title must be " +
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

    // Checks the title for control characters first, then builds the slug of the feature id from it.
    private static string CreateCheckedSlug(string title, string titleOrigin)
    {
        EnsureNoControlCharacter(title, titleOrigin);

        return CreateSlug(title);
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
