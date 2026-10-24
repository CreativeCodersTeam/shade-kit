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
    /// An unusable <c>.editorconfig</c> file adds a warning to the result. The file need not exist.
    /// </remarks>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="relativePath">
    /// The path of the file relative to <paramref name="repositoryRootPath" />, with forward slashes or the directory
    /// separator of the operating system.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the line ending for the file and
    /// the warnings that occurred while it was determined.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryRootPath" /> or <paramref name="relativePath" /> is <see langword="null" />, empty,
    /// or whitespace, or <paramref name="relativePath" /> is rooted or does not name a path below
    /// <paramref name="repositoryRootPath" />.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<LineEndingResolution> GetLineEndingAsync(string repositoryRootPath, string relativePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares the line endings of several files that are written next, so determining each of them later takes
    /// fewer <c>git</c> processes.
    /// </summary>
    /// <remarks>
    /// Optional; it reads the git attributes of all files at once and does not change the results.
    /// </remarks>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="relativePaths">
    /// The paths of the files relative to <paramref name="repositoryRootPath" />, with forward slashes or the
    /// directory separator of the operating system.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativePaths" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryRootPath" /> is <see langword="null" />, empty, or whitespace, or a path of
    /// <paramref name="relativePaths" /> is <see langword="null" />, empty, whitespace, rooted, or does not name a
    /// path below <paramref name="repositoryRootPath" />.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task PrepareAsync(string repositoryRootPath, IReadOnlyCollection<string> relativePaths,
        CancellationToken cancellationToken = default);
}
