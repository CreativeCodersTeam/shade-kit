namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles;

/// <summary>
/// Defines the names of the phases that the built-in workflow profiles consist of.
/// </summary>
/// <remarks>
/// The names are part of the agent contract (<c>feature.json</c>, <c>shade-kit phase</c> commands). Which phases a
/// profile has, and in which order, is defined in <see cref="BuiltInWorkflowProfiles" />.
/// </remarks>
public static class WorkflowPhaseNames
{
    /// <summary>
    /// Represents the name of the phase in which a feature is created.
    /// </summary>
    public const string New = "new";

    /// <summary>
    /// Represents the name of the phase in which the requirement of a feature is clarified.
    /// </summary>
    public const string Refine = "refine";

    /// <summary>
    /// Represents the name of the phase in which the code base is mapped for a feature.
    /// </summary>
    public const string Map = "map";

    /// <summary>
    /// Represents the name of the phase in which the cause of a bug is found.
    /// </summary>
    public const string Diagnose = "diagnose";

    /// <summary>
    /// Represents the name of the phase in which the implementation is planned.
    /// </summary>
    public const string Plan = "plan";

    /// <summary>
    /// Represents the name of the phase in which the change is implemented.
    /// </summary>
    public const string Implement = "implement";

    /// <summary>
    /// Represents the name of the phase in which the change is verified.
    /// </summary>
    public const string Verify = "verify";

    /// <summary>
    /// Represents the name of the phase in which a small change is clarified, specified, implemented, and reviewed in
    /// one run.
    /// </summary>
    public const string Quick = "quick";

    /// <summary>
    /// Represents the name of the phase in which a feature is finished.
    /// </summary>
    public const string Finish = "finish";
}
