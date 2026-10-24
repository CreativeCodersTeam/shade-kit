using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Git.Warnings;

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
    /// If <c>git</c> is not found, git does not decide. If <c>git</c> cannot be started or fails, git does not decide
    /// and the result carries a <see cref="GitAttributesUnavailableWarning" />. Either failure is kept per repository
    /// root for the lifetime of the instance, so no further <c>git</c> process is started for that repository; a
    /// canceled call is not kept. No exception is thrown for a failing <c>git</c>.
    /// </para>
    /// </remarks>
    /// <param name="file">The file whose line ending is determined; the file need not exist.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the line ending that git
    /// prescribes, or <see langword="null" /> if git does not decide, and the warning of a <c>git</c> that failed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="file" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<GitLineEndingResult> GetLineEndingAsync(RepositoryFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the git attributes of several files at once and keeps them, so a later
    /// <see cref="GetLineEndingAsync" /> call for one of these files starts no <c>git check-attr</c> process of its
    /// own.
    /// </summary>
    /// <remarks>
    /// The attributes are kept for the lifetime of the instance. If <c>git</c> fails, the failure is kept for the
    /// repository as by <see cref="GetLineEndingAsync" />, and nothing is thrown.
    /// </remarks>
    /// <param name="files">The files, which may belong to different repositories.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="files" /> is <see langword="null" /> or contains <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task PreloadAttributesAsync(IReadOnlyCollection<RepositoryFile> files,
        CancellationToken cancellationToken = default);
}
