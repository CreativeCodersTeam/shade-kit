using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Next;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit phase next --json</c> returns.
/// </summary>
/// <remarks>
/// A part that does not apply is written as <see langword="null" />, like every absent value of the JSON output; for a
/// done feature only <c>current</c> is not <see langword="null" />.
/// </remarks>
/// <param name="Current">The current feature, its current phase with its state, and the feature status.</param>
/// <param name="Continue">
/// The current phase and its skill if the current phase is in progress; otherwise, <see langword="null" />.
/// </param>
/// <param name="Pending">
/// The target phase of the pending transition with its gate and skill if the current phase awaits an approval;
/// otherwise, <see langword="null" />.
/// </param>
/// <param name="Forward">
/// The next phase of the profile with its gate and skill if the current phase is in progress and not the last phase;
/// otherwise, <see langword="null" />.
/// </param>
/// <param name="Reopen">
/// The earlier phases that can be reopened, with their skills, in profile order, if the current phase is in
/// progress; possibly empty. Otherwise, <see langword="null" />.
/// </param>
public sealed record PhaseNextCommandData(
    PhaseNextCommandCurrentData Current,
    PhaseNextCommandStepData? Continue,
    PhaseNextCommandGatedStepData? Pending,
    PhaseNextCommandGatedStepData? Forward,
    IReadOnlyList<PhaseNextCommandStepData>? Reopen)
{
    /// <summary>
    /// Creates the data from the result of a <c>phase next</c> request.
    /// </summary>
    /// <param name="result">The result of the request.</param>
    /// <returns>The data with the current state and the possible next steps of <paramref name="result" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static PhaseNextCommandData FromResult(PhaseNextResult result)
    {
        Ensure.NotNull(result);

        return new PhaseNextCommandData(
            new PhaseNextCommandCurrentData(result.FeatureId, result.CurrentPhase.Name, result.CurrentPhase.State,
                result.Status),
            result.Continue is null ? null : PhaseNextCommandStepData.FromStep(result.Continue),
            result.Pending is null ? null : PhaseNextCommandGatedStepData.FromStep(result.Pending),
            result.Forward is null ? null : PhaseNextCommandGatedStepData.FromStep(result.Forward),
            result.Reopen is null ? null : [.. result.Reopen.Select(PhaseNextCommandStepData.FromStep)]);
    }
}
