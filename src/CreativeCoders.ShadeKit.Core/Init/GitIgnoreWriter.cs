namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Ensures that a <c>.gitignore</c> file ignores the local-only files of SHADE kit.
/// </summary>
public sealed class GitIgnoreWriter : IGitIgnoreWriter
{
    private const string CommentLine = "# SHADE kit: local-only files";

    /// <inheritdoc />
    public async Task ValidateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Reading the file applies every check that the write would fail on.
        await TextFileContent.ReadAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InitializedFileAction?> EnsureEntriesAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var content = await TextFileContent.ReadAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (content is null)
        {
            var newFileBytes = TextFileContent.CreateNew(CreateBlock(KitGitIgnoreEntries.All));

            await File.WriteAllBytesAsync(filePath, newFileBytes, cancellationToken).ConfigureAwait(false);

            return InitializedFileAction.Created;
        }

        var missingEntries = KitGitIgnoreEntries.All
            .Where(entry => !content.Lines.Any(line => IsEntryLine(line.Text, entry)))
            .ToArray();

        if (missingEntries.Length == 0)
        {
            return null;
        }

        await File.WriteAllBytesAsync(filePath, content.AppendBlock(CreateBlock(missingEntries)), cancellationToken)
            .ConfigureAwait(false);

        return InitializedFileAction.Appended;
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
