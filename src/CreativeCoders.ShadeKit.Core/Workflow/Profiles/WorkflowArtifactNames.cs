using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles;

/// <summary>
/// Defines the paths, relative to the feature folder, of the artifacts that the phases of the built-in workflow
/// profiles write.
/// </summary>
/// <remarks>
/// The paths are part of the agent contract (<c>shade-kit gate info</c>, the phase skills). Which phase writes which
/// artifact is defined in <see cref="BuiltInWorkflowProfiles" />.
/// </remarks>
public static class WorkflowArtifactNames
{
    /// <summary>
    /// Represents the requirement as the feature was created from it.
    /// </summary>
    public const string Request = KitStorePaths.RequestFileName;

    /// <summary>
    /// Represents the specification with the requirements and their acceptance criteria.
    /// </summary>
    public const string Spec = "spec.md";

    /// <summary>
    /// Represents the research findings, in sections by phase.
    /// </summary>
    public const string Researches = "researches.md";

    /// <summary>
    /// Represents the diagnosis of a bug: reproduction, root cause, and ruled-out causes.
    /// </summary>
    public const string Diagnosis = "diagnosis.md";

    /// <summary>
    /// Represents the implementation plan.
    /// </summary>
    public const string Plan = "plan.md";

    /// <summary>
    /// Represents the tasks of the implementation with their coverage matrix.
    /// </summary>
    public const string Tasks = "tasks.md";

    /// <summary>
    /// Represents the review of the implementation with its commands and findings.
    /// </summary>
    public const string Review = "review.md";

    /// <summary>
    /// Represents the verification with the proving tests and the measured coverage.
    /// </summary>
    public const string Verification = "verification.md";

    /// <summary>
    /// Represents the playbook of the manual acceptance checks.
    /// </summary>
    public const string Playbook = "playbook.md";

    /// <summary>
    /// Represents the report of the finished feature.
    /// </summary>
    public const string Report = "report.md";
}
