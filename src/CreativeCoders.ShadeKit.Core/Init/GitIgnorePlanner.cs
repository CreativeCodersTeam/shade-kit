using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Plans the change that makes a <c>.gitignore</c> file ignore the local-only files of SHADE kit.
/// </summary>
public sealed class GitIgnorePlanner : IGitIgnorePlanner
{
    private const string CommentLine = "# SHADE kit: local-only files";

    /// <inheritdoc />
    public async Task<PlannedFileChange?> PlanAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await TextFileContent.ReadAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (content is null)
        {
            return new PlannedFileChange(TextFileContent.CreateNew(CreateBlock(KitGitIgnoreEntries.All)),
                InitializedFileAction.Created);
        }

        var missingEntries = KitGitIgnoreEntries.All
            .Where(entry => !content.Lines.Any(line => IsEntryLine(line, entry)))
            .ToArray();

        return missingEntries.Length == 0
            ? null
            : new PlannedFileChange(content.AppendBlock(CreateBlock(missingEntries)), InitializedFileAction.Appended);
    }

    private static string CreateBlock(IEnumerable<string> entries)
    {
        return string.Join('\n', [CommentLine, .. entries]);
    }

    private static bool IsEntryLine(string lineText, string entry)
    {
        // git ignores trailing spaces of a pattern, but keeps leading whitespace and trailing tabs as part of it.
        var trimmedText = lineText.TrimEnd(' ');

        return trimmedText == entry || trimmedText == "/" + entry;
    }
}
