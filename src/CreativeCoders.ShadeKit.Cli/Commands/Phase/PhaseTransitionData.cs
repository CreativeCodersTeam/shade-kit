using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase;

/// <summary>
/// Represents a transition object, <c>{ kind, from, to }</c>, in the <c>data</c> of the <c>shade-kit phase</c>
/// commands.
/// </summary>
/// <param name="Kind">The direction of the transition, written as <c>forward</c> or <c>reopen</c>.</param>
/// <param name="From">The phase the transition starts in.</param>
/// <param name="To">The target phase of the transition.</param>
public sealed record PhaseTransitionData(PhaseTransitionKind Kind, string From, string To)
{
    /// <summary>
    /// Creates the data from a transition of the phase engine.
    /// </summary>
    /// <param name="transition">The transition to describe.</param>
    /// <returns>The data with the kind, the start phase, and the target phase of <paramref name="transition" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transition" /> is <see langword="null" />.</exception>
    public static PhaseTransitionData FromTransition(PhaseTransition transition)
    {
        Ensure.NotNull(transition);

        return new PhaseTransitionData(transition.Kind, transition.From, transition.To);
    }
}
