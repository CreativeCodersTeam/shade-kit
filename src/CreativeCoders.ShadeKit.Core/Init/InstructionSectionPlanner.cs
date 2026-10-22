using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Plans the managed SHADE kit section of an agent instruction file such as <c>CLAUDE.md</c> or
/// <c>AGENTS.md</c>.
/// </summary>
public sealed class InstructionSectionPlanner : IInstructionSectionPlanner
{
    private const string CommentEnd = "-->";

    /// <inheritdoc />
    public async Task<PlannedFileChange?> PlanAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await TextFileContent.ReadAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (content is null)
        {
            return new PlannedFileChange(TextFileContent.CreateNew(ManagedInstructionSection.Text),
                InitializedFileAction.Created);
        }

        if (FindSection(content, filePath) is not { } section)
        {
            return new PlannedFileChange(content.AppendBlock(ManagedInstructionSection.Text),
                InitializedFileAction.Appended);
        }

        var newBytes = content.ReplaceLines(section.Begin, section.End, ManagedInstructionSection.Text);

        return content.HasBytes(newBytes) ? null : new PlannedFileChange(newBytes, InitializedFileAction.Updated);
    }

    /// <summary>
    /// Finds the lines of the begin and end marker of the managed section.
    /// </summary>
    /// <returns>The marker lines, or <see langword="null" /> if the file contains no marker.</returns>
    /// <exception cref="InvalidManagedSectionException">The markers in the file are invalid.</exception>
    private static (TextFileLine Begin, TextFileLine End)? FindSection(TextFileContent content, string filePath)
    {
        // Markers in a fenced code block are examples, for example in documentation of the markers, not the section.
        var (lines, unclosedFenceIndex) = GetTrimmedLinesOutsideCodeFences(content);

        // A section appended after an unclosed block would be part of it and not be found again, so every later
        // init would append another one.
        if (unclosedFenceIndex is { } fenceIndex)
        {
            throw new InvalidManagedSectionException(filePath,
                $"The code block opened in line {fenceIndex + 1} is never closed, so the section cannot be placed " +
                "after it.");
        }

        var beginIndexes = FindLineIndexes(lines, IsBeginMarker);
        var endIndexes = FindLineIndexes(lines, text => text == ManagedInstructionSection.EndMarker);

        var reason = (beginIndexes.Length, endIndexes.Length) switch
        {
            (0, 0) => null,
            (> 1, _) => $"The file contains more than one begin marker (lines {FormatLineNumbers(beginIndexes)}).",
            (_, > 1) => $"The file contains more than one end marker (lines {FormatLineNumbers(endIndexes)}).",
            (1, 0) => $"The begin marker in line {beginIndexes[0] + 1} has no end marker.",
            (0, 1) => $"The end marker in line {endIndexes[0] + 1} has no begin marker.",
            _ when endIndexes[0] < beginIndexes[0] =>
                $"The end marker in line {endIndexes[0] + 1} comes before the begin marker in line " +
                $"{beginIndexes[0] + 1}.",
            _ => null
        };

        if (reason is not null)
        {
            throw new InvalidManagedSectionException(filePath, reason);
        }

        return beginIndexes.Length == 0
            ? null
            : (content.Lines[beginIndexes[0]], content.Lines[endIndexes[0]]);
    }

    private static bool IsBeginMarker(string text)
    {
        if (!text.StartsWith(ManagedInstructionSection.BeginMarkerPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        // The prefix must end a word, so that for example "<!-- shade-kit:beginning" is no begin marker.
        var rest = text.AsSpan(ManagedInstructionSection.BeginMarkerPrefix.Length);

        return rest.StartsWith(CommentEnd, StringComparison.Ordinal) || (rest.Length > 0 && char.IsWhiteSpace(rest[0]));
    }

    private static int[] FindLineIndexes(List<(string Text, int Index)> lines, Func<string, bool> isMarker)
    {
        return [.. lines.Where(line => isMarker(line.Text)).Select(line => line.Index)];
    }

    /// <summary>
    /// Gets the lines that are not part of a fenced code block, as <see cref="MarkdownCodeFences" /> detects them.
    /// </summary>
    /// <returns>
    /// The text without leading and trailing whitespace and the index of every line outside a block, and the index of
    /// the line that opens a block that is still open at the end of the file, or <see langword="null" /> if every
    /// block is closed.
    /// </returns>
    private static (List<(string Text, int Index)> Lines, int? UnclosedFenceIndex) GetTrimmedLinesOutsideCodeFences(
        TextFileContent content)
    {
        var (lineIndexes, unclosedFenceIndex) =
            MarkdownCodeFences.FindLinesOutsideCodeFences([.. content.Lines.Select(line => line.Text)]);

        return ([.. lineIndexes.Select(index => (content.Lines[index].Text.Trim(), index))], unclosedFenceIndex);
    }

    private static string FormatLineNumbers(IEnumerable<int> lineIndexes)
    {
        return string.Join(", ", lineIndexes.Select(index => index + 1));
    }
}
