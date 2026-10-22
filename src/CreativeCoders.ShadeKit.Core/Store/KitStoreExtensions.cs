using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Provides the knowledge about the location of a SHADE kit store that is derived from its root directory.
/// </summary>
internal static class KitStoreExtensions
{
    /// <summary>
    /// Gets the full path of the root of the git repository that contains the specified store.
    /// </summary>
    /// <remarks>
    /// The repository root is the parent directory of <see cref="IKitStore.RootPath" />, because the SHADE kit root
    /// directory <c>.shadekit</c> lies at the root of the git repository.
    /// </remarks>
    /// <param name="store">The SHADE kit store.</param>
    /// <returns>The full path of the parent directory of the SHADE kit root directory.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// The root directory of <paramref name="store" /> has no parent directory.
    /// </exception>
    public static string GetRepositoryRootPath(this IKitStore store)
    {
        Ensure.NotNull(store);

        return Path.GetDirectoryName(store.RootPath)
               ?? throw new ArgumentException(
                   $"The SHADE kit root directory '{store.RootPath}' has no parent directory.", nameof(store));
    }
}
