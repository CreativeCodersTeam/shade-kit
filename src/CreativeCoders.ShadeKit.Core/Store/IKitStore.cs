namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Provides read access to the content of a SHADE kit store, the <c>.shadekit</c> directory of a SHADE kit root.
/// </summary>
public interface IKitStore
{
    /// <summary>
    /// Gets the names of all features in the SHADE kit store.
    /// </summary>
    /// <returns>The names of the directories in the <c>features</c> directory of the store.</returns>
    /// <exception cref="FeaturesDirectoryNotFoundException">The store contains no <c>features</c> directory.</exception>
    IEnumerable<string> GetFeatureNames();
}
