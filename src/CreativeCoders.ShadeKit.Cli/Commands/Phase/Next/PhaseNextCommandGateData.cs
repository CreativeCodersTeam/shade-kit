namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Next;

/// <summary>
/// Represents the <c>gate</c> object of a gated step in the <c>data</c> of <c>shade-kit phase next --json</c>.
/// </summary>
/// <param name="Approval">
/// <see langword="true" /> if the transition needs a human approval; otherwise, <see langword="false" />.
/// </param>
/// <param name="Checklist">The checklist of the gate, or <see langword="null" /> if the gate has none.</param>
public sealed record PhaseNextCommandGateData(bool Approval, string? Checklist);
