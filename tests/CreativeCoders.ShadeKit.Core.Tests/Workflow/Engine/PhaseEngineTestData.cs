using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

/// <summary>
/// Builds profiles, stored phase fields, and phase states for the tests of <see cref="PhaseEngine" />.
/// </summary>
internal static class PhaseEngineTestData
{
    public const string FeatureId = "gh-142-login-sso";

    public const string FeatureFilePath = ".shadekit/features/gh-142-login-sso/feature.json";

    public static IReadOnlyDictionary<string, TransitionGate> NoGates { get; } =
        new Dictionary<string, TransitionGate>();

    /// <summary>
    /// Gets a synthetic profile <c>alpha → beta → gamma → delta</c> with custom skills, in which <c>beta</c> cannot be
    /// reopened.
    /// </summary>
    public static WorkflowProfile Greek { get; } = new("greek",
    [
        new WorkflowPhase("alpha", "skill-alpha", []),
        new WorkflowPhase("beta", "skill-beta", ["general-one"], canReopen: false),
        new WorkflowPhase("gamma", "skill-gamma", ["general-one", "general-two"]),
        new WorkflowPhase("delta", "skill-delta", ["general-three"])
    ]);

    public static WorkflowProfile TwoPhases { get; } = new("pair",
    [
        new WorkflowPhase("first", "do-first", []),
        new WorkflowPhase("second", "do-second", ["helper"])
    ]);

    public static WorkflowProfile OnePhase { get; } = new("single", [new WorkflowPhase("only", "do-only", [])]);

    public static TheoryData<string> BuiltInProfileNames { get; } =
        [WorkflowProfileNames.Feature, WorkflowProfileNames.Bugfix, WorkflowProfileNames.Quick];

    /// <summary>
    /// Gets every pair of a built-in profile and a phase that has a next phase.
    /// </summary>
    public static TheoryData<string, int> BuiltInProfilePhasesWithNext { get; } = CreateBuiltInProfilePhases(
        (profile, index) => index + 1 < profile.Phases.Count);

    /// <summary>
    /// Gets every pair of a built-in profile and a phase that has an earlier phase which can be reopened.
    /// </summary>
    public static TheoryData<string, int> BuiltInProfilePhasesWithReopenableEarlierPhase { get; } =
        CreateBuiltInProfilePhases((profile, index) => profile.Phases.Take(index).Any(x => x.CanReopen));

    public static WorkflowProfile GetBuiltInProfile(string name)
    {
        return BuiltInWorkflowProfiles.All.Single(x => x.Name == name);
    }

    public static StoredPhaseState CreateStored(string? currentPhase,
        IEnumerable<KeyValuePair<string, PhaseState>>? phases = null, string? pendingTransition = null,
        FeatureStatus status = FeatureStatus.Active)
    {
        return new StoredPhaseState(FeatureId, FeatureFilePath, status, currentPhase,
            phases is null ? null : new OrderedDictionary<string, PhaseState>(phases), pendingTransition);
    }

    /// <summary>
    /// Creates a state in which the specified phase is current with the specified state, every earlier phase is
    /// approved, and every later phase is pending.
    /// </summary>
    public static FeaturePhaseState CreateState(WorkflowProfile profile, string currentPhase,
        PhaseState currentState = PhaseState.InProgress, string? pendingTransition = null,
        FeatureStatus status = FeatureStatus.Active)
    {
        var currentIndex = profile.IndexOf(currentPhase);

        var phases = profile.Phases.Select((x, index) => KeyValuePair.Create(x.Name, (index - currentIndex) switch
        {
            < 0 => PhaseState.Approved,
            0 => currentState,
            _ => PhaseState.Pending
        }));

        return new PhaseEngine().ReadState(profile,
            CreateStored(currentPhase, phases, pendingTransition, status));
    }

    public static FeaturePhaseState CreateAwaitingState(WorkflowProfile profile, string currentPhase,
        string pendingTransition)
    {
        return CreateState(profile, currentPhase, PhaseState.AwaitingApproval, pendingTransition);
    }

    public static FeaturePhaseState CreateDoneState(WorkflowProfile profile)
    {
        return CreateState(profile, profile.Phases[^1].Name, PhaseState.Approved, status: FeatureStatus.Done);
    }

    public static KeyValuePair<string, PhaseState>[] Phases(params (string Phase, PhaseState State)[] phases)
    {
        return [.. phases.Select(x => KeyValuePair.Create(x.Phase, x.State))];
    }

    public static PhaseSkills SkillsOf(WorkflowPhase phase)
    {
        return new PhaseSkills(phase.PhaseSkill, phase.GeneralSkills);
    }

    private static TheoryData<string, int> CreateBuiltInProfilePhases(Func<WorkflowProfile, int, bool> predicate)
    {
        var data = new TheoryData<string, int>();

        foreach (var profile in BuiltInWorkflowProfiles.All)
        {
            for (var index = 0; index < profile.Phases.Count; index++)
            {
                if (predicate(profile, index))
                {
                    data.Add(profile.Name, index);
                }
            }
        }

        return data;
    }
}
