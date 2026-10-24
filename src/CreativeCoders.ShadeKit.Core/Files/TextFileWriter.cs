using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Warnings;

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
    public async Task<IReadOnlyList<KitWarning>> WriteAsync(RepositoryFile file, TextFileContent content,
        RepositoryFile? lineEndingFile = null, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(file);
        Ensure.NotNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        var resolution = await _lineEndingResolver.GetLineEndingAsync(lineEndingFile ?? file, cancellationToken)
            .ConfigureAwait(false);

        var bytes = Encode(content, resolution.LineEnding);

        // Nothing, not even a parent directory, is created once the operation is canceled.
        cancellationToken.ThrowIfCancellationRequested();

        // The file lies below the repository root, so it always has a parent directory.
        Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath)!);

        await File.WriteAllBytesAsync(file.FullPath, bytes, cancellationToken).ConfigureAwait(false);

        return resolution.Warnings;
    }

    /// <inheritdoc />
    public Task PrepareAsync(IReadOnlyCollection<RepositoryFile> files, CancellationToken cancellationToken = default)
    {
        return _lineEndingResolver.PrepareAsync(files, cancellationToken);
    }

    /// <summary>
    /// Encodes the specified content as the bytes of a file with the specified line ending.
    /// </summary>
    /// <param name="content">The content to encode.</param>
    /// <param name="lineEnding">The line ending that separates the lines and ends the last line.</param>
    /// <returns>
    /// The UTF-8 bytes of the lines joined by <paramref name="lineEnding" />, preceded by the UTF-8 byte order mark if
    /// <see cref="TextFileContent.HasUtf8Bom" /> is <see langword="true" />.
    /// </returns>
    internal static byte[] Encode(TextFileContent content, LineEnding lineEnding)
    {
        var textBytes = TextFileEncoding.Utf8WithoutBom.GetBytes(content.ToText(lineEnding));

        return content.HasUtf8Bom ? [.. TextFileEncoding.Utf8Bom, .. textBytes] : textBytes;
    }
}
