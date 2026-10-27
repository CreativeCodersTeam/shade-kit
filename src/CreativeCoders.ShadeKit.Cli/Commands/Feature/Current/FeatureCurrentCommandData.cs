using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Current;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit feature current --json</c> returns.
/// </summary>
/// <param name="Resolved">
/// <see langword="true" /> if a current feature was resolved; otherwise, <see langword="false" />.
/// </param>
/// <param name="ResolvedFrom">
/// Where the current feature came from, written as <c>branch</c> or <c>override</c>, or <see langword="null" /> if no
/// current feature was resolved.
/// </param>
/// <param name="Feature">The current feature, or <see langword="null" /> if no current feature was resolved.</param>
public sealed record FeatureCurrentCommandData(
    bool Resolved,
    CurrentFeatureOrigin? ResolvedFrom,
    FeatureCurrentCommandFeatureData? Feature)
{
    /// <summary>
    /// Creates the data from the result of a current-feature resolution.
    /// </summary>
    /// <param name="result">The result of the resolution.</param>
    /// <returns>
    /// The data with the origin and the feature of <paramref name="result" />, or the "not resolved" data if
    /// <paramref name="result" /> has no feature.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static FeatureCurrentCommandData FromResult(CurrentFeatureResult result)
    {
        Ensure.NotNull(result);

        if (!result.IsResolved)
        {
            return new FeatureCurrentCommandData(false, null, null);
        }

        var feature = result.Feature;

        return new FeatureCurrentCommandData(true, result.Origin,
            new FeatureCurrentCommandFeatureData(result.FeatureId, feature.Title, feature.Profile, feature.Status,
                result.PhaseState.CurrentPhase, result.PhaseState.CurrentPhaseState, result.Paths));
    }
}
