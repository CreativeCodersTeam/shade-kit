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
/// <param name="Checklist">
/// The path of the checklist of the gate relative to the feature folder, for example
/// <c>checks/definition-of-ready.md</c>, or <see langword="null" /> if the gate has none.
/// </param>
/// <param name="ChecklistCreated">
/// <see langword="true" /> if the request created the feature's copy of the checklist; <see langword="false" /> if the
/// copy already existed; <see langword="null" /> if the gate has no checklist.
/// </param>
public sealed record PhaseStartCommandGateData(string Phase, bool Approval, string? Checklist, bool? ChecklistCreated)
{
    /// <summary>
    /// Creates the data from the gate of a transition and the outcome of ensuring the copy of its checklist.
    /// </summary>
    /// <param name="gate">The target phase and its gate.</param>
    /// <param name="checklistCreated">
    /// <see langword="true" /> if the request created the copy of the checklist; <see langword="false" /> if the copy
    /// already existed; <see langword="null" /> if no copy was ensured.
    /// </param>
    /// <returns>
    /// The data with the target phase, the approval, and the checklist of <paramref name="gate" />, and
    /// <paramref name="checklistCreated" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="gate" /> is <see langword="null" />.</exception>
    public static PhaseStartCommandGateData FromGate(PhaseGate gate, bool? checklistCreated)
    {
        Ensure.NotNull(gate);

        return new PhaseStartCommandGateData(gate.Phase, gate.Gate.Approval, gate.Gate.Checklist, checklistCreated);
    }
}
