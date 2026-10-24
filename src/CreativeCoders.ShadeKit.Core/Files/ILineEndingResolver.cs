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
    /// The first source that decides wins: the git checkout configuration (see
    /// <see cref="IGitLineEndingResolver.GetLineEndingAsync" />), then the <c>end_of_line</c> property of the
    /// <c>.editorconfig</c> files (see <see cref="IEditorConfigLineEndingResolver.GetLineEnding" />), and finally the
    /// line ending of the operating system: <see cref="LineEnding.CrLf" /> on Windows and <see cref="LineEnding.Lf" />
    /// everywhere else. The file does not need to exist.
    /// <para>
    /// If an <c>.editorconfig</c> file cannot be read or cannot be evaluated because of its content, the
    /// <c>.editorconfig</c> files do not decide, the line ending of the operating system is used, and the result
    /// carries an <see cref="EditorConfigUnreadableWarning" /> or an <see cref="EditorConfigInvalidWarning" />.
    /// </para>
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
    /// The git attributes of all files are read at once (see
    /// <see cref="IGitLineEndingResolver.PreloadAttributesAsync" />). Calling this method is optional; it does not
    /// change which line ending <see cref="GetLineEndingAsync" /> returns.
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
