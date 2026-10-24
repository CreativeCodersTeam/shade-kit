namespace CreativeCoders.ShadeKit.Core.Features.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a feature id does not match the rules of
/// <see cref="FeatureIds" />.
/// </summary>
/// <param name="featureId">The feature id that is not valid.</param>
public class InvalidFeatureIdException(string featureId)
    : Exception($"'{featureId}' is not a valid feature id.")
{
    /// <summary>
    /// Gets the feature id that is not valid.
    /// </summary>
    /// <value>The id as it was passed to the check.</value>
    public string FeatureId { get; } = featureId;
}
