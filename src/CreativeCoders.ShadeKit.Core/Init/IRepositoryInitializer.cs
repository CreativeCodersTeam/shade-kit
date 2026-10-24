using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Init.Exceptions;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;

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
    /// Works at the repository root, also from a subdirectory, and checks everything before anything is written. If a
    /// write fails or is canceled after <c>.shadekit/</c> was created, <c>.shadekit/</c> is removed again; repository
    /// files already written stay, and the next run does not repeat their changes.
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
