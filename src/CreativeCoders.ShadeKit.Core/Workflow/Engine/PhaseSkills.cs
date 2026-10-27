using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the skills an agent loads for a phase.
/// </summary>
/// <param name="PhaseSkill">The name of the skill the phase runs through, <see cref="WorkflowPhase.PhaseSkill" />.</param>
/// <param name="GeneralSkills">
/// The names of the general skills the phase loads in addition, <see cref="WorkflowPhase.GeneralSkills" />; an empty
/// list if it loads none.
/// </param>
public sealed record PhaseSkills(string PhaseSkill, IReadOnlyList<string> GeneralSkills);
