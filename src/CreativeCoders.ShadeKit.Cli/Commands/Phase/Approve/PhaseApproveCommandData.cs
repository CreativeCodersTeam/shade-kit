using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Approve;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit phase approve &lt;phase&gt; --json</c> returns.
/// </summary>
/// <param name="FeatureId">The id of the current feature.</param>
/// <param name="Transition">The approved transition.</param>
/// <param name="CurrentPhase">The current phase, the target phase of the transition, and its state.</param>
public sealed record PhaseApproveCommandData(
    string FeatureId,
    PhaseTransitionData Transition,
    PhaseSnapshotData CurrentPhase)
{
    /// <summary>
    /// Creates the data from the result of a <c>phase approve</c> request.
    /// </summary>
    /// <param name="result">The result of the request.</param>
    /// <returns>The data with the approved transition and the current phase of <paramref name="result" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static PhaseApproveCommandData FromResult(PhaseApproveResult result)
    {
        Ensure.NotNull(result);

        return new PhaseApproveCommandData(result.FeatureId, PhaseTransitionData.FromTransition(result.Transition),
            PhaseSnapshotData.FromSnapshot(result.CurrentPhase));
    }
}
