using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles;

/// <summary>
/// Describes one phase of a <see cref="WorkflowProfile" />: its name, the skills it loads, and whether it can be
/// reopened.
/// </summary>
public sealed class WorkflowPhase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowPhase" /> class.
    /// </summary>
    /// <param name="name">The name of the phase, for example <see cref="WorkflowPhaseNames.Refine" />.</param>
    /// <param name="phaseSkill">
    /// The name of the skill the phase runs through, for example the result of
    /// <see cref="WorkflowSkillNames.ForPhase" />.
    /// </param>
    /// <param name="generalSkills">
    /// The names of the general skills the phase loads in addition to its phase skill, for example
    /// <see cref="WorkflowSkillNames.Interview" />; an empty list if it loads none.
    /// </param>
    /// <param name="canReopen">
    /// <see langword="true" /> if the phase can be reopened from a later phase; otherwise, <see langword="false" />.
    /// The default is <see langword="true" />.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="generalSkills" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name" /> or <paramref name="phaseSkill" /> is <see langword="null" />, empty, or whitespace, or
    /// <paramref name="generalSkills" /> contains a name that is <see langword="null" />, empty, or whitespace.
    /// </exception>
    public WorkflowPhase(string name, string phaseSkill, IEnumerable<string> generalSkills, bool canReopen = true)
    {
        Name = Ensure.IsNotNullOrWhitespace(name);
        PhaseSkill = Ensure.IsNotNullOrWhitespace(phaseSkill);
        GeneralSkills = [.. Ensure.NotNull(generalSkills)];

        if (GeneralSkills.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                $"The general skills of the phase '{name}' contain an empty skill name.", nameof(generalSkills));
        }

        CanReopen = canReopen;
    }

    /// <summary>
    /// Gets the name of the phase.
    /// </summary>
    /// <value>The name of the phase, for example <see cref="WorkflowPhaseNames.Refine" />.</value>
    public string Name { get; }

    /// <summary>
    /// Gets the name of the skill the phase runs through.
    /// </summary>
    /// <value>The name of the phase skill, for example <c>shade-kit-refine</c>.</value>
    public string PhaseSkill { get; }

    /// <summary>
    /// Gets the names of the general skills the phase loads in addition to its phase skill.
    /// </summary>
    /// <value>
    /// A copy of the general skills in the order they were passed, for example
    /// <see cref="WorkflowSkillNames.Interview" /> and <see cref="WorkflowSkillNames.Research" />; an empty list if the
    /// phase loads none.
    /// </value>
    public IReadOnlyList<string> GeneralSkills { get; }

    /// <summary>
    /// Gets a value that indicates whether the phase can be reopened from a later phase.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if the phase can be reopened; otherwise, <see langword="false" />.
    /// </value>
    public bool CanReopen { get; }
}
