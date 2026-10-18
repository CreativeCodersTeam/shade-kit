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
    /// Files that are already written stay in place if a later write fails.
    /// </para>
    /// </remarks>
    /// <param name="path">The directory at which the search for the repository root starts.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous initialisation. The task result contains the repository root and the
    /// files that were created or changed.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="path" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="NotAGitRepositoryException">
    /// <paramref name="path" /> is not inside a git repository.
    /// </exception>
    /// <exception cref="KitAlreadyInitializedException">
    /// The repository root already contains a <c>.shadekit</c> directory or file.
    /// </exception>
    /// <exception cref="PathNotAFileException">
    /// <c>.gitignore</c>, <c>CLAUDE.md</c>, or <c>AGENTS.md</c> at the repository root is a directory.
    /// </exception>
    /// <exception cref="UnsupportedFileEncodingException">
    /// <c>.gitignore</c>, <c>CLAUDE.md</c>, or <c>AGENTS.md</c> at the repository root starts with a UTF-16 or UTF-32
    /// byte order mark.
    /// </exception>
    /// <exception cref="InvalidManagedSectionException">
    /// The managed section markers in <c>CLAUDE.md</c> or <c>AGENTS.md</c> are invalid.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<RepositoryInitResult> InitializeAsync(string path, CancellationToken cancellationToken = default);
}
