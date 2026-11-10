using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents the result of <see cref="IPhaseWorkflow.GetGateInfoAsync" />: everything a gate check needs about the
/// gate into one phase of one feature.
/// </summary>
/// <param name="FeatureId">The id of the feature, which is the name of its folder.</param>
/// <param name="Profile">The name of the workflow profile of the feature, for example <c>feature</c>.</param>
/// <param name="Phases">
/// The phases of the feature with their normalized state and their artifacts, as <see cref="FeaturePhase.ListOf" />
/// creates them: every phase of the profile in profile order, then the stored phases that the profile does not have.
/// </param>
/// <param name="Gate">
/// The configured gate of the forward transition into the requested phase; or <see langword="null" /> if the phase is
/// the first phase of the profile, into which no transition leads.
/// </param>
/// <param name="CoverageThreshold">
/// The <see cref="KitConfig.CoverageThreshold" /> of <c>config.json</c>; or <see langword="null" /> if
/// <c>config.json</c> does not exist or has no <c>coverageThreshold</c> field.
/// </param>
/// <param name="Warnings">
/// The warnings of reading the feature, <c>gates.json</c>, and <c>config.json</c>, with the
/// <see cref="FormatWarning" /> instances first, then the other warnings in that order, and every warning only once.
/// </param>
public sealed record GateInfoResult(
    string FeatureId,
    string Profile,
    IReadOnlyList<FeaturePhase> Phases,
    GateInfoGate? Gate,
    double? CoverageThreshold,
    IReadOnlyList<KitWarning> Warnings)
{
    /// <summary>
    /// Gets the paths of the folder and the files of the feature.
    /// </summary>
    /// <value>
    /// The paths for <see cref="FeatureId" /> relative to the repository root; its <see cref="FeaturePaths.FeatureDir" />
    /// has a trailing <c>/</c>, for example <c>.shadekit/features/gh-142-login-sso/</c>.
    /// </value>
    /// <exception cref="InvalidFeatureIdException"><see cref="FeatureId" /> is not a valid feature id.</exception>
    public FeaturePaths Paths => FeaturePaths.For(FeatureId);
}
