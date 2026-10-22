using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Provides the knowledge about the features of a SHADE kit store that is shared by the feature services.
/// </summary>
internal static class KitStoreFeatureExtensions
{
    /// <summary>
    /// Determines whether the specified store has a feature with the specified id.
    /// </summary>
    /// <remarks>
    /// The id is checked before the path is built, so an invalid id never leads to a file system access.
    /// </remarks>
    /// <param name="store">The SHADE kit store.</param>
    /// <param name="featureId">The id of the feature.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="featureId" /> is a valid feature id and its feature folder exists;
    /// otherwise, <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    public static bool FeatureExists(this IKitStore store, string featureId)
    {
        Ensure.NotNull(store);

        return FeatureIds.IsValid(featureId) && store.DirectoryExists(KitStorePaths.GetFeatureDirPath(featureId));
    }
}
