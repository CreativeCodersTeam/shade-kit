using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase;

/// <summary>
/// Represents a phase and its state, <c>{ name, state }</c>, in the JSON <c>data</c> of a shade-kit command.
/// </summary>
/// <remarks>
/// The <c>shade-kit phase</c> commands that change the phase state write it as the <c>currentPhase</c> object, with
/// the state of the current phase after the command; <c>shade-kit gate info</c> writes one as each entry of the
/// <c>phases</c> array, with the normalized state of the phase in the feature (<c>pending</c> for a phase without a
/// stored state).
/// </remarks>
/// <param name="Name">The name of the phase, for example <c>refine</c>.</param>
/// <param name="State">
/// The state of the phase, written as <c>pending</c>, <c>in-progress</c>, <c>awaiting-approval</c>, or
/// <c>approved</c>.
/// </param>
public sealed record PhaseSnapshotData(string Name, PhaseState State)
{
    /// <summary>
    /// Creates the data from a snapshot of a phase.
    /// </summary>
    /// <param name="snapshot">The phase and its state.</param>
    /// <returns>The data with the name and the state of <paramref name="snapshot" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot" /> is <see langword="null" />.</exception>
    public static PhaseSnapshotData FromSnapshot(PhaseSnapshot snapshot)
    {
        Ensure.NotNull(snapshot);

        return new PhaseSnapshotData(snapshot.Name, snapshot.State);
    }
}
