namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles;

/// <summary>
/// Defines the workflow profiles that are built into the engine.
/// </summary>
/// <remarks>
/// Adding a profile or changing the phases of a profile means changing this class only: the
/// <see cref="BuiltInWorkflowProfileCatalog" /> offers every profile in <see cref="All" />, and the phase engine takes
/// the phase order from the profile it is given. Every phase runs through the skill named after it (see
/// <see cref="WorkflowSkillNames.ForPhase" />), and the phase <see cref="WorkflowPhaseNames.New" /> cannot be reopened.
/// The artifacts of a phase are the files it writes into the feature folder (<see cref="WorkflowArtifactNames" />); a
/// gate check decides from them whether a checklist item applies to the profile.
/// </remarks>
public static class BuiltInWorkflowProfiles
{
    /// <summary>
    /// Gets the profile of a new feature.
    /// </summary>
    /// <value>
    /// The profile <see cref="WorkflowProfileNames.Feature" />: new, refine, map, plan, implement, verify, finish.
    /// </value>
    public static WorkflowProfile Feature { get; } = new WorkflowProfile(WorkflowProfileNames.Feature,
    [
        CreateNewPhase(),
        CreatePhase(WorkflowPhaseNames.Refine, [WorkflowSkillNames.Interview, WorkflowSkillNames.Research],
            Always(WorkflowArtifactNames.Spec), Always(WorkflowArtifactNames.Researches)),
        CreatePhase(WorkflowPhaseNames.Map, [WorkflowSkillNames.Research], Always(WorkflowArtifactNames.Researches)),
        CreatePlanPhase(),
        CreateImplementPhase(),
        CreateVerifyPhase(),
        CreateFinishPhase()
    ]);

    /// <summary>
    /// Gets the profile of a bug fix.
    /// </summary>
    /// <value>
    /// The profile <see cref="WorkflowProfileNames.Bugfix" />: new, diagnose, plan, implement, verify, finish.
    /// </value>
    public static WorkflowProfile Bugfix { get; } = new WorkflowProfile(WorkflowProfileNames.Bugfix,
    [
        CreateNewPhase(),
        CreatePhase(WorkflowPhaseNames.Diagnose, [WorkflowSkillNames.Interview, WorkflowSkillNames.Research],
            Always(WorkflowArtifactNames.Diagnosis), Always(WorkflowArtifactNames.Spec),
            Always(WorkflowArtifactNames.Researches)),
        CreatePlanPhase(),
        CreateImplementPhase(),
        CreateVerifyPhase(),
        CreateFinishPhase()
    ]);

    /// <summary>
    /// Gets the profile of a small change.
    /// </summary>
    /// <value>The profile <see cref="WorkflowProfileNames.Quick" />: new, quick, finish.</value>
    public static WorkflowProfile Quick { get; } = new WorkflowProfile(WorkflowProfileNames.Quick,
    [
        CreateNewPhase(),
        CreatePhase(WorkflowPhaseNames.Quick, [WorkflowSkillNames.Interview, WorkflowSkillNames.Review],
            Always(WorkflowArtifactNames.Spec), Always(WorkflowArtifactNames.Review),
            Always(WorkflowArtifactNames.Verification), OnDemand(WorkflowArtifactNames.Playbook)),
        CreateFinishPhase()
    ]);

    /// <summary>
    /// Gets all built-in workflow profiles.
    /// </summary>
    /// <value>The profiles <see cref="Feature" />, <see cref="Bugfix" />, and <see cref="Quick" />, in this order.</value>
    public static IReadOnlyList<WorkflowProfile> All { get; } = [Feature, Bugfix, Quick];

    private static WorkflowPhase CreateNewPhase()
    {
        return new WorkflowPhase(WorkflowPhaseNames.New, WorkflowSkillNames.ForPhase(WorkflowPhaseNames.New), [],
            canReopen: false, [Always(WorkflowArtifactNames.Request)]);
    }

    private static WorkflowPhase CreatePlanPhase()
    {
        return CreatePhase(WorkflowPhaseNames.Plan, [WorkflowSkillNames.Interview], Always(WorkflowArtifactNames.Plan),
            Always(WorkflowArtifactNames.Tasks));
    }

    private static WorkflowPhase CreateImplementPhase()
    {
        return CreatePhase(WorkflowPhaseNames.Implement, [WorkflowSkillNames.Review],
            Always(WorkflowArtifactNames.Review));
    }

    private static WorkflowPhase CreateVerifyPhase()
    {
        return CreatePhase(WorkflowPhaseNames.Verify, [], Always(WorkflowArtifactNames.Verification),
            Always(WorkflowArtifactNames.Playbook));
    }

    // The report is optional: config.json decides whether finish writes one.
    private static WorkflowPhase CreateFinishPhase()
    {
        return CreatePhase(WorkflowPhaseNames.Finish, [WorkflowSkillNames.Report],
            OnDemand(WorkflowArtifactNames.Report));
    }

    private static WorkflowPhase CreatePhase(string name, string[] generalSkills, params PhaseArtifact[] artifacts)
    {
        return new WorkflowPhase(name, WorkflowSkillNames.ForPhase(name), generalSkills, artifacts: artifacts);
    }

    private static PhaseArtifact Always(string file)
    {
        return new PhaseArtifact(file);
    }

    private static PhaseArtifact OnDemand(string file)
    {
        return new PhaseArtifact(file, onDemand: true);
    }
}
