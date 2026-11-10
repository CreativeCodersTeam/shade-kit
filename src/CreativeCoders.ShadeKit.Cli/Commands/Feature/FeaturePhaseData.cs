using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature;

/// <summary>
/// Represents one entry of the <c>phases</c> array in the JSON <c>data</c> of <c>shade-kit feature show</c> and
/// <c>shade-kit gate info</c>: a phase of the feature, <c>{ name, state, artifacts }</c>.
/// </summary>
/// <param name="Name">The name of the phase, for example <c>refine</c>.</param>
/// <param name="State">
/// The normalized state of the phase, written as <c>pending</c>, <c>in-progress</c>, <c>awaiting-approval</c>, or
/// <c>approved</c>; <c>pending</c> for a phase of the profile without a stored state.
/// </param>
/// <param name="Artifacts">
/// The artifacts the phase writes into the feature folder; an empty array for a stored phase that the profile does not
/// have.
/// </param>
public sealed record FeaturePhaseData(string Name, PhaseState State, IReadOnlyList<PhaseArtifactData> Artifacts)
{
    /// <summary>
    /// Creates the data of the specified phases of a feature.
    /// </summary>
    /// <param name="phases">The phases of the feature.</param>
    /// <returns>The data of <paramref name="phases" />, in their order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="phases" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<FeaturePhaseData> FromPhases(IEnumerable<FeaturePhase> phases)
    {
        Ensure.NotNull(phases);

        return
        [
            .. phases.Select(x =>
                new FeaturePhaseData(x.Name, x.State, [.. x.Artifacts.Select(PhaseArtifactData.FromArtifact)]))
        ];
    }
}
