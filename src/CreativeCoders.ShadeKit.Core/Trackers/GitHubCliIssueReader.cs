using System.Globalization;
using System.Text.Json;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Text;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;
using CreativeCoders.ShadeKit.Core.Web;

namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Reads an issue of GitHub or GitHub Enterprise Server with the GitHub CLI <c>gh</c>.
/// </summary>
/// <remarks>
/// <para>
/// Runs <c>gh issue view &lt;url&gt; --json title,body,comments</c> in the working directory, so <c>gh</c> uses its
/// own authentication for the host of the URL. A non-zero exit code is a failure that names the trimmed text <c>gh</c>
/// wrote to standard error.
/// </para>
/// <para>
/// The output must be a JSON object with the string <c>title</c>. A missing or <see langword="null" /> <c>body</c> is
/// an empty description, and missing or <see langword="null" /> <c>comments</c> are no comments. Every comment must be
/// an object with a <c>createdAt</c> time in ISO 8601 format (a time without an offset is taken as UTC); its
/// <c>body</c> follows the rules of the issue body, and a comment whose <c>author</c> or <c>author.login</c> is
/// missing, <see langword="null" />, or empty gets the author <see cref="UnknownAuthor" />. Any other value of an
/// unexpected type is a failure.
/// </para>
/// </remarks>
/// <param name="ghCommandRunner">The runner that starts <c>gh</c>.</param>
public sealed class GitHubCliIssueReader(IGhCommandRunner ghCommandRunner) : ITrackerIssueReader
{
    /// <summary>
    /// Represents the author of a comment whose author is not known, for example because the account was deleted.
    /// </summary>
    /// <remarks>GitHub shows the same name for comments of deleted accounts.</remarks>
    public const string UnknownAuthor = "ghost";

    /// <summary>
    /// Represents the fields that are requested from <c>gh issue view</c>.
    /// </summary>
    public const string JsonFields = "title,body,comments";

    private readonly IGhCommandRunner _ghCommandRunner = Ensure.NotNull(ghCommandRunner);

    /// <inheritdoc />
    public async Task<TrackerIssue> ReadIssueAsync(Uri issueUrl, string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(issueUrl);
        Ensure.IsNotNullOrWhitespace(workingDirectory);

        cancellationToken.ThrowIfCancellationRequested();

        // Only an absolute http or https URL is passed, so the argument can never be read as an option of gh.
        if (!HttpUrls.IsHttpUrl(issueUrl))
        {
            throw new TrackerIssueReadFailedException(issueUrl,
                "Only absolute http and https URLs can be read as issues.");
        }

        string[] arguments = ["issue", "view", issueUrl.AbsoluteUri, "--json", JsonFields];

        var result = await _ghCommandRunner.RunAsync(workingDirectory, arguments, cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new TrackerIssueReadFailedException(issueUrl, result.CreateUnexpectedExitCodeReason("gh"));
        }

        return ParseIssue(issueUrl, result.StandardOutput);
    }

    private static TrackerIssue ParseIssue(Uri issueUrl, string json)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new TrackerIssueReadFailedException(issueUrl,
                $"The gh output is not valid JSON: {Sentences.Terminate(exception.Message)}", exception);
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new TrackerIssueReadFailedException(issueUrl, "The gh output is not a JSON object.");
            }

            if (!root.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String)
            {
                throw new TrackerIssueReadFailedException(issueUrl, "The gh output has no title string.");
            }

            var body = ReadOptionalString(issueUrl, root, "body", "The body of the issue in the gh output");
            var comments = ReadComments(issueUrl, root);

            return new TrackerIssue(title.GetString()!, body, comments);
        }
    }

    private static List<TrackerIssueComment> ReadComments(Uri issueUrl, JsonElement root)
    {
        if (!root.TryGetProperty("comments", out var comments) || comments.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (comments.ValueKind != JsonValueKind.Array)
        {
            throw new TrackerIssueReadFailedException(issueUrl, "The comments in the gh output are not an array.");
        }

        return comments.EnumerateArray()
            .Select((comment, index) => ReadComment(issueUrl, comment, index + 1))
            .ToList();
    }

    private static TrackerIssueComment ReadComment(Uri issueUrl, JsonElement comment, int number)
    {
        var commentName = $"comment {number.ToString(CultureInfo.InvariantCulture)} in the gh output";

        if (comment.ValueKind != JsonValueKind.Object)
        {
            throw new TrackerIssueReadFailedException(issueUrl, $"The value of {commentName} is not a JSON object.");
        }

        var author = ReadAuthor(issueUrl, comment, commentName);

        if (!comment.TryGetProperty("createdAt", out var createdAt)
            || createdAt.ValueKind != JsonValueKind.String
            || !createdAt.TryGetDateTime(out var createdAtValue))
        {
            throw new TrackerIssueReadFailedException(issueUrl,
                $"The createdAt time of {commentName} is missing or not in ISO 8601 format.");
        }

        var body = ReadOptionalString(issueUrl, comment, "body", $"The body of {commentName}");

        return new TrackerIssueComment(author, CreateCreatedAt(createdAt, createdAtValue), body);
    }

    // A time without an offset is taken as UTC, so the result does not depend on the time zone of the machine;
    // TryGetDateTimeOffset would apply the local offset instead.
    private static DateTimeOffset CreateCreatedAt(JsonElement createdAt, DateTime createdAtValue)
    {
        return createdAtValue.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(createdAtValue, TimeSpan.Zero)
            : createdAt.GetDateTimeOffset();
    }

    private static string ReadAuthor(Uri issueUrl, JsonElement comment, string commentName)
    {
        if (!comment.TryGetProperty("author", out var author) || author.ValueKind == JsonValueKind.Null)
        {
            return UnknownAuthor;
        }

        if (author.ValueKind != JsonValueKind.Object)
        {
            throw new TrackerIssueReadFailedException(issueUrl,
                $"The author of {commentName} is not a JSON object.");
        }

        var login = ReadOptionalString(issueUrl, author, "login", $"The author login of {commentName}");

        return string.IsNullOrWhiteSpace(login) ? UnknownAuthor : login;
    }

    private static string ReadOptionalString(Uri issueUrl, JsonElement parent, string propertyName,
        string valueText)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new TrackerIssueReadFailedException(issueUrl, $"{valueText} is not a string.");
        }

        return value.GetString()!;
    }
}
