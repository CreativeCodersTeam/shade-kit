namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Creates <see cref="IKitStore" /> instances for the SHADE kit root that contains a given directory.
/// </summary>
/// <remarks>
/// A SHADE kit root is a directory that contains a <c>.shadekit</c> directory. The search starts at the given
/// directory and walks up to 19 parent directories; the closest root wins.
/// </remarks>
public interface IKitStoreFactory
{
    /// <summary>
    /// Creates a store for the SHADE kit root that contains the current directory.
    /// </summary>
    /// <returns>The store of the closest SHADE kit root at or above the current directory.</returns>
    /// <exception cref="KitRootPathNotFoundException">
    /// No SHADE kit root is found in the current directory or its parent directories.
    /// </exception>
    IKitStore Create();

    /// <summary>
    /// Creates a store for the SHADE kit root that contains the specified directory.
    /// </summary>
    /// <param name="path">The directory at which the search for the SHADE kit root starts.</param>
    /// <returns>The store of the closest SHADE kit root at or above <paramref name="path" />.</returns>
    /// <exception cref="KitRootPathNotFoundException">
    /// No SHADE kit root is found in <paramref name="path" /> or its parent directories.
    /// </exception>
    IKitStore Create(string path);
}
