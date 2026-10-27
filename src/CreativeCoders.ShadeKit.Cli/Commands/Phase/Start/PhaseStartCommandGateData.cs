using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Start;

/// <summary>
/// Represents the <c>gate</c> object in the <c>data</c> of <c>shade-kit phase start &lt;phase&gt; --json</c>.
/// </summary>
/// <param name="Phase">The target phase whose entry gate this is.</param>
/// <param name="Approval">
/// <see langword="true" /> if the transition needs a human approval; otherwise, <see langword="false" />.
/// </param>
/// <param name="Checklist">The checklist of the gate, or <see langword="null" /> if the gate has none.</param>
public sealed record PhaseStartCommandGateData(string Phase, bool Approval, string? Checklist)
{
    /// <summary>
    /// Creates the data from the gate of a transition.
    /// </summary>
    /// <param name="gate">The target phase and its gate.</param>
    /// <returns>The data with the target phase, the approval, and the checklist of <paramref name="gate" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="gate" /> is <see langword="null" />.</exception>
    public static PhaseStartCommandGateData FromGate(PhaseGate gate)
    {
        Ensure.NotNull(gate);

        return new PhaseStartCommandGateData(gate.Phase, gate.Gate.Approval, gate.Gate.Checklist);
    }
}
