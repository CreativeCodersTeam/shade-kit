using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Determines the line ending that the git checkout configuration prescribes for a file in a repository.
/// </summary>
public interface IGitLineEndingResolver
{
    /// <summary>
    /// Gets the line ending that the git attributes and the git configuration prescribe for the specified file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Evaluates <c>text</c>, <c>crlf</c>, and <c>eol</c> as git does on checkout; the legacy <c>crlf</c> counts only
    /// while <c>text</c> is unspecified. The first matching rule decides:
    /// </para>
    /// <list type="number">
    /// <item><description><c>-text</c> (also <c>binary</c>, <c>-crlf</c>): git does not decide.</description></item>
    /// <item><description><c>eol=lf</c> or <c>eol=crlf</c>: that line ending.</description></item>
    /// <item><description><c>text=input</c> or <c>crlf=input</c>: <see cref="LineEnding.Lf" />.</description></item>
    /// <item><description>
    /// <c>core.autocrlf</c> true: <see cref="LineEnding.CrLf" />; <c>input</c>: <see cref="LineEnding.Lf" />.
    /// </description></item>
    /// <item><description>
    /// <c>core.eol</c> (<c>lf</c>, <c>crlf</c>, or <c>native</c>) for a file with <c>text</c> or <c>text=auto</c>.
    /// </description></item>
    /// </list>
    /// <para>
    /// If <c>git</c> cannot be started or fails, git does not decide; no exception is thrown.
    /// </para>
    /// </remarks>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="relativePath">
    /// The path of the file relative to <paramref name="repositoryRootPath" />, with forward slashes or the directory
    /// separator of the operating system.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the line ending that git
    /// prescribes, or <see langword="null" /> if git does not decide.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryRootPath" /> or <paramref name="relativePath" /> is <see langword="null" />, empty,
    /// or whitespace, or <paramref name="relativePath" /> is rooted or does not name a path below
    /// <paramref name="repositoryRootPath" />.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<LineEnding?> GetLineEndingAsync(string repositoryRootPath, string relativePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the git attributes of several files at once and keeps them, so a later
    /// <see cref="GetLineEndingAsync" /> call for one of these files starts no <c>git check-attr</c> process of its
    /// own.
    /// </summary>
    /// <remarks>
    /// The attributes are kept for the lifetime of the instance. If <c>git</c> fails, nothing is kept or thrown.
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
    Task PreloadAttributesAsync(string repositoryRootPath, IReadOnlyCollection<string> relativePaths,
        CancellationToken cancellationToken = default);
}
