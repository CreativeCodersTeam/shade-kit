namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Locates the root directory of the git repository that contains a given directory.
/// </summary>
public interface IGitRepositoryLocator
{
    /// <summary>
    /// Gets the root directory of the git repository that contains the specified directory.
    /// </summary>
    /// <remarks>
    /// The search walks up from <paramref name="path" /> and stops at the closest directory that contains a
    /// <c>.git</c> directory or file (a worktree or submodule), so the root of a nested repository wins over the root
    /// of the repository around it. A relative <paramref name="path" /> is resolved against the current directory.
    /// </remarks>
    /// <param name="path">The directory at which the search starts.</param>
    /// <returns>
    /// The full path of the repository root, without a trailing directory separator unless the repository root is a
    /// file system root such as <c>/</c> or <c>C:\</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="path" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="NotAGitRepositoryException">
    /// <paramref name="path" /> is not inside a git repository.
    /// </exception>
    string GetRepositoryRootPath(string path);
}
