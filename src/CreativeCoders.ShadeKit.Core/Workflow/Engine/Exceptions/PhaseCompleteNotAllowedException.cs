using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;

/// <summary>
/// Represents the exception that is thrown when <c>phase complete</c> runs while the current phase is not the last
/// phase of the profile or is not in progress.
/// </summary>
/// <param name="featureId">The id of the feature.</param>
/// <param name="currentPhase">The name of the current phase of the feature.</param>
/// <param name="currentState">The state of the current phase.</param>
/// <param name="lastPhase">The name of the last phase of the profile, the only phase that can be completed.</param>
public class PhaseCompleteNotAllowedException(
    string featureId,
    string currentPhase,
    PhaseState currentState,
    string lastPhase)
    : Exception($"The feature '{featureId}' cannot be completed: only the last phase '{lastPhase}' can be completed " +
                $"while it is in progress, but the current phase '{currentPhase}' is {currentState.ToJsonName()}.")
{
    /// <summary>
    /// Gets the id of the feature.
    /// </summary>
    /// <value>The feature id, for example <c>gh-142-login-sso</c>.</value>
    public string FeatureId { get; } = featureId;

    /// <summary>
    /// Gets the name of the current phase of the feature.
    /// </summary>
    /// <value>The name of a phase of the profile of the feature.</value>
    public string CurrentPhase { get; } = currentPhase;

    /// <summary>
    /// Gets the state of the current phase.
    /// </summary>
    /// <value>One of the enumeration values.</value>
    public PhaseState CurrentState { get; } = currentState;

    /// <summary>
    /// Gets the name of the last phase of the profile.
    /// </summary>
    /// <value>The name of the only phase that <c>phase complete</c> can complete.</value>
    public string LastPhase { get; } = lastPhase;
}
