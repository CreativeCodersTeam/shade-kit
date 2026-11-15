namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Checks whether a tag exists in a remote git repository.
/// </summary>
public interface IGitRemoteTagChecker
{
    /// <summary>
    /// Checks whether the specified tag exists in the remote repository at the specified URL.
    /// </summary>
    /// <remarks>
    /// The check never asks for credentials and never fails because of <c>git</c> or the network: a missing
    /// <c>git</c>, a <c>git</c> that cannot be started, and a failed call give <see cref="GitRemoteTagState.Unknown" />.
    /// </remarks>
    /// <param name="remoteUrl">The <c>https://</c> URL of the remote repository.</param>
    /// <param name="tagName">The name of the tag without <c>refs/tags/</c>, for example <c>v0.8.0</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous check. The task result is <see cref="GitRemoteTagState.Found" /> if
    /// the tag exists, <see cref="GitRemoteTagState.Missing" /> if the repository was read and has no such tag, and
    /// <see cref="GitRemoteTagState.Unknown" /> if the repository could not be read.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="remoteUrl" /> or <paramref name="tagName" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="remoteUrl" /> is not an absolute <c>https://</c> URL, or <paramref name="tagName" /> is empty,
    /// starts with <c>-</c>, or contains whitespace, one of the glob characters <c>*</c>, <c>?</c>, <c>[</c>, and
    /// <c>\</c>, or one of the characters <c>^</c>, <c>~</c>, and <c>:</c>, which a ref name cannot contain.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<GitRemoteTagState> CheckTagAsync(string remoteUrl, string tagName,
        CancellationToken cancellationToken = default);
}
