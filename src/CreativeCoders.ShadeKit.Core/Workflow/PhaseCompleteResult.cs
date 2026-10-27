using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents the result of <see cref="IPhaseWorkflow.CompleteAsync" />.
/// </summary>
/// <param name="FeatureId">The id of the feature that was completed, which is the name of its folder.</param>
/// <param name="Phase">The name of the last phase of the profile, which was completed.</param>
/// <param name="Status">The status of the feature after the request, <see cref="FeatureStatus.Done" />.</param>
/// <param name="OverrideCleared">
/// <see langword="true" /> if the local override selected the feature and was deleted; otherwise,
/// <see langword="false" />.
/// </param>
/// <param name="Warnings">The format warnings first, then the other warnings, each only once.</param>
public sealed record PhaseCompleteResult(
    string FeatureId,
    string Phase,
    FeatureStatus Status,
    bool OverrideCleared,
    IReadOnlyList<KitWarning> Warnings);
