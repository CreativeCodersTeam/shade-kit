using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a phase command requests a transition that the workflow profile does
/// not allow from the current phase state of the feature.
/// </summary>
/// <param name="featureId">The id of the feature.</param>
/// <param name="phase">The name of the requested phase.</param>
/// <param name="currentPhase">The name of the current phase of the feature.</param>
/// <param name="currentState">The state of the current phase.</param>
/// <param name="pendingTransition">
/// The target phase of the pending transition, or <see langword="null" /> if no transition is pending.
/// </param>
/// <param name="reason">One of the enumeration values that specifies why the transition is not allowed.</param>
public class PhaseTransitionNotAllowedException(
    string featureId,
    string phase,
    string currentPhase,
    PhaseState currentState,
    string? pendingTransition,
    PhaseTransitionRefusalReason reason)
    : Exception($"The feature '{featureId}' cannot move to the phase '{phase}' ({reason.ToJsonName()}); the current phase " +
                $"'{currentPhase}' is {currentState.ToJsonName()}.")
{
    /// <summary>
    /// Gets the id of the feature.
    /// </summary>
    /// <value>The feature id, for example <c>gh-142-login-sso</c>.</value>
    public string FeatureId { get; } = featureId;

    /// <summary>
    /// Gets the name of the requested phase.
    /// </summary>
    /// <value>The phase name as it was passed, for example the argument of <c>shade-kit phase start</c>.</value>
    public string Phase { get; } = phase;

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
    /// Gets the target phase of the pending transition.
    /// </summary>
    /// <value>The name of the target phase, or <see langword="null" /> if no transition is pending.</value>
    public string? PendingTransition { get; } = pendingTransition;

    /// <summary>
    /// Gets the reason why the transition is not allowed.
    /// </summary>
    /// <value>One of the enumeration values.</value>
    public PhaseTransitionRefusalReason Reason { get; } = reason;
}
