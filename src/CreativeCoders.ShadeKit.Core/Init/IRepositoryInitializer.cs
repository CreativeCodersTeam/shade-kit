using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Initialises a git repository for SHADE kit, as <c>shade-kit init</c> does.
/// </summary>
public interface IRepositoryInitializer
{
    /// <summary>
    /// Initialises the git repository that contains the specified directory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The initialisation works at the root of the git repository, also when <paramref name="path" /> is a
    /// subdirectory. Before anything is written, it checks that the repository root contains no <c>.shadekit</c>
    /// directory or file; that <c>.gitignore</c>, <c>CLAUDE.md</c>, and <c>AGENTS.md</c> are files or missing, and
    /// UTF-8 if they exist; and that the managed section markers in <c>CLAUDE.md</c> and <c>AGENTS.md</c> are valid.
    /// </para>
    /// <para>
    /// It then creates <c>.shadekit/</c> with <c>config.json</c>, <c>gates.json</c>, every shipped template,
    /// <c>templates.lock.json</c>, and an empty <c>features/</c> directory; adds the local-only files to
    /// <c>.gitignore</c>; and writes the managed section into <c>CLAUDE.md</c> and <c>AGENTS.md</c>, in this order.
    /// If a write fails or the operation is canceled after <c>.shadekit/</c> was created, <c>.shadekit/</c> is
    /// removed again, so that the next initialisation can start over. Repository files that are already written stay
    /// in place; the next initialisation recognises their changes and does not repeat them.
    /// </para>
    /// <para>
    /// <c>.gitignore</c>, <c>CLAUDE.md</c>, and <c>AGENTS.md</c> are written through <see cref="ITextFileWriter" />,
    /// so each file is written as a whole with the line ending that the repository prescribes for it. Warnings of
    /// these writes, such as an <see cref="EditorConfigUnreadableWarning" />, are returned in
    /// <see cref="RepositoryInitResult.Warnings" />.
    /// </para>
    /// </remarks>
    /// <param name="path">The directory at which the search for the repository root starts.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous initialisation. The task result contains the repository root, the
    /// files that were created or changed, and the warnings of the writes.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="path" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="NotAGitRepositoryException">
    /// <paramref name="path" /> is not inside a git repository.
    /// </exception>
    /// <exception cref="KitAlreadyInitializedException">
    /// The repository root already contains a <c>.shadekit</c> directory.
    /// </exception>
    /// <exception cref="KitRootNotADirectoryException">
    /// The repository root contains a <c>.shadekit</c> file.
    /// </exception>
    /// <exception cref="PathNotAFileException">
    /// <c>.gitignore</c>, <c>CLAUDE.md</c>, or <c>AGENTS.md</c> at the repository root is a directory.
    /// </exception>
    /// <exception cref="UnsupportedFileEncodingException">
    /// <c>.gitignore</c>, <c>CLAUDE.md</c>, or <c>AGENTS.md</c> at the repository root starts with a UTF-16 or UTF-32
    /// byte order mark, contains a NUL byte, or contains bytes that are not valid UTF-8.
    /// </exception>
    /// <exception cref="InvalidManagedSectionException">
    /// The managed section markers in <c>CLAUDE.md</c> or <c>AGENTS.md</c> are invalid.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<RepositoryInitResult> InitializeAsync(string path, CancellationToken cancellationToken = default);
}
