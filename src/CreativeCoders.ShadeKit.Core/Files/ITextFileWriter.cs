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
    /// <paramref name="lineEndingPath" /> or else <paramref name="relativePath" />, is used for every line. The content
    /// is encoded as UTF-8, with a byte order mark if <see cref="TextFileContent.HasUtf8Bom" /> is
    /// <see langword="true" />. Missing parent directories are created; an existing file is overwritten.
    /// </remarks>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="relativePath">
    /// The path of the file relative to <paramref name="repositoryRootPath" />, with forward slashes or the directory
    /// separator of the operating system.
    /// </param>
    /// <param name="content">The content to write.</param>
    /// <param name="lineEndingPath">
    /// The path relative to <paramref name="repositoryRootPath" /> for which the line ending is determined, with forward
    /// slashes or the directory separator of the operating system; or <see langword="null" /> to determine it for
    /// <paramref name="relativePath" />. The file at this path is neither read nor written.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous write operation. The task result contains the warnings that occurred
    /// while the line ending was determined, for example an <see cref="EditorConfigUnreadableWarning" />; empty if
    /// there were none.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryRootPath" /> or <paramref name="relativePath" /> is <see langword="null" />, empty,
    /// or whitespace; <paramref name="relativePath" /> is rooted or does not name a path below
    /// <paramref name="repositoryRootPath" />; or <paramref name="lineEndingPath" /> is not <see langword="null" />
    /// and is empty, whitespace, rooted, or does not name a path below <paramref name="repositoryRootPath" />.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="content" /> is <see langword="null" />.</exception>
    /// <exception cref="IOException">An I/O error occurred while the file was written.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller has no permission to write the file.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<IReadOnlyList<KitWarning>> WriteAsync(string repositoryRootPath, string relativePath,
        TextFileContent content, string? lineEndingPath = null, CancellationToken cancellationToken = default);
}
