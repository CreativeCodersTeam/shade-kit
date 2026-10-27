using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Start;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit phase start &lt;phase&gt; --json</c> returns.
/// </summary>
/// <param name="FeatureId">The id of the current feature.</param>
/// <param name="Profile">The workflow profile of the current feature.</param>
/// <param name="Outcome">
/// What the request did, written as <c>transition-requested</c>, <c>transition-pending</c>,
/// <c>transition-performed</c>, <c>already-in-progress</c>, or <c>resume-pending</c>.
/// </param>
/// <param name="Transition">
/// The requested, pending, or performed transition; for <c>resume-pending</c> the pending transition; for
/// <c>already-in-progress</c> <see langword="null" />.
/// </param>
/// <param name="CurrentPhase">The current phase and its state after the request.</param>
/// <param name="Gate">
/// The entry gate of the target phase; for <c>already-in-progress</c> and <c>resume-pending</c>
/// <see langword="null" />.
/// </param>
/// <param name="Skills">
/// The skills of the requested phase; for <c>already-in-progress</c> and <c>resume-pending</c> those of the current
/// phase.
/// </param>
public sealed record PhaseStartCommandData(
    string FeatureId,
    string Profile,
    PhaseStartOutcome Outcome,
    PhaseTransitionData? Transition,
    PhaseSnapshotData CurrentPhase,
    PhaseStartCommandGateData? Gate,
    PhaseStartCommandSkillsData Skills)
{
    /// <summary>
    /// Creates the data from the result of a <c>phase start</c> request.
    /// </summary>
    /// <param name="result">The result of the request.</param>
    /// <returns>The data with the outcome, the transition, the state, the gate, and the skills of
    /// <paramref name="result" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static PhaseStartCommandData FromResult(PhaseStartResult result)
    {
        Ensure.NotNull(result);

        return new PhaseStartCommandData(result.FeatureId, result.Profile, result.Outcome,
            result.Transition is null ? null : PhaseTransitionData.FromTransition(result.Transition),
            PhaseSnapshotData.FromSnapshot(result.CurrentPhase),
            result.Gate is null ? null : PhaseStartCommandGateData.FromGate(result.Gate),
            PhaseStartCommandSkillsData.FromSkills(result.Skills));
    }
}
