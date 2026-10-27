namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the decision of <see cref="IPhaseEngine.Reject" />.
/// </summary>
/// <param name="Rejected">The transition that was rejected.</param>
/// <param name="Note">The trimmed note of the rejection, or <see langword="null" /> if none was given.</param>
/// <param name="Change">The change to persist; its state has the current phase in progress again.</param>
public sealed record PhaseRejectDecision(PhaseTransition Rejected, string? Note, PhaseStateChange Change);
