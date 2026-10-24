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
    /// The attributes <c>text</c>, <c>crlf</c>, and <c>eol</c> are evaluated as git does when it checks out a file. The
    /// legacy attribute <c>crlf</c> counts only while <c>text</c> is unspecified or has a value that git does not
    /// know: <c>crlf</c> stands for <c>text</c>, <c>-crlf</c> for <c>-text</c>, and <c>crlf=input</c> for
    /// <c>text=input</c>. The macro <c>binary</c> is not read itself; it sets <c>-text</c>, which a later
    /// <c>text</c> attribute overrides. The rules are applied in this order, and the first one that matches decides:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// The attribute <c>text</c> is unset (<c>-text</c>, also through <c>binary</c> or <c>-crlf</c>): git does not
    /// decide, even if the attribute <c>eol</c> is set, because git checks out such a file unchanged.
    /// </description></item>
    /// <item><description>The attribute <c>eol</c> is <c>lf</c> or <c>crlf</c>: that line ending.</description></item>
    /// <item><description>
    /// The attribute <c>text</c> is <c>input</c> (also through <c>crlf=input</c>): <see cref="LineEnding.Lf" />.
    /// </description></item>
    /// <item><description>
    /// <c>core.autocrlf</c> is true (<c>true</c>, <c>yes</c>, or <c>on</c> in any case, or an integer other than
    /// <c>0</c> in git's syntax, such as <c>2</c>, <c>0x1</c>, or <c>1k</c>): <see cref="LineEnding.CrLf" />; it is <c>input</c>: <see cref="LineEnding.Lf" />. A
    /// <c>core.autocrlf</c> key without a value, which git treats as <c>true</c>, counts as not set.
    /// </description></item>
    /// <item><description>
    /// <c>core.eol</c> is set and the attribute <c>text</c> is set or <c>auto</c> (also through <c>crlf</c> or
    /// <c>crlf=auto</c>): <c>lf</c> gives
    /// <see cref="LineEnding.Lf" />, <c>crlf</c> gives <see cref="LineEnding.CrLf" />, and <c>native</c> gives the line
    /// ending of the operating system.
    /// </description></item>
    /// </list>
    /// <para>
    /// The attributes are read with <c>git check-attr</c> and the configuration with <c>git config</c>, both run in
    /// <paramref name="repositoryRootPath" />, so the repository, global, and system configuration all apply. The
    /// file does not need to exist. If <c>git</c> cannot be started or exits with an unexpected exit code, git does
    /// not decide; no exception is thrown in that case.
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
    /// A caller that writes several files in a row, such as <c>shade-kit init</c>, calls this method once before the
    /// first write. The attributes are kept for the lifetime of the instance. If <c>git</c> cannot be started or fails,
    /// nothing is kept and no exception is thrown; each file then reads its attributes when its line ending is
    /// determined.
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
