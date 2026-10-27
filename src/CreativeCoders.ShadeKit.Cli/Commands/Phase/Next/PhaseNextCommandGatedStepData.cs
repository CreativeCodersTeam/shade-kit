using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Next;

/// <summary>
/// Represents a step through a gate, <c>{ phase, gate, skill }</c>, in the <c>pending</c> and <c>forward</c> objects
/// of the <c>data</c> of <c>shade-kit phase next --json</c>.
/// </summary>
/// <param name="Phase">The target phase of the step.</param>
/// <param name="Gate">The entry gate of the target phase.</param>
/// <param name="Skill">The skill that runs the target phase.</param>
public sealed record PhaseNextCommandGatedStepData(string Phase, PhaseNextCommandGateData Gate, string Skill)
{
    /// <summary>
    /// Creates the data from a gated step of the phase engine.
    /// </summary>
    /// <param name="step">The step to describe.</param>
    /// <returns>The data with the target phase, the gate, and the skill of <paramref name="step" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="step" /> is <see langword="null" />.</exception>
    public static PhaseNextCommandGatedStepData FromStep(GatedPhaseStep step)
    {
        Ensure.NotNull(step);

        return new PhaseNextCommandGatedStepData(step.Phase,
            new PhaseNextCommandGateData(step.Gate.Approval, step.Gate.Checklist), step.Skill);
    }
}
