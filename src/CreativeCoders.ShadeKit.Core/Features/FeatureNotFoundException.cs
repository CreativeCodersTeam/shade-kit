namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the exception that is thrown when a command names a feature whose feature folder does not exist.
/// </summary>
/// <param name="featureId">The id of the feature that does not exist.</param>
public class FeatureNotFoundException(string featureId)
    : Exception($"The feature '{featureId}' does not exist: there is no folder '.shadekit/features/{featureId}/'.")
{
    /// <summary>
    /// Gets the id of the feature that does not exist.
    /// </summary>
    /// <value>The feature id as it was passed, for example the argument of <c>shade-kit feature set</c>.</value>
    public string FeatureId { get; } = featureId;
}
