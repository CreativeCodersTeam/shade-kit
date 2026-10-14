namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Creates <see cref="IKitStore" /> instances for the SHADE kit root of the git repository that contains a given
/// directory.
/// </summary>
/// <remarks>
/// The search walks up from the given directory to the root of the git repository, the closest directory that
/// contains a <c>.git</c> directory or file (a worktree or submodule). Only a <c>.shadekit</c> directory at that
/// repository root is a SHADE kit root; <c>.shadekit</c> directories below it are ignored.
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
}
