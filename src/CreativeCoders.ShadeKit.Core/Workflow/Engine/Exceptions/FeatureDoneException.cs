namespace CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a phase command would change the phases of a feature that is done.
/// </summary>
/// <param name="featureId">The id of the feature that is done.</param>
public class FeatureDoneException(string featureId)
    : Exception($"The feature '{featureId}' is done; its phases cannot change any more.")
{
    /// <summary>
    /// Gets the id of the feature that is done.
    /// </summary>
    /// <value>The feature id, for example <c>gh-142-login-sso</c>.</value>
    public string FeatureId { get; } = featureId;
}
