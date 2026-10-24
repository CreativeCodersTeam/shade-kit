using CreativeCoders.ShadeKit.Core.Git;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Determines the line ending with which SHADE kit writes a file in a repository.
/// </summary>
public interface ILineEndingResolver
{
    /// <summary>
    /// Gets the line ending with which the specified file is written.
    /// </summary>
    /// <remarks>
    /// The first source that decides wins: git (<see cref="IGitLineEndingResolver.GetLineEndingAsync" />),
    /// <c>.editorconfig</c> (<see cref="IEditorConfigLineEndingResolver.GetLineEnding" />), then the operating system.
    /// A <c>git</c> that is installed but fails and an unusable <c>.editorconfig</c> file each add a warning to the
    /// result.
    /// </remarks>
    /// <param name="file">The file whose line ending is determined; the file need not exist.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the line ending for the file and
    /// the warnings that occurred while it was determined.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="file" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<LineEndingResolution> GetLineEndingAsync(RepositoryFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares the line endings of several files that are written next, so determining each of them later takes
    /// fewer <c>git</c> processes.
    /// </summary>
    /// <remarks>
    /// Optional; it reads the git attributes of all files at once and does not change the results.
    /// </remarks>
    /// <param name="files">The files, which may belong to different repositories.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="files" /> is <see langword="null" /> or contains <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task PrepareAsync(IReadOnlyCollection<RepositoryFile> files, CancellationToken cancellationToken = default);
}
