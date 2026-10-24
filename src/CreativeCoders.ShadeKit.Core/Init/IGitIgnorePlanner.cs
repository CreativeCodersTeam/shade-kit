using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Plans the change that makes a <c>.gitignore</c> file ignore the local-only files of SHADE kit.
/// </summary>
public interface IGitIgnorePlanner
{
    /// <summary>
    /// Computes the change that makes a <c>.gitignore</c> file contain every entry of
    /// <see cref="KitGitIgnoreEntries.All" />, without writing it.
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
    /// A missing file gets new content without byte order mark that ends with a line ending. In an existing file, the
    /// existing lines are kept, a leading UTF-8 byte order mark is kept, and a missing final line ending is added. The
    /// content holds no line endings: the line ending is decided when the file is written (see
    /// <see cref="ITextFileWriter.WriteAsync" />), and the whole file, existing lines included, is written with it. A
    /// file that is not UTF-8 is refused.
    /// </para>
    /// </remarks>
    /// <param name="filePath">The absolute path of the <c>.gitignore</c> file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the new content with
    /// <see cref="InitializedFileAction.Created" /> if the file does not exist or
    /// <see cref="InitializedFileAction.Appended" /> if entries are appended, or <see langword="null" /> if the file
    /// already contains every entry and needs no change.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath" /> is <see langword="null" />, empty, whitespace, or not an absolute path.
    /// </exception>
    /// <exception cref="PathNotAFileException"><paramref name="filePath" /> is an existing directory.</exception>
    /// <exception cref="UnsupportedFileEncodingException">
    /// The file starts with a UTF-16 or UTF-32 byte order mark, contains a NUL byte, or contains bytes that are not
    /// valid UTF-8.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<PlannedFileChange?> PlanAsync(string filePath, CancellationToken cancellationToken = default);
}
