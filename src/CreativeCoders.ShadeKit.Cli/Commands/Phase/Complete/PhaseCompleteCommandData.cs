using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Complete;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit phase complete --json</c> returns.
/// </summary>
/// <param name="FeatureId">The id of the completed feature.</param>
/// <param name="Phase">The last phase of the workflow profile, which is now approved.</param>
/// <param name="Status">The status of the feature, written as <c>done</c>.</param>
/// <param name="OverrideCleared">
/// <see langword="true" /> if the local override selected the feature and was cleared; otherwise,
/// <see langword="false" />.
/// </param>
public sealed record PhaseCompleteCommandData(
    string FeatureId,
    string Phase,
    FeatureStatus Status,
    bool OverrideCleared)
{
    /// <summary>
    /// Creates the data from the result of a <c>phase complete</c> request.
    /// </summary>
    /// <param name="result">The result of the request.</param>
    /// <returns>
    /// The data with the completed phase, the status, and the override flag of <paramref name="result" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static PhaseCompleteCommandData FromResult(PhaseCompleteResult result)
    {
        Ensure.NotNull(result);

        return new PhaseCompleteCommandData(result.FeatureId, result.Phase, result.Status, result.OverrideCleared);
    }
}
