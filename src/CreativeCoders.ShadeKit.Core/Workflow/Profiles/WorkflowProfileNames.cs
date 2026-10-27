namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles;

/// <summary>
/// Defines the names of the built-in workflow profiles that a feature can follow.
/// </summary>
/// <remarks>
/// The names are part of the agent contract (<c>feature.json</c>, <c>defaultProfile</c>, <c>--profile</c>). The
/// phases of each profile are defined in <see cref="BuiltInWorkflowProfiles" />; the known profiles are listed by the
/// <see cref="IWorkflowProfileCatalog" />.
/// </remarks>
public static class WorkflowProfileNames
{
    /// <summary>
    /// Represents the name of the profile of a new feature.
    /// </summary>
    public const string Feature = "feature";

    /// <summary>
    /// Represents the name of the profile of a bug fix.
    /// </summary>
    public const string Bugfix = "bugfix";

    /// <summary>
    /// Represents the name of the profile of a small change.
    /// </summary>
    public const string Quick = "quick";
}
