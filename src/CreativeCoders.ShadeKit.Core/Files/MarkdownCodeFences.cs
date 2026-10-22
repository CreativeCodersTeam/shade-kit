using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Provides the detection of fenced code blocks in Markdown text, as CommonMark defines them.
/// </summary>
/// <remarks>
/// A fenced code block starts with a line of at least three backticks or tildes, indented by at most three spaces.
/// The info string after a backtick fence must not contain a backtick. The block ends with a line that contains only
/// the same character, at least as many times as the opening fence, or at the end of the text.
/// </remarks>
internal static class MarkdownCodeFences
{
    private const int MaxFenceIndent = 3;

    private const int MinFenceLength = 3;

    /// <summary>
    /// Finds the lines that are not part of a fenced code block.
    /// </summary>
    /// <param name="lines">
    /// The lines of the text, without line breaks. A trailing carriage return of a CRLF line break is allowed.
    /// </param>
    /// <returns>
    /// The index of every line outside a block, in ascending order, and the index of the line that opens a block that
    /// is still open at the end of the text, or <see langword="null" /> if every block is closed. The lines that open
    /// and close a block are part of the block.
    /// </returns>
    public static (IReadOnlyList<int> LineIndexes, int? UnclosedFenceIndex) FindLinesOutsideCodeFences(
        IReadOnlyList<string> lines)
    {
        Ensure.NotNull(lines);

        var lineIndexes = new List<int>();
        string? openFence = null;
        var openFenceIndex = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            var text = lines[index];
            var fence = ReadCodeFence(text);

            if (openFence is null)
            {
                if (fence is null)
                {
                    lineIndexes.Add(index);
                }
                else
                {
                    openFence = fence;
                    openFenceIndex = index;
                }
            }
            else if (fence is not null && fence[0] == openFence[0] && fence.Length >= openFence.Length &&
                     text.Trim().Length == fence.Length)
            {
                openFence = null;
            }
        }

        return (lineIndexes, openFence is null ? null : openFenceIndex);
    }

    /// <summary>
    /// Reads the fence characters at the start of a line that opens or closes a fenced code block.
    /// </summary>
    /// <returns>The run of backticks or tildes, or <see langword="null" /> if the line is no code fence.</returns>
    private static string? ReadCodeFence(string text)
    {
        var indent = text.Length - text.TrimStart(' ').Length;

        if (indent > MaxFenceIndent || text.Length - indent < MinFenceLength || text[indent] is not ('`' or '~'))
        {
            return null;
        }

        var fenceChar = text[indent];
        var fenceEnd = text.AsSpan(indent).IndexOfAnyExcept(fenceChar);
        var length = fenceEnd < 0 ? text.Length - indent : fenceEnd;

        // The info string after a backtick fence must not contain a backtick, so "```code```" is inline code.
        var isFence = length >= MinFenceLength &&
                      (fenceChar == '~' || text.AsSpan(indent + length).IndexOf('`') < 0);

        return isFence ? text.Substring(indent, length) : null;
    }
}
