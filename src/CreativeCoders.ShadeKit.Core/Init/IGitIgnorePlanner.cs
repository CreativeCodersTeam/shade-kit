using CreativeCoders.ShadeKit.Core.Files.Exceptions;

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
    /// An entry is present if a line without trailing spaces equals it, with or without a leading <c>/</c>, as git
    /// matches patterns. Missing entries are appended under <c># SHADE kit: local-only files</c> after one blank line.
    /// Existing lines and a byte order mark are kept.
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
