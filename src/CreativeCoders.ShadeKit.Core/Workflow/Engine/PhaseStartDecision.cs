using System.Diagnostics.CodeAnalysis;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the decision of <see cref="IPhaseEngine.Start" />.
/// </summary>
/// <param name="Outcome">One of the enumeration values that specifies what the request did.</param>
/// <param name="Transition">
/// The requested, pending, or performed transition; for <see cref="PhaseStartOutcome.ResumePending" /> the pending
/// transition; for <see cref="PhaseStartOutcome.AlreadyInProgress" /> <see langword="null" />.
/// </param>
/// <param name="Gate">
/// The gate of the target phase <see cref="PhaseTransition.To" /> of <paramref name="Transition" />; for
/// <see cref="PhaseStartOutcome.AlreadyInProgress" /> and <see cref="PhaseStartOutcome.ResumePending" />
/// <see langword="null" />. A reopen always has <see cref="TransitionGate.Default" />.
/// </param>
/// <param name="Skills">
/// The skills of the requested phase; for <see cref="PhaseStartOutcome.AlreadyInProgress" /> and
/// <see cref="PhaseStartOutcome.ResumePending" /> those of the current phase, which is the requested phase.
/// </param>
/// <param name="State">The phase state after the request, which is the given state if nothing changed.</param>
/// <param name="Change">
/// The change to persist, or <see langword="null" /> if the request changed nothing and nothing must be written.
/// </param>
public sealed record PhaseStartDecision(
    PhaseStartOutcome Outcome,
    PhaseTransition? Transition,
    TransitionGate? Gate,
    PhaseSkills Skills,
    FeaturePhaseState State,
    PhaseStateChange? Change)
{
    /// <summary>
    /// Gets a value that indicates whether the request changed the phase state.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if <see cref="Change" /> must be persisted; otherwise, <see langword="false" />.
    /// </value>
    [MemberNotNullWhen(true, nameof(Change))]
    public bool HasChanges => Change is not null;
}
