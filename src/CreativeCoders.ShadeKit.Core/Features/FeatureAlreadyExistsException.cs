using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the exception that is thrown when a new feature gets an id whose feature folder already exists.
/// </summary>
/// <remarks>
/// An existing id usually means that the feature was already created, for example on another branch. SHADE kit does
/// not add a suffix to the id, and nothing is changed.
/// </remarks>
/// <param name="featureId">The id of the feature that already exists.</param>
/// <param name="innerException">
/// The exception that caused this exception, for example the failed move of the new feature folder when another
/// process created the folder first; or <see langword="null" /> if there is none.
/// </param>
public class FeatureAlreadyExistsException(string featureId, Exception? innerException = null)
    : Exception($"The feature '{featureId}' already exists: its folder " +
        $"'{KitStorePaths.GetRepositoryFeatureDirPath(featureId)}' exists.", innerException)
{
    /// <summary>
    /// Gets the id of the feature that already exists.
    /// </summary>
    /// <value>The id of the new feature, whose folder already exists.</value>
    public string FeatureId { get; } = featureId;
}
