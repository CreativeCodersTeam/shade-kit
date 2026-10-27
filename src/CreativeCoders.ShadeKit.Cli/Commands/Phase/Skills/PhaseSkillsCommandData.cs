using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Skills;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit phase skills &lt;phase&gt; --json</c> returns.
/// </summary>
/// <param name="FeatureId">The id of the current feature.</param>
/// <param name="Profile">The workflow profile of the current feature.</param>
/// <param name="Phase">The phase whose skills are returned.</param>
/// <param name="PhaseSkill">The skill that runs the phase.</param>
/// <param name="GeneralSkills">The general skills the phase skill needs, in the order in which they are loaded.</param>
public sealed record PhaseSkillsCommandData(
    string FeatureId,
    string Profile,
    string Phase,
    string PhaseSkill,
    IReadOnlyList<string> GeneralSkills)
{
    /// <summary>
    /// Creates the data from the result of a <c>phase skills</c> request.
    /// </summary>
    /// <param name="result">The result of the request.</param>
    /// <returns>The data with the profile, the phase, and the skills of <paramref name="result" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static PhaseSkillsCommandData FromResult(PhaseSkillsResult result)
    {
        Ensure.NotNull(result);

        return new PhaseSkillsCommandData(result.FeatureId, result.Profile, result.Phase, result.Skills.PhaseSkill,
            [.. result.Skills.GeneralSkills]);
    }
}
