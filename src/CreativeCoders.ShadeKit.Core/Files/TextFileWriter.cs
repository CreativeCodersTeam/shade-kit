using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;
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
        RepositoryFile? lineEndingFile = null, FileWriteMode mode = FileWriteMode.Overwrite,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(file);
        Ensure.NotNull(content);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The write mode is not supported.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var resolution = await _lineEndingResolver.GetLineEndingAsync(lineEndingFile ?? file, cancellationToken)
            .ConfigureAwait(false);

        var bytes = Encode(content, resolution.LineEnding);

        // Nothing, not even a parent directory, is created once the operation is canceled.
        cancellationToken.ThrowIfCancellationRequested();

        // The file lies below the repository root, so it always has a parent directory.
        Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath)!);

        if (mode == FileWriteMode.CreateNew)
        {
            await WriteNewFileAsync(file.FullPath, bytes, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await File.WriteAllBytesAsync(file.FullPath, bytes, cancellationToken).ConfigureAwait(false);
        }

        return resolution.Warnings;
    }

    /// <inheritdoc />
    public Task PrepareAsync(IReadOnlyCollection<RepositoryFile> files, CancellationToken cancellationToken = default)
    {
        return _lineEndingResolver.PrepareAsync(files, cancellationToken);
    }

    // FileMode.CreateNew checks and creates the file in one operation of the file system. The file system reports an
    // existing file with a plain IOException, so the failure counts as "exists" only if a file exists afterwards.
    private static async Task WriteNewFileAsync(string fullPath, byte[] bytes, CancellationToken cancellationToken)
    {
        FileStream stream;

        try
        {
            stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 1,
                FileOptions.Asynchronous);
        }
        catch (IOException exception) when (File.Exists(fullPath))
        {
            throw new FileAlreadyExistsException(fullPath, exception);
        }

        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
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
