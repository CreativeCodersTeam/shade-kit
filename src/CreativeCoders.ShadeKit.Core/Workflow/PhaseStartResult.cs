using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents the result of <see cref="IPhaseWorkflow.StartAsync" />.
/// </summary>
/// <param name="FeatureId">The id of the current feature, which is the name of its folder.</param>
/// <param name="Profile">The name of the workflow profile of the feature.</param>
/// <param name="Outcome">One of the enumeration values that specifies what the request did.</param>
/// <param name="Transition">
/// The requested, pending, or performed transition; for <see cref="PhaseStartOutcome.ResumePending" /> the pending
/// transition; for <see cref="PhaseStartOutcome.AlreadyInProgress" /> <see langword="null" />.
/// </param>
/// <param name="CurrentPhase">The current phase of the feature and its state after the request.</param>
/// <param name="Gate">
/// The gate of the target phase of <paramref name="Transition" />; for
/// <see cref="PhaseStartOutcome.AlreadyInProgress" /> and <see cref="PhaseStartOutcome.ResumePending" />
/// <see langword="null" />.
/// </param>
/// <param name="Skills">
/// The skills of the requested phase; for <see cref="PhaseStartOutcome.AlreadyInProgress" /> and
/// <see cref="PhaseStartOutcome.ResumePending" /> those of the current phase.
/// </param>
/// <param name="Warnings">The format warnings first, then the other warnings, each only once.</param>
public sealed record PhaseStartResult(
    string FeatureId,
    string Profile,
    PhaseStartOutcome Outcome,
    PhaseTransition? Transition,
    PhaseSnapshot CurrentPhase,
    PhaseGate? Gate,
    PhaseSkills Skills,
    IReadOnlyList<KitWarning> Warnings);
