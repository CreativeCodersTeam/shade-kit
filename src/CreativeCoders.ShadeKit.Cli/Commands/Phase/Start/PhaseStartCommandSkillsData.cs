using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Start;

/// <summary>
/// Represents the <c>skills</c> object in the <c>data</c> of <c>shade-kit phase start &lt;phase&gt; --json</c>.
/// </summary>
/// <param name="PhaseSkill">The skill that runs the phase.</param>
/// <param name="GeneralSkills">The general skills the phase skill needs, in the order in which they are loaded.</param>
public sealed record PhaseStartCommandSkillsData(string PhaseSkill, IReadOnlyList<string> GeneralSkills)
{
    /// <summary>
    /// Creates the data from the skills of a phase.
    /// </summary>
    /// <param name="skills">The skills of the phase.</param>
    /// <returns>The data with the phase skill and the general skills of <paramref name="skills" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="skills" /> is <see langword="null" />.</exception>
    public static PhaseStartCommandSkillsData FromSkills(PhaseSkills skills)
    {
        Ensure.NotNull(skills);

        return new PhaseStartCommandSkillsData(skills.PhaseSkill, [.. skills.GeneralSkills]);
    }
}
