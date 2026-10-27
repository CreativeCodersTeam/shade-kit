namespace CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;

/// <summary>
/// Represents the exception that is thrown when <c>phase approve</c> or <c>phase reject</c> names a phase to which no
/// transition is pending.
/// </summary>
/// <param name="featureId">The id of the feature.</param>
/// <param name="phase">The name of the phase the command names.</param>
/// <param name="pendingTransition">
/// The target phase of the transition that is actually pending, or <see langword="null" /> if none is pending.
/// </param>
public class NoPendingTransitionException(string featureId, string phase, string? pendingTransition)
    : Exception(pendingTransition is null
        ? $"The feature '{featureId}' has no pending transition to '{phase}'; no transition is pending."
        : $"The feature '{featureId}' has no pending transition to '{phase}'; the transition to " +
          $"'{pendingTransition}' is pending.")
{
    /// <summary>
    /// Gets the id of the feature.
    /// </summary>
    /// <value>The feature id, for example <c>gh-142-login-sso</c>.</value>
    public string FeatureId { get; } = featureId;

    /// <summary>
    /// Gets the name of the phase the command names.
    /// </summary>
    /// <value>The phase name as it was passed, for example the argument of <c>shade-kit phase approve</c>.</value>
    public string Phase { get; } = phase;

    /// <summary>
    /// Gets the target phase of the transition that is actually pending.
    /// </summary>
    /// <value>The name of the target phase, or <see langword="null" /> if no transition is pending.</value>
    public string? PendingTransition { get; } = pendingTransition;
}
