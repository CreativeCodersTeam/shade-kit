using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Writes the managed SHADE kit section into an agent instruction file such as <c>CLAUDE.md</c> or
/// <c>AGENTS.md</c>.
/// </summary>
public sealed class InstructionSectionWriter : IInstructionSectionWriter
{
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
        var beginIndexes = FindLineIndexes(content,
            text => text.StartsWith(ManagedInstructionSection.BeginMarkerPrefix, StringComparison.Ordinal));
        var endIndexes = FindLineIndexes(content, text => text == ManagedInstructionSection.EndMarker);

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

    private static int[] FindLineIndexes(TextFileContent content, Func<string, bool> isMarker)
    {
        return
        [
            .. content.Lines
                .Select((line, index) => (Text: line.Text.Trim(), Index: index))
                .Where(line => isMarker(line.Text))
                .Select(line => line.Index)
        ];
    }

    private static string FormatLineNumbers(IEnumerable<int> lineIndexes)
    {
        return string.Join(", ", lineIndexes.Select(index => index + 1));
    }
}
