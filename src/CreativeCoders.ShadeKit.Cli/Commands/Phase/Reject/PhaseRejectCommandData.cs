using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Reject;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit phase reject &lt;phase&gt; --json</c> returns.
/// </summary>
/// <param name="FeatureId">The id of the current feature.</param>
/// <param name="Rejected">The rejected transition.</param>
/// <param name="CurrentPhase">The current phase, back in progress, and its state.</param>
/// <param name="Note">The recorded note, or <see langword="null" /> if the rejection has none.</param>
public sealed record PhaseRejectCommandData(
    string FeatureId,
    PhaseTransitionData Rejected,
    PhaseSnapshotData CurrentPhase,
    string? Note)
{
    /// <summary>
    /// Creates the data from the result of a <c>phase reject</c> request.
    /// </summary>
    /// <param name="result">The result of the request.</param>
    /// <returns>
    /// The data with the rejected transition, the current phase, and the note of <paramref name="result" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static PhaseRejectCommandData FromResult(PhaseRejectResult result)
    {
        Ensure.NotNull(result);

        return new PhaseRejectCommandData(result.FeatureId, PhaseTransitionData.FromTransition(result.Rejected),
            PhaseSnapshotData.FromSnapshot(result.CurrentPhase), result.Note);
    }
}
