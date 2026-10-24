using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Writes the content of a text file in a repository to disk with the line ending that the repository prescribes for
/// the file.
/// </summary>
/// <remarks>
/// This is the only place in SHADE kit that turns the lines of a <see cref="TextFileContent" /> into the bytes of a
/// file.
/// </remarks>
/// <param name="lineEndingResolver">The resolver that determines the line ending of the file.</param>
public sealed class TextFileWriter(ILineEndingResolver lineEndingResolver) : ITextFileWriter
{
    private readonly ILineEndingResolver _lineEndingResolver = Ensure.NotNull(lineEndingResolver);

    /// <inheritdoc />
    public async Task<IReadOnlyList<KitWarning>> WriteAsync(string repositoryRootPath, string relativePath,
        TextFileContent content, string? lineEndingPath = null, CancellationToken cancellationToken = default)
    {
        // Validates every path before anything is resolved or written.
        var fullPath = RepositoryFilePath.GetFullPath(repositoryRootPath, relativePath);

        if (lineEndingPath is not null)
        {
            RepositoryFilePath.GetFullPath(repositoryRootPath, lineEndingPath);
        }

        Ensure.NotNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        var resolution = await _lineEndingResolver
            .GetLineEndingAsync(repositoryRootPath, lineEndingPath ?? relativePath, cancellationToken)
            .ConfigureAwait(false);

        var bytes = Encode(content, resolution.LineEnding);

        // Nothing, not even a parent directory, is created once the operation is canceled.
        cancellationToken.ThrowIfCancellationRequested();

        // The full path lies below the repository root, so it always has a parent directory.
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);

        return resolution.Warnings;
    }

    private static byte[] Encode(TextFileContent content, LineEnding lineEnding)
    {
        var textBytes = TextFileEncoding.Utf8WithoutBom.GetBytes(content.ToText(lineEnding));

        return content.HasUtf8Bom ? [.. TextFileEncoding.Utf8Bom, .. textBytes] : textBytes;
    }
}
