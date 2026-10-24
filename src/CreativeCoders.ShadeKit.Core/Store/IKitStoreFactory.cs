using CreativeCoders.ShadeKit.Core.Store.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Creates <see cref="IKitStore" /> instances for the SHADE kit root of a git repository.
/// </summary>
/// <remarks>
/// Only a <c>.shadekit</c> directory at the root of the git repository is a SHADE kit root; that root is the
/// closest directory with a <c>.git</c> directory or file. <see cref="CreateForRepositoryRoot" /> skips the search.
/// </remarks>
public interface IKitStoreFactory
{
    /// <summary>
    /// Creates a store for the SHADE kit root of the git repository that contains the current directory.
    /// </summary>
    /// <returns>The store of the <c>.shadekit</c> directory at the repository root.</returns>
    /// <exception cref="NotAGitRepositoryException">The current directory is not inside a git repository.</exception>
    /// <exception cref="KitRootPathNotFoundException">The repository root contains no <c>.shadekit</c> directory.</exception>
    /// <exception cref="KitRootNotADirectoryException">The repository root contains a <c>.shadekit</c> file.</exception>
    IKitStore Create();

    /// <summary>
    /// Creates a store for the SHADE kit root of the git repository that contains the specified directory.
    /// </summary>
    /// <param name="path">The directory at which the search for the repository root starts.</param>
    /// <returns>The store of the <c>.shadekit</c> directory at the repository root.</returns>
    /// <exception cref="ArgumentException"><paramref name="path" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="NotAGitRepositoryException"><paramref name="path" /> is not inside a git repository.</exception>
    /// <exception cref="KitRootPathNotFoundException">The repository root contains no <c>.shadekit</c> directory.</exception>
    /// <exception cref="KitRootNotADirectoryException">The repository root contains a <c>.shadekit</c> file.</exception>
    IKitStore Create(string path);

    /// <summary>
    /// Creates a store for the SHADE kit root of the specified repository root directory.
    /// </summary>
    /// <remarks>
    /// Neither searches for a git repository nor checks that the SHADE kit root exists. A store for a directory that
    /// does not exist yet creates the missing directories when it writes a file.
    /// </remarks>
    /// <param name="repositoryRootPath">The path of the root directory of the git repository.</param>
    /// <returns>The store of the <c>.shadekit</c> directory in <paramref name="repositoryRootPath" />.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryRootPath" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    IKitStore CreateForRepositoryRoot(string repositoryRootPath);
}
