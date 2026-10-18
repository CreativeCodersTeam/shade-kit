namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Creates <see cref="IKitStore" /> instances for the SHADE kit root of a git repository.
/// </summary>
/// <remarks>
/// <see cref="Create()" /> and <see cref="Create(string)" /> search for the root of the git repository that contains a
/// directory with <see cref="IGitRepositoryLocator" />: the closest directory that contains a <c>.git</c> directory
/// or file (a worktree or submodule). Only a <c>.shadekit</c> directory at that repository root is a SHADE kit root;
/// <c>.shadekit</c> directories below it are ignored. <see cref="CreateForKitRoot" /> skips the search for callers
/// that already know the SHADE kit root, for example while it is being created.
/// </remarks>
public interface IKitStoreFactory
{
    /// <summary>
    /// Creates a store for the SHADE kit root of the git repository that contains the current directory.
    /// </summary>
    /// <returns>The store of the <c>.shadekit</c> directory at the repository root.</returns>
    /// <exception cref="NotAGitRepositoryException">The current directory is not inside a git repository.</exception>
    /// <exception cref="KitRootPathNotFoundException">The repository root contains no <c>.shadekit</c> directory.</exception>
    IKitStore Create();

    /// <summary>
    /// Creates a store for the SHADE kit root of the git repository that contains the specified directory.
    /// </summary>
    /// <param name="path">The directory at which the search for the repository root starts.</param>
    /// <returns>The store of the <c>.shadekit</c> directory at the repository root.</returns>
    /// <exception cref="ArgumentException"><paramref name="path" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="NotAGitRepositoryException"><paramref name="path" /> is not inside a git repository.</exception>
    /// <exception cref="KitRootPathNotFoundException">The repository root contains no <c>.shadekit</c> directory.</exception>
    IKitStore Create(string path);

    /// <summary>
    /// Creates a store for the specified SHADE kit root directory.
    /// </summary>
    /// <remarks>
    /// Neither searches for a git repository nor checks that <paramref name="kitRootPath" /> exists. A store for a
    /// directory that does not exist yet creates the missing directories when it writes a file.
    /// </remarks>
    /// <param name="kitRootPath">The path of the <c>.shadekit</c> directory.</param>
    /// <returns>The store of <paramref name="kitRootPath" />.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="kitRootPath" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    IKitStore CreateForKitRoot(string kitRootPath);
}
