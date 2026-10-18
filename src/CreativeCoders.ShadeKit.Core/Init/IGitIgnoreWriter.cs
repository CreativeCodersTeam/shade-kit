namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Ensures that a <c>.gitignore</c> file ignores the local-only files of SHADE kit.
/// </summary>
public interface IGitIgnoreWriter
{
    /// <summary>
    /// Checks that a <c>.gitignore</c> file can be changed by <see cref="EnsureEntriesAsync" />, without changing it.
    /// </summary>
    /// <param name="filePath">The absolute path of the <c>.gitignore</c> file. A missing file is valid.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous check.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath" /> is <see langword="null" />, empty, whitespace, or not an absolute path.
    /// </exception>
    /// <exception cref="PathNotAFileException"><paramref name="filePath" /> is an existing directory.</exception>
    /// <exception cref="UnsupportedFileEncodingException">
    /// The file starts with a UTF-16 or UTF-32 byte order mark.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task ValidateAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that a <c>.gitignore</c> file contains every entry of <see cref="KitGitIgnoreEntries.All" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry is present if a line, without trailing spaces, equals the entry or the entry prefixed with <c>/</c>.
    /// This follows git, which ignores trailing spaces of a pattern but treats leading whitespace and trailing tabs
    /// as part of it, so a line with leading whitespace or a trailing tab does not count. Missing entries are
    /// appended without a leading <c>/</c>, under the comment line <c># SHADE kit: local-only files</c>; one blank
    /// line separates them from existing content, unless the file already ends with an empty line.
    /// </para>
    /// <para>
    /// A missing file is created as UTF-8 without byte order mark and with <c>\n</c> line terminators. In an existing
    /// file, every existing byte stays unchanged, a leading UTF-8 byte order mark is kept, a missing final line
    /// terminator is added, and the appended lines end with <c>\r\n</c> if the file contains one. A file that is
    /// not UTF-8 is not changed.
    /// </para>
    /// </remarks>
    /// <param name="filePath">The absolute path of the <c>.gitignore</c> file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous write operation. The task result contains
    /// <see cref="InitializedFileAction.Created" /> if the file was created,
    /// <see cref="InitializedFileAction.Appended" /> if entries were appended, or <see langword="null" /> if the file
    /// already contained every entry and was not changed.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath" /> is <see langword="null" />, empty, whitespace, or not an absolute path.
    /// </exception>
    /// <exception cref="PathNotAFileException"><paramref name="filePath" /> is an existing directory.</exception>
    /// <exception cref="UnsupportedFileEncodingException">
    /// The file starts with a UTF-16 or UTF-32 byte order mark.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<InitializedFileAction?> EnsureEntriesAsync(string filePath, CancellationToken cancellationToken = default);
}
