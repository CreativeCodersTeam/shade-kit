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
    /// <para>
    /// The line ending is determined with <see cref="ILineEndingResolver.GetLineEndingAsync" /> and used for every line
    /// of the file, so an existing file is replaced by normalised content. The lines are joined with the line ending,
    /// followed by one more line ending if <see cref="TextFileContent.HasFinalLineEnding" /> is
    /// <see langword="true" />, and encoded as UTF-8, prefixed with the UTF-8 byte order mark if
    /// <see cref="TextFileContent.HasUtf8Bom" /> is <see langword="true" />.
    /// </para>
    /// <para>
    /// The line ending is determined for <paramref name="lineEndingPath" /> if it is specified, otherwise for
    /// <paramref name="relativePath" />. This lets a file that is written to a temporary location, for example a
    /// hidden staging directory that is moved into place later, get the line ending of the path where it ends up.
    /// </para>
    /// <para>
    /// Missing parent directories of the file are created. An existing file is overwritten.
    /// </para>
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
