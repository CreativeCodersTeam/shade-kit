using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Next;

/// <summary>
/// Represents a step without a gate, <c>{ phase, skill }</c>, in the <c>continue</c> object and the <c>reopen</c>
/// array of the <c>data</c> of <c>shade-kit phase next --json</c>.
/// </summary>
/// <param name="Phase">The phase of the step.</param>
/// <param name="Skill">The skill that runs the phase.</param>
public sealed record PhaseNextCommandStepData(string Phase, string Skill)
{
    /// <summary>
    /// Creates the data from a step of the phase engine.
    /// </summary>
    /// <param name="step">The step to describe.</param>
    /// <returns>The data with the phase and the skill of <paramref name="step" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="step" /> is <see langword="null" />.</exception>
    public static PhaseNextCommandStepData FromStep(PhaseStep step)
    {
        Ensure.NotNull(step);

        return new PhaseNextCommandStepData(step.Phase, step.Skill);
    }
}
