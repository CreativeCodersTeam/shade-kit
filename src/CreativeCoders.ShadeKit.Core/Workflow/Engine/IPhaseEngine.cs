using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Decides the phase transitions of a feature from its workflow profile and its phase state.
/// </summary>
/// <remarks>
/// The engine is pure: it reads no file, no clock, and no git configuration, and it writes nothing. It knows no
/// profile or phase by name; the order of the phases, their skills, and whether they can be reopened come from the
/// <see cref="WorkflowProfile" /> of the state, and the gates of the transitions from the caller. A mutating request
/// either throws or returns the change the caller persists, with the content of the <c>history</c> entry to append.
/// Requests on a done feature are checked first, then whether the phase is in the profile, then the transition rule.
/// </remarks>
public interface IPhaseEngine
{
    /// <summary>
    /// Creates the initial phase state of a new feature: the first phase of the profile in progress, every other
    /// phase pending, and no pending transition.
    /// </summary>
    /// <param name="profile">The workflow profile of the feature.</param>
    /// <param name="featureId">The id of the feature.</param>
    /// <param name="featureFilePath">The repository-relative path of the <c>feature.json</c> file of the feature.</param>
    /// <returns>The initial state of an active feature.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="featureId" /> or <paramref name="featureFilePath" /> is <see langword="null" />, empty, or
    /// whitespace.
    /// </exception>
    FeaturePhaseState CreateInitialState(WorkflowProfile profile, string featureId, string featureFilePath);

    /// <summary>
    /// Validates the stored phase fields of a feature against its workflow profile and normalizes them.
    /// </summary>
    /// <remarks>
    /// A feature without <c>currentPhase</c> was created before the phase engine and gets the initial state of the
    /// profile; that is valid only if it has no <c>pendingTransition</c>, no or empty <c>phases</c>, and is active.
    /// A phase of the profile without an entry counts as pending. Entries of phases the profile does not have are kept
    /// after the phases of the profile but ignored by every rule. Only the current phase is checked: an active feature
    /// needs it in progress without a pending transition, or awaiting approval with a pending transition to the next
    /// or an earlier phase; a done feature needs it approved without a pending transition.
    /// </remarks>
    /// <param name="profile">The workflow profile of the feature.</param>
    /// <param name="stored">The phase fields as they are stored in <c>feature.json</c>.</param>
    /// <returns>The validated state.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="profile" /> or <paramref name="stored" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The feature id or the file path of <paramref name="stored" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidPhaseStateException">
    /// The stored fields name a phase that is not in the profile or contradict each other.
    /// </exception>
    FeaturePhaseState ReadState(WorkflowProfile profile, StoredPhaseState stored);

    /// <summary>
    /// Decides a <c>phase start</c> request: requests the transition to the next phase or the reopen of an earlier
    /// phase, or reports a pending transition or the current phase without a change.
    /// </summary>
    /// <param name="state">The phase state of the feature.</param>
    /// <param name="phase">The name of the requested phase.</param>
    /// <param name="gates">
    /// The gates of the transitions into the phases of the profile, by target phase; a phase without an entry has
    /// <see cref="TransitionGate.Default" />. A gate without approval performs a forward transition at once. A reopen
    /// always has <see cref="TransitionGate.Default" />. The gate of a pending forward transition always requires an
    /// approval, because the transition waits for one.
    /// </param>
    /// <returns>The decision with the outcome, and the change to persist if the request changed the state.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="state" />, <paramref name="phase" />, or <paramref name="gates" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="FeatureDoneException">The feature is done.</exception>
    /// <exception cref="PhaseNotInProfileException"><paramref name="phase" /> is not a phase of the profile.</exception>
    /// <exception cref="PhaseTransitionNotAllowedException">
    /// The request would skip phases, another transition is pending, an earlier phase is requested while a transition
    /// is pending, or the earlier phase cannot be reopened.
    /// </exception>
    PhaseStartDecision Start(FeaturePhaseState state, string phase,
        IReadOnlyDictionary<string, TransitionGate> gates);

    /// <summary>
    /// Decides a <c>phase approve</c> request: performs the pending transition to the specified phase.
    /// </summary>
    /// <remarks>
    /// A forward transition approves the current phase. A reopen sets every phase after the target phase to pending.
    /// Either way the target phase becomes the current phase, in progress.
    /// </remarks>
    /// <param name="state">The phase state of the feature.</param>
    /// <param name="phase">The name of the target phase of the pending transition.</param>
    /// <returns>The decision with the approved transition and the change to persist.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="state" /> or <paramref name="phase" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="FeatureDoneException">The feature is done.</exception>
    /// <exception cref="PhaseNotInProfileException"><paramref name="phase" /> is not a phase of the profile.</exception>
    /// <exception cref="NoPendingTransitionException">No transition to <paramref name="phase" /> is pending.</exception>
    /// <exception cref="PhaseTransitionNotAllowedException">
    /// The pending transition reopens a phase that the profile no longer allows to reopen; it can only be rejected.
    /// </exception>
    PhaseApproveDecision Approve(FeaturePhaseState state, string phase);

    /// <summary>
    /// Decides a <c>phase reject</c> request: cancels the pending transition to the specified phase, so the current
    /// phase is in progress again.
    /// </summary>
    /// <param name="state">The phase state of the feature.</param>
    /// <param name="phase">The name of the target phase of the pending transition.</param>
    /// <param name="note">
    /// The note of the rejection, for example the failed checklist items; it is trimmed, may contain line breaks, and
    /// is dropped if it is <see langword="null" />, empty, or whitespace.
    /// </param>
    /// <returns>The decision with the rejected transition, the note, and the change to persist.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="state" /> or <paramref name="phase" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="FeatureDoneException">The feature is done.</exception>
    /// <exception cref="PhaseNotInProfileException"><paramref name="phase" /> is not a phase of the profile.</exception>
    /// <exception cref="NoPendingTransitionException">No transition to <paramref name="phase" /> is pending.</exception>
    PhaseRejectDecision Reject(FeaturePhaseState state, string phase, string? note);

    /// <summary>
    /// Decides a <c>phase complete</c> request: approves the last phase of the profile and makes the feature done.
    /// </summary>
    /// <param name="state">The phase state of the feature.</param>
    /// <returns>The decision with the completed phase and the change to persist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state" /> is <see langword="null" />.</exception>
    /// <exception cref="FeatureDoneException">The feature is done.</exception>
    /// <exception cref="PhaseCompleteNotAllowedException">
    /// The current phase is not the last phase of the profile or is not in progress.
    /// </exception>
    PhaseCompleteDecision Complete(FeaturePhaseState state);

    /// <summary>
    /// Gets the skills an agent loads for the specified phase of a workflow profile.
    /// </summary>
    /// <param name="profile">The workflow profile of the feature.</param>
    /// <param name="phase">The name of the phase.</param>
    /// <returns>The phase skill and the general skills of <paramref name="phase" />.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="profile" /> or <paramref name="phase" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="PhaseNotInProfileException"><paramref name="phase" /> is not a phase of the profile.</exception>
    PhaseSkills GetSkills(WorkflowProfile profile, string phase);

    /// <summary>
    /// Gets the possible next steps of a feature without changing anything; allowed on a done feature.
    /// </summary>
    /// <param name="state">The phase state of the feature.</param>
    /// <param name="gates">
    /// The gates of the transitions into the phases of the profile, by target phase; a phase without an entry has
    /// <see cref="TransitionGate.Default" />. The gate of a pending forward transition always requires an approval,
    /// because the transition waits for one; a pending reopen has <see cref="TransitionGate.Default" />.
    /// </param>
    /// <returns>The possible next steps.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="state" /> or <paramref name="gates" /> is <see langword="null" />.
    /// </exception>
    PhaseNextSteps GetNext(FeaturePhaseState state, IReadOnlyDictionary<string, TransitionGate> gates);
}
