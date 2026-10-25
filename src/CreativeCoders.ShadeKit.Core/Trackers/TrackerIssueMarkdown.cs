using System.Text;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Formats a <see cref="TrackerIssue" /> as the Markdown text of a <c>request.md</c> file.
/// </summary>
/// <remarks>
/// <para>
/// The text starts with the heading <c># &lt;title&gt;</c>, followed by the body. If the issue has comments, a
/// <c>## Comments</c> section follows with one <c>### &lt;author&gt; · &lt;createdAt&gt;</c> heading and the comment
/// body per comment, in the order of <see cref="TrackerIssue.Comments" />. The time is formatted with
/// <see cref="FeatureTextFormat.FormatCreatedAt" />.
/// </para>
/// <para>
/// The heading is one line: every run of control characters in the title, tabs and line breaks included, is replaced
/// by one space, and leading and trailing whitespace is removed. A title that is empty after this is left out
/// together with its heading line and the empty line after it.
/// </para>
/// <para>
/// Blocks are separated by one empty line. A body that is empty or consists only of whitespace is left out together
/// with its empty line; of any other body only the trailing <c>\r</c> and <c>\n</c> characters are removed, so they
/// add no empty lines between the blocks. Blocks are joined with <c>\n</c> and the text ends with one <c>\n</c>,
/// or is empty if there is no block; the line ending of the file is decided when it is written.
/// </para>
/// </remarks>
internal static class TrackerIssueMarkdown
{
    private const string LineEnding = "\n";

    /// <summary>
    /// Formats the specified issue as Markdown.
    /// </summary>
    /// <param name="issue">The issue to format.</param>
    /// <returns>
    /// The Markdown text of the issue, ending with <c>\n</c>, or an empty string if the issue has no title, no body,
    /// and no comments.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="issue" /> is <see langword="null" />.</exception>
    public static string Format(TrackerIssue issue)
    {
        Ensure.NotNull(issue);

        var blocks = new List<string>();
        var headingText = CreateHeadingText(issue.Title);

        if (headingText.Length > 0)
        {
            blocks.Add($"# {headingText}");
        }

        AddBodyBlock(blocks, issue.Body);

        if (issue.Comments.Count > 0)
        {
            blocks.Add("## Comments");

            foreach (var comment in issue.Comments)
            {
                blocks.Add($"### {comment.Author} · {FeatureTextFormat.FormatCreatedAt(comment.CreatedAt)}");
                AddBodyBlock(blocks, comment.Body);
            }
        }

        return blocks.Count == 0
            ? string.Empty
            : string.Join(LineEnding + LineEnding, blocks) + LineEnding;
    }

    // A line break in the title would end the heading, so every run of control characters becomes one space.
    private static string CreateHeadingText(string title)
    {
        var text = new StringBuilder(title.Length);
        var isInControlRun = false;

        foreach (var character in title)
        {
            if (char.IsControl(character))
            {
                if (!isInControlRun)
                {
                    text.Append(' ');
                }

                isInControlRun = true;

                continue;
            }

            text.Append(character);
            isInControlRun = false;
        }

        return text.ToString().Trim();
    }

    private static void AddBodyBlock(List<string> blocks, string body)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            // Trailing line breaks would add empty lines, because the blocks are joined with one empty line.
            blocks.Add(body.TrimEnd('\r', '\n'));
        }
    }
}
