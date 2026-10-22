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
    /// <para>
    /// The id is checked before the store is accessed, so an invalid id never leads to a file system access.
    /// </para>
    /// <para>
    /// The id is compared ordinally with the real names of the feature folders from
    /// <see cref="IKitStore.GetFeatureNames" />, the same source the feature list uses. A check of the folder path
    /// would ignore case on a case-insensitive file system (macOS, Windows), so a folder whose name differs from the
    /// id only in case would be taken as the feature, although its name is not a valid feature id.
    /// </para>
    /// </remarks>
    /// <param name="store">The SHADE kit store.</param>
    /// <param name="featureId">The id of the feature.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="featureId" /> is a valid feature id and a feature folder whose name
    /// equals it ordinally exists; otherwise, <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    public static bool FeatureExists(this IKitStore store, string featureId)
    {
        Ensure.NotNull(store);

        return FeatureIds.IsValid(featureId)
               && store.GetFeatureNames().Contains(featureId, StringComparer.Ordinal);
    }
}
