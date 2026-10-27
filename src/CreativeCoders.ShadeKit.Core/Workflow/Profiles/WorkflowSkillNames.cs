using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles;

/// <summary>
/// Defines the names of the skills that the phases of a workflow profile load.
/// </summary>
/// <remarks>
/// A phase runs through its phase skill (see <see cref="ForPhase" />) and can load general skills, such as
/// <see cref="Interview" />, in addition.
/// </remarks>
public static class WorkflowSkillNames
{
    private const string Prefix = "shade-kit-";

    /// <summary>
    /// Represents the name of the general skill that interviews the human.
    /// </summary>
    public const string Interview = Prefix + "interview";

    /// <summary>
    /// Represents the name of the general skill that researches the code base or other sources.
    /// </summary>
    public const string Research = Prefix + "research";

    /// <summary>
    /// Represents the name of the general skill that reviews a change.
    /// </summary>
    public const string Review = Prefix + "review";

    /// <summary>
    /// Represents the name of the general skill that writes the report of a feature.
    /// </summary>
    public const string Report = Prefix + "report";

    /// <summary>
    /// Represents the name of the skill that checks the checklist of a gate.
    /// </summary>
    /// <remarks>
    /// The skill is not part of the general skills of a phase; the entry protocol of a phase loads it for a gate that
    /// has a checklist.
    /// </remarks>
    public const string GateCheck = Prefix + "gate-check";

    /// <summary>
    /// Gets the name of the phase skill of the specified phase.
    /// </summary>
    /// <param name="phase">The name of the phase, for example <see cref="WorkflowPhaseNames.Refine" />.</param>
    /// <returns>The name <c>shade-kit-&lt;phase&gt;</c>, for example <c>shade-kit-refine</c>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="phase" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    public static string ForPhase(string phase)
    {
        Ensure.IsNotNullOrWhitespace(phase);

        return Prefix + phase;
    }
}
