using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;

namespace CreativeCoders.ShadeKit.Cli.Commands.Gate.Info;

/// <summary>
/// Represents the <c>gate</c> object in the <c>data</c> of <c>shade-kit gate info &lt;phase&gt; --json</c>: the
/// configured gate of the forward transition into the phase and the feature's copy of its checklist.
/// </summary>
/// <param name="Phase">The name of the target phase of the gate, for example <c>plan</c>.</param>
/// <param name="Approval">
/// <see langword="true" /> if a human must approve the transition into <paramref name="Phase" />;
/// <see langword="false" /> if the transition is performed at once.
/// </param>
/// <param name="Checklist">
/// The path of the checklist of the gate relative to the feature folder, for example
/// <c>checks/definition-of-ready.md</c>, or <see langword="null" /> if the gate has none.
/// </param>
/// <param name="ChecklistPath">
/// The path of the feature's copy of the checklist relative to the repository root, with <c>/</c> as separator, for
/// example <c>.shadekit/features/gh-142-login-sso/checks/definition-of-ready.md</c>, or <see langword="null" /> if the
/// gate has no checklist.
/// </param>
/// <param name="ChecklistExists">
/// <see langword="true" /> if the feature's copy of the checklist exists as a file; <see langword="false" /> if it does
/// not; or <see langword="null" /> if the gate has no checklist.
/// </param>
public sealed record GateInfoCommandGateData(
    string Phase,
    bool Approval,
    string? Checklist,
    string? ChecklistPath,
    bool? ChecklistExists)
{
    /// <summary>
    /// Creates the data from the gate that the core read.
    /// </summary>
    /// <param name="gate">The configured gate into the phase with the location and the existence of its copy.</param>
    /// <returns>The data with the phase, the approval, and the checklist fields of <paramref name="gate" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="gate" /> is <see langword="null" />.</exception>
    public static GateInfoCommandGateData FromGate(GateInfoGate gate)
    {
        Ensure.NotNull(gate);

        return new GateInfoCommandGateData(gate.Gate.Phase, gate.Gate.Gate.Approval, gate.Gate.Gate.Checklist,
            gate.ChecklistPath, gate.ChecklistExists);
    }
}
