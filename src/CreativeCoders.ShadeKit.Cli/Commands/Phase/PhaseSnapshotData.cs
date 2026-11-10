using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase;

/// <summary>
/// Represents the <c>currentPhase</c> object, <c>{ name, state }</c>, in the <c>data</c> of the
/// <c>shade-kit phase</c> commands that change the phase state.
/// </summary>
/// <param name="Name">The name of the current phase.</param>
/// <param name="State">
/// The state of the current phase after the command, written as <c>pending</c>, <c>in-progress</c>,
/// <c>awaiting-approval</c>, or <c>approved</c>.
/// </param>
public sealed record PhaseSnapshotData(string Name, PhaseState State)
{
    /// <summary>
    /// Creates the data from a snapshot of the current phase.
    /// </summary>
    /// <param name="snapshot">The current phase and its state.</param>
    /// <returns>The data with the name and the state of <paramref name="snapshot" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot" /> is <see langword="null" />.</exception>
    public static PhaseSnapshotData FromSnapshot(PhaseSnapshot snapshot)
    {
        Ensure.NotNull(snapshot);

        return new PhaseSnapshotData(snapshot.Name, snapshot.State);
    }
}
