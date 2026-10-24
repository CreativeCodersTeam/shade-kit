using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Writes the content of a text file in a repository to disk with the line ending that the repository prescribes for
/// the file.
/// </summary>
public interface ITextFileWriter
{
    /// <summary>
    /// Writes the specified content to a file in a repository.
    /// </summary>
    /// <remarks>
    /// One line ending, determined with <see cref="ILineEndingResolver.GetLineEndingAsync" /> for
    /// <paramref name="lineEndingFile" /> or else <paramref name="file" />, is used for every line. The content is
    /// encoded as UTF-8, with a byte order mark if <see cref="TextFileContent.HasUtf8Bom" /> is
    /// <see langword="true" />. Missing parent directories are created; an existing file is overwritten.
    /// </remarks>
    /// <param name="file">The file to write.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="lineEndingFile">
    /// The file for which the line ending is determined, or <see langword="null" /> to determine it for
    /// <paramref name="file" />. This file is neither read nor written.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous write operation. The task result contains the warnings that occurred
    /// while the line ending was determined, for example an <see cref="EditorConfigUnreadableWarning" />; empty if
    /// there were none.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="file" /> or <paramref name="content" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="IOException">An I/O error occurred while the file was written.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller has no permission to write the file.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<IReadOnlyList<KitWarning>> WriteAsync(RepositoryFile file, TextFileContent content,
        RepositoryFile? lineEndingFile = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares the line endings of several files that are written next, as
    /// <see cref="ILineEndingResolver.PrepareAsync" /> does.
    /// </summary>
    /// <remarks>
    /// Optional; it does not change the results of later writes.
    /// </remarks>
    /// <param name="files">The files whose line endings are determined next.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="files" /> is <see langword="null" /> or contains <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task PrepareAsync(IReadOnlyCollection<RepositoryFile> files, CancellationToken cancellationToken = default);
}
