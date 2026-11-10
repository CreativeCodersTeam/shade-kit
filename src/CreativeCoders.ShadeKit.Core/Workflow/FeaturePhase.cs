using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents one phase of a feature: its name, its state, and the artifacts its workflow profile lets it write.
/// </summary>
/// <param name="Name">The name of the phase, for example <c>refine</c>.</param>
/// <param name="State">The normalized state of the phase in the feature.</param>
/// <param name="Artifacts">
/// The artifacts that the phase of the profile writes into the feature folder (<see cref="WorkflowPhase.Artifacts" />);
/// an empty list for a stored phase that the profile does not have.
/// </param>
public sealed record FeaturePhase(string Name, PhaseState State, IReadOnlyList<PhaseArtifact> Artifacts)
{
    /// <summary>
    /// Creates the phases of a feature from its phase state.
    /// </summary>
    /// <param name="state">The normalized phase state of the feature.</param>
    /// <returns>
    /// The phases of <see cref="FeaturePhaseState.Phases" /> in their order: every phase of the profile in profile
    /// order, then the stored phases that the profile does not have.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="state" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<FeaturePhase> ListOf(FeaturePhaseState state)
    {
        Ensure.NotNull(state);

        return
        [
            .. state.Phases.Select(x => new FeaturePhase(x.Key, x.Value,
                state.Profile.TryGetPhase(x.Key, out var phase) ? phase.Artifacts : []))
        ];
    }
}
