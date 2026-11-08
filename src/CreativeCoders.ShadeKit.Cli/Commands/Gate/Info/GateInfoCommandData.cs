using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Gates;

namespace CreativeCoders.ShadeKit.Cli.Commands.Gate.Info;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit gate info &lt;phase&gt; --json</c> returns: everything a gate
/// check needs about the gate into one phase of one feature.
/// </summary>
/// <remarks>
/// An absent value is written as <see langword="null" />, like every absent value of the JSON output: <c>gate</c> for
/// the first phase of the profile, and <c>coverageThreshold</c> if <c>config.json</c> sets none.
/// </remarks>
/// <param name="FeatureId">The id of the feature, which is the name of its folder.</param>
/// <param name="Profile">The name of the workflow profile of the feature, for example <c>feature</c>.</param>
/// <param name="FeatureDir">
/// The folder of the feature relative to the repository root, with a trailing <c>/</c>, for example
/// <c>.shadekit/features/gh-142-login-sso/</c>.
/// </param>
/// <param name="Phases">The phases of the workflow profile in profile order, each with its normalized state.</param>
/// <param name="Gate">
/// The configured gate of the forward transition into the requested phase, or <see langword="null" /> if the phase is
/// the first phase of the profile.
/// </param>
/// <param name="CoverageThreshold">
/// The <c>coverageThreshold</c> of <c>config.json</c>, or <see langword="null" /> if it is not set.
/// </param>
public sealed record GateInfoCommandData(
    string FeatureId,
    string Profile,
    string FeatureDir,
    IReadOnlyList<GateInfoCommandPhaseData> Phases,
    GateInfoCommandGateData? Gate,
    double? CoverageThreshold)
{
    /// <summary>
    /// Creates the data from the result of a <c>gate info</c> request.
    /// </summary>
    /// <param name="result">The result of the request.</param>
    /// <returns>
    /// The data with the feature, the phases, the gate, and the coverage threshold of <paramref name="result" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static GateInfoCommandData FromResult(GateInfoResult result)
    {
        Ensure.NotNull(result);

        return new GateInfoCommandData(result.FeatureId, result.Profile, result.Paths.FeatureDir,
            [.. result.Phases.Select(GateInfoCommandPhaseData.FromSnapshot)],
            result.Gate is null ? null : GateInfoCommandGateData.FromGate(result.Gate),
            result.CoverageThreshold);
    }
}
