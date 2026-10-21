namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Reads information about a git repository and its configuration from <c>git</c>.
/// </summary>
public interface IGitRepositoryInfo
{
    /// <summary>
    /// Gets the name of the branch that is checked out in the specified repository.
    /// </summary>
    /// <remarks>
    /// The branch is read with <c>git symbolic-ref --short -q HEAD</c>, run in <paramref name="repositoryRootPath" />.
    /// The short name is returned, for example <c>feature/login-sso</c> for <c>refs/heads/feature/login-sso</c>.
    /// </remarks>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the branch name without
    /// leading or trailing whitespace, or <see langword="null" /> if <c>HEAD</c> is detached and no branch is
    /// checked out (exit code 1) or <c>git</c> succeeds without output.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryRootPath" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it exits with an exit code other than 0 or 1.
    /// </exception>
    Task<string?> GetCurrentBranchAsync(string repositoryRootPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the git identity that is configured for the specified repository.
    /// </summary>
    /// <remarks>
    /// The values are read with <c>git config user.name</c> and <c>git config user.email</c>, run in
    /// <paramref name="repositoryRootPath" />, so the repository, global, and system configuration all apply. A key
    /// counts as missing if <c>git</c> exits with exit code 1 (not set) or the value is empty or whitespace.
    /// </remarks>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the configured name and
    /// email address without leading or trailing whitespace.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryRootPath" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="GitIdentityNotConfiguredException">
    /// <c>user.name</c>, <c>user.email</c>, or both are not set.
    /// </exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it exits with an exit code other than 0 or 1.
    /// </exception>
    Task<GitIdentity> GetIdentityAsync(string repositoryRootPath, CancellationToken cancellationToken = default);
}
