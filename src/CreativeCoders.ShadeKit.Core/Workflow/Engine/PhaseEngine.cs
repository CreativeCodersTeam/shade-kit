using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Decides the phase transitions of a feature from its workflow profile and its phase state, without any I/O.
/// </summary>
/// <remarks>
/// The engine works on every <see cref="WorkflowProfile" />: it knows no profile or phase by name.
/// </remarks>
public sealed class PhaseEngine : IPhaseEngine
{
    /// <summary>
    /// The note of the <c>approve</c> history entry of a transition that was performed without an approval.
    /// </summary>
    internal const string ApprovalOffNote = "approval off";

    /// <inheritdoc />
    public FeaturePhaseState CreateInitialState(WorkflowProfile profile, string featureId, string featureFilePath)
    {
        Ensure.NotNull(profile);
        Ensure.IsNotNullOrWhitespace(featureId);
        Ensure.IsNotNullOrWhitespace(featureFilePath);

        return PhaseStateReader.CreateInitial(profile, featureId, featureFilePath);
    }

    /// <inheritdoc />
    public FeaturePhaseState ReadState(WorkflowProfile profile, StoredPhaseState stored)
    {
        Ensure.NotNull(profile);
        Ensure.NotNull(stored);
        Ensure.IsNotNullOrWhitespace(stored.FeatureId);
        Ensure.IsNotNullOrWhitespace(stored.FeatureFilePath);

        return PhaseStateReader.Read(profile, stored);
    }

    /// <inheritdoc />
    public PhaseStartDecision Start(FeaturePhaseState state, string phase,
        IReadOnlyDictionary<string, TransitionGate> gates)
    {
        Ensure.NotNull(state);
        Ensure.NotNull(phase);
        Ensure.NotNull(gates);

        var target = GetPhaseToChange(state, phase);

        return state.Pending is { } pending
            ? StartWhilePending(state, target, pending, gates)
            : StartWhileInProgress(state, target, gates);
    }

    /// <inheritdoc />
    public PhaseApproveDecision Approve(FeaturePhaseState state, string phase)
    {
        Ensure.NotNull(state);
        Ensure.NotNull(phase);

        var target = GetPhaseToChange(state, phase);
        var pending = GetPendingTransitionTo(state, target.Name);

        if (pending.Kind == PhaseTransitionKind.Reopen && !target.CanReopen)
        {
            throw CreateNotAllowed(state, target.Name, PhaseTransitionRefusalReason.PhaseCannotBeReopened);
        }

        var newState = pending.Kind == PhaseTransitionKind.Forward
            ? MoveForward(state, target.Name)
            : state.With(target.Name, null, CreateReopenedPhases(state.Profile, target.Name));

        var change = new PhaseStateChange(newState,
            new PhaseHistoryRecord(PhaseHistoryAction.Approve, pending.From, pending.To, null));

        return new PhaseApproveDecision(pending, change);
    }

    /// <inheritdoc />
    public PhaseRejectDecision Reject(FeaturePhaseState state, string phase, string? note)
    {
        Ensure.NotNull(state);
        Ensure.NotNull(phase);

        var target = GetPhaseToChange(state, phase);
        var pending = GetPendingTransitionTo(state, target.Name);
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        var change = new PhaseStateChange(
            state.With(state.CurrentPhase, null,
                new Dictionary<string, PhaseState> { [state.CurrentPhase] = PhaseState.InProgress }),
            new PhaseHistoryRecord(PhaseHistoryAction.Reject, pending.From, pending.To, normalizedNote));

        return new PhaseRejectDecision(pending, normalizedNote, change);
    }

    /// <inheritdoc />
    public PhaseCompleteDecision Complete(FeaturePhaseState state)
    {
        Ensure.NotNull(state);

        EnsureNotDone(state);

        var lastPhase = state.Profile.Phases[^1].Name;

        if (state.CurrentPhase != lastPhase || state.CurrentPhaseState != PhaseState.InProgress)
        {
            throw new PhaseCompleteNotAllowedException(state.FeatureId, state.CurrentPhase, state.CurrentPhaseState,
                lastPhase);
        }

        var change = new PhaseStateChange(
            state.With(lastPhase, null, new Dictionary<string, PhaseState> { [lastPhase] = PhaseState.Approved },
                FeatureStatus.Done),
            new PhaseHistoryRecord(PhaseHistoryAction.Complete, lastPhase, null, null));

        return new PhaseCompleteDecision(lastPhase, change);
    }

    /// <inheritdoc />
    public PhaseSkills GetSkills(WorkflowProfile profile, string phase)
    {
        Ensure.NotNull(profile);
        Ensure.NotNull(phase);

        return CreateSkills(GetPhase(profile, phase));
    }

    /// <inheritdoc />
    public PhaseNextSteps GetNext(FeaturePhaseState state, IReadOnlyDictionary<string, TransitionGate> gates)
    {
        Ensure.NotNull(state);
        Ensure.NotNull(gates);

        if (state.Status == FeatureStatus.Done)
        {
            return new PhaseNextSteps(state, null, null, null, null);
        }

        var profile = state.Profile;

        if (state.Pending is { } pending)
        {
            return new PhaseNextSteps(state, null,
                CreateGatedStep(GetPhase(profile, pending.To), GetPendingGate(pending, gates)), null, null);
        }

        var currentIndex = profile.IndexOf(state.CurrentPhase);
        var current = profile.Phases[currentIndex];

        var forward = currentIndex + 1 < profile.Phases.Count
            ? profile.Phases[currentIndex + 1]
            : null;

        var reopen = profile.Phases
            .Take(currentIndex)
            .Where(x => x.CanReopen)
            .Select(CreateStep)
            .ToArray();

        return new PhaseNextSteps(state, CreateStep(current), null,
            forward is null ? null : CreateGatedStep(forward, GetGate(gates, forward.Name)), reopen);
    }

    private static PhaseStartDecision StartWhilePending(FeaturePhaseState state, WorkflowPhase target,
        PhaseTransition pending, IReadOnlyDictionary<string, TransitionGate> gates)
    {
        if (target.Name == pending.To)
        {
            return new PhaseStartDecision(PhaseStartOutcome.TransitionPending, pending, GetPendingGate(pending, gates),
                CreateSkills(target), state, null);
        }

        if (target.Name == state.CurrentPhase)
        {
            return new PhaseStartDecision(PhaseStartOutcome.ResumePending, pending, null, CreateSkills(target), state,
                null);
        }

        var reason = state.GetKind(state.CurrentPhase, target.Name) == PhaseTransitionKind.Reopen
            ? PhaseTransitionRefusalReason.ReopenWhilePending
            : PhaseTransitionRefusalReason.OtherTransitionPending;

        throw CreateNotAllowed(state, target.Name, reason);
    }

    private static PhaseStartDecision StartWhileInProgress(FeaturePhaseState state, WorkflowPhase target,
        IReadOnlyDictionary<string, TransitionGate> gates)
    {
        var currentIndex = state.Profile.IndexOf(state.CurrentPhase);
        var targetIndex = state.Profile.IndexOf(target.Name);

        if (targetIndex == currentIndex)
        {
            return new PhaseStartDecision(PhaseStartOutcome.AlreadyInProgress, null, null, CreateSkills(target), state,
                null);
        }

        if (targetIndex < currentIndex)
        {
            return RequestReopen(state, target);
        }

        if (targetIndex > currentIndex + 1)
        {
            throw CreateNotAllowed(state, target.Name, PhaseTransitionRefusalReason.SkipsPhases);
        }

        return StartForward(state, target, GetGate(gates, target.Name));
    }

    private static PhaseStartDecision RequestReopen(FeaturePhaseState state, WorkflowPhase target)
    {
        if (!target.CanReopen)
        {
            throw CreateNotAllowed(state, target.Name, PhaseTransitionRefusalReason.PhaseCannotBeReopened);
        }

        var transition = new PhaseTransition(PhaseTransitionKind.Reopen, state.CurrentPhase, target.Name);

        return CreateRequested(state, target, transition, PhaseHistoryAction.Reopen, TransitionGate.Default);
    }

    private static PhaseStartDecision StartForward(FeaturePhaseState state, WorkflowPhase target,
        TransitionGate gate)
    {
        var transition = new PhaseTransition(PhaseTransitionKind.Forward, state.CurrentPhase, target.Name);

        if (gate.Approval)
        {
            return CreateRequested(state, target, transition, PhaseHistoryAction.Start, gate);
        }

        var change = new PhaseStateChange(MoveForward(state, target.Name),
            new PhaseHistoryRecord(PhaseHistoryAction.Approve, transition.From, transition.To, ApprovalOffNote));

        return new PhaseStartDecision(PhaseStartOutcome.TransitionPerformed, transition, gate, CreateSkills(target),
            change.State, change);
    }

    private static PhaseStartDecision CreateRequested(FeaturePhaseState state, WorkflowPhase target,
        PhaseTransition transition, PhaseHistoryAction action, TransitionGate gate)
    {
        var change = new PhaseStateChange(
            state.With(state.CurrentPhase, target.Name,
                new Dictionary<string, PhaseState> { [state.CurrentPhase] = PhaseState.AwaitingApproval }),
            new PhaseHistoryRecord(action, transition.From, transition.To, null));

        return new PhaseStartDecision(PhaseStartOutcome.TransitionRequested, transition, gate, CreateSkills(target),
            change.State, change);
    }

    // An approved forward transition: the current phase is finished and the next phase becomes current.
    private static FeaturePhaseState MoveForward(FeaturePhaseState state, string nextPhase)
    {
        return state.With(nextPhase, null, new Dictionary<string, PhaseState>
        {
            [state.CurrentPhase] = PhaseState.Approved,
            [nextPhase] = PhaseState.InProgress
        });
    }

    // The reopened phase is in progress again and every later phase of the profile pending; the phases before it
    // and the preserved entries of phases the profile does not have keep their state.
    private static Dictionary<string, PhaseState> CreateReopenedPhases(WorkflowProfile profile, string reopenedPhase)
    {
        return profile.Phases
            .Skip(profile.IndexOf(reopenedPhase))
            .ToDictionary(x => x.Name, x => x.Name == reopenedPhase ? PhaseState.InProgress : PhaseState.Pending,
                StringComparer.Ordinal);
    }

    private static PhaseTransition GetPendingTransitionTo(FeaturePhaseState state, string phase)
    {
        return state.Pending is { } pending && pending.To == phase
            ? pending
            : throw new NoPendingTransitionException(state.FeatureId, phase, state.PendingTransition);
    }

    private static WorkflowPhase GetPhaseToChange(FeaturePhaseState state, string phase)
    {
        EnsureNotDone(state);

        return GetPhase(state.Profile, phase);
    }

    private static void EnsureNotDone(FeaturePhaseState state)
    {
        if (state.Status == FeatureStatus.Done)
        {
            throw new FeatureDoneException(state.FeatureId);
        }
    }

    private static WorkflowPhase GetPhase(WorkflowProfile profile, string phase)
    {
        return profile.TryGetPhase(phase, out var workflowPhase)
            ? workflowPhase
            : throw new PhaseNotInProfileException(phase, profile.Name, [.. profile.Phases.Select(x => x.Name)]);
    }

    // The gate of a pending transition: it was requested because its gate needed an approval, so it still needs one,
    // even if the gate was switched off since; the checklist is the current one. A reopen has the default gate.
    private static TransitionGate GetPendingGate(PhaseTransition pending,
        IReadOnlyDictionary<string, TransitionGate> gates)
    {
        return pending.Kind == PhaseTransitionKind.Reopen
            ? TransitionGate.Default
            : GetGate(gates, pending.To) with { Approval = true };
    }

    private static TransitionGate GetGate(IReadOnlyDictionary<string, TransitionGate> gates, string phase)
    {
        return gates.GetValueOrDefault(phase) ?? TransitionGate.Default;
    }

    private static PhaseTransitionNotAllowedException CreateNotAllowed(FeaturePhaseState state, string phase,
        PhaseTransitionRefusalReason reason)
    {
        return new PhaseTransitionNotAllowedException(state.FeatureId, phase, state.CurrentPhase,
            state.CurrentPhaseState, state.PendingTransition, reason);
    }

    private static PhaseSkills CreateSkills(WorkflowPhase phase)
    {
        return new PhaseSkills(phase.PhaseSkill, phase.GeneralSkills);
    }

    private static PhaseStep CreateStep(WorkflowPhase phase)
    {
        return new PhaseStep(phase.Name, phase.PhaseSkill);
    }

    private static GatedPhaseStep CreateGatedStep(WorkflowPhase phase, TransitionGate gate)
    {
        return new GatedPhaseStep(phase.Name, gate, phase.PhaseSkill);
    }
}
