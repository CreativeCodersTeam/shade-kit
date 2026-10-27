namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents a change of the phase state of a feature that the caller must persist: the new state and the
/// <c>history</c> entry to append.
/// </summary>
/// <remarks>
/// Every state change adds exactly one <c>history</c> entry, so both always come together. A call that changes
/// nothing returns no <see cref="PhaseStateChange" /> at all, which keeps the caller from writing.
/// </remarks>
/// <param name="State">The phase state after the change.</param>
/// <param name="History">The content of the <c>history</c> entry that records the change.</param>
public sealed record PhaseStateChange(FeaturePhaseState State, PhaseHistoryRecord History);
