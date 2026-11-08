using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Gate.Info;

/// <summary>
/// Represents one entry, <c>{ name, state }</c>, of the <c>phases</c> array in the <c>data</c> of
/// <c>shade-kit gate info &lt;phase&gt; --json</c>.
/// </summary>
/// <param name="Name">The name of the phase, for example <c>refine</c>.</param>
/// <param name="State">
/// The normalized state of the phase, written as <c>pending</c>, <c>in-progress</c>, <c>awaiting-approval</c>, or
/// <c>approved</c>.
/// </param>
public sealed record GateInfoCommandPhaseData(string Name, PhaseState State)
{
    /// <summary>
    /// Creates the data from a phase of the profile and its state.
    /// </summary>
    /// <param name="snapshot">The phase and its state.</param>
    /// <returns>The data with the name and the state of <paramref name="snapshot" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot" /> is <see langword="null" />.</exception>
    public static GateInfoCommandPhaseData FromSnapshot(PhaseSnapshot snapshot)
    {
        Ensure.NotNull(snapshot);

        return new GateInfoCommandPhaseData(snapshot.Name, snapshot.State);
    }
}
