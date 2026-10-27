using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Creates the initial phase state of a profile, and normalizes and validates stored phase fields against the
/// profile, which applies live.
/// </summary>
internal static class PhaseStateReader
{
    private const string StatusField = "status";

    private const string CurrentPhaseField = "currentPhase";

    private const string PhasesField = "phases";

    private const string PendingTransitionField = "pendingTransition";

    /// <summary>
    /// Creates the initial phase state: the first phase in progress, every other phase pending.
    /// </summary>
    public static FeaturePhaseState CreateInitial(WorkflowProfile profile, string featureId, string featureFilePath)
    {
        var firstPhase = profile.Phases[0].Name;

        var phases = profile.Phases.Select(x =>
            KeyValuePair.Create(x.Name, x.Name == firstPhase ? PhaseState.InProgress : PhaseState.Pending));

        return new FeaturePhaseState(profile, featureId, featureFilePath, FeatureStatus.Active, firstPhase, phases,
            null);
    }

    /// <summary>
    /// Normalizes and validates the stored phase fields against the profile.
    /// </summary>
    /// <exception cref="InvalidPhaseStateException">The stored fields do not fit the profile or contradict each other.</exception>
    public static FeaturePhaseState Read(WorkflowProfile profile, StoredPhaseState stored)
    {
        if (stored.CurrentPhase is null)
        {
            ValidateWithoutCurrentPhase(stored);

            return CreateInitial(profile, stored.FeatureId, stored.FeatureFilePath);
        }

        ValidateInProfile(profile, stored, CurrentPhaseField, stored.CurrentPhase);

        if (stored.PendingTransition is not null)
        {
            ValidateInProfile(profile, stored, PendingTransitionField, stored.PendingTransition);
        }

        var state = new FeaturePhaseState(profile, stored.FeatureId, stored.FeatureFilePath, stored.Status,
            stored.CurrentPhase, NormalizePhases(profile, stored.Phases), stored.PendingTransition);

        if (state.Status == FeatureStatus.Done)
        {
            ValidateDone(state);
        }
        else
        {
            ValidateActive(state);
        }

        return state;
    }

    // An old feature has no phase fields at all; a feature with other phase fields but no current phase was edited
    // by hand or by another tool, and guessing its current phase could move it.
    private static void ValidateWithoutCurrentPhase(StoredPhaseState stored)
    {
        if (stored.PendingTransition is not null)
        {
            throw Invalid(stored.FeatureFilePath, PendingTransitionField,
                $"names the phase '{stored.PendingTransition}', but the field '{CurrentPhaseField}' is missing.");
        }

        if (stored.Phases is { Count: > 0 })
        {
            throw Invalid(stored.FeatureFilePath, CurrentPhaseField,
                $"is missing, but the field '{PhasesField}' is set.");
        }

        if (stored.Status == FeatureStatus.Done)
        {
            throw Invalid(stored.FeatureFilePath, CurrentPhaseField,
                $"is missing, but the field '{StatusField}' says the feature is {FeatureStatus.Done.ToJsonName()}.");
        }
    }

    private static void ValidateInProfile(WorkflowProfile profile, StoredPhaseState stored, string field,
        string phase)
    {
        if (profile.IndexOf(phase) < 0)
        {
            throw Invalid(stored.FeatureFilePath, field,
                $"names the phase '{phase}', which is not a phase of the workflow profile '{profile.Name}'.");
        }
    }

    // Profile phases come first, in profile order, with a missing entry as pending; entries of phases the profile
    // no longer has follow in their stored order, so a later write keeps them.
    private static IEnumerable<KeyValuePair<string, PhaseState>> NormalizePhases(WorkflowProfile profile,
        IReadOnlyDictionary<string, PhaseState>? storedPhases)
    {
        var profilePhases = profile.Phases.Select(x => KeyValuePair.Create(x.Name,
            storedPhases is not null && storedPhases.TryGetValue(x.Name, out var state) ? state : PhaseState.Pending));

        var extraPhases = storedPhases?.Where(x => profile.IndexOf(x.Key) < 0) ?? [];

        return profilePhases.Concat(extraPhases);
    }

    // A done feature only needs a consistent end state: the position of its current phase is not checked, so a
    // phase added to the end of the profile later does not make finished features invalid.
    private static void ValidateDone(FeaturePhaseState state)
    {
        if (state.CurrentPhaseState != PhaseState.Approved)
        {
            throw Invalid(state.FeatureFilePath, PhasesField,
                $"gives the current phase '{state.CurrentPhase}' the state {state.CurrentPhaseState.ToJsonName()}, " +
                $"but the feature is {FeatureStatus.Done.ToJsonName()}, which needs " +
                $"{PhaseState.Approved.ToJsonName()}.");
        }

        if (state.PendingTransition is not null)
        {
            throw Invalid(state.FeatureFilePath, PendingTransitionField,
                $"names the phase '{state.PendingTransition}', but the feature is {FeatureStatus.Done.ToJsonName()}.");
        }
    }

    private static void ValidateActive(FeaturePhaseState state)
    {
        switch (state.CurrentPhaseState)
        {
            case PhaseState.InProgress when state.PendingTransition is not null:
                throw Invalid(state.FeatureFilePath, PendingTransitionField,
                    $"names the phase '{state.PendingTransition}', but the current phase '{state.CurrentPhase}' is " +
                    $"{PhaseState.InProgress.ToJsonName()}, not {PhaseState.AwaitingApproval.ToJsonName()}.");

            case PhaseState.InProgress:
                break;

            case PhaseState.AwaitingApproval:
                ValidatePendingTransition(state);
                break;

            default:
                throw Invalid(state.FeatureFilePath, PhasesField,
                    $"gives the current phase '{state.CurrentPhase}' the state " +
                    $"{state.CurrentPhaseState.ToJsonName()}, but the current phase of an " +
                    $"{FeatureStatus.Active.ToJsonName()} feature is {PhaseState.InProgress.ToJsonName()} or " +
                    $"{PhaseState.AwaitingApproval.ToJsonName()}.");
        }
    }

    private static void ValidatePendingTransition(FeaturePhaseState state)
    {
        if (state.PendingTransition is null)
        {
            throw Invalid(state.FeatureFilePath, PendingTransitionField,
                $"is missing, but the current phase '{state.CurrentPhase}' is " +
                $"{PhaseState.AwaitingApproval.ToJsonName()}.");
        }

        var currentIndex = state.Profile.IndexOf(state.CurrentPhase);
        var pendingIndex = state.Profile.IndexOf(state.PendingTransition);

        if (pendingIndex == currentIndex || pendingIndex > currentIndex + 1)
        {
            throw Invalid(state.FeatureFilePath, PendingTransitionField,
                $"names the phase '{state.PendingTransition}', which is neither the phase after the current phase " +
                $"'{state.CurrentPhase}' nor an earlier phase.");
        }
    }

    private static InvalidPhaseStateException Invalid(string featureFilePath, string field, string reason)
    {
        return new InvalidPhaseStateException(featureFilePath, field, reason);
    }
}
