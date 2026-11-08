using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Represents the result of <see cref="IGateInfoReader.ReadAsync" />: everything a gate check needs about the gate
/// into one phase of one feature.
/// </summary>
/// <param name="FeatureId">The id of the feature, which is the name of its folder.</param>
/// <param name="Profile">The name of the workflow profile of the feature, for example <c>feature</c>.</param>
/// <param name="Paths">
/// The paths of the folder and the files of the feature relative to the repository root; its
/// <see cref="FeaturePaths.FeatureDir" /> has a trailing <c>/</c>, for example
/// <c>.shadekit/features/gh-142-login-sso/</c>.
/// </param>
/// <param name="Phases">
/// The phases of the workflow profile in profile order, each with its normalized state; a phase without a stored state
/// is <see cref="Workflow.Engine.PhaseState.Pending" />. Stored entries of phases that the profile does not have are
/// not included.
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
    FeaturePaths Paths,
    IReadOnlyList<PhaseSnapshot> Phases,
    GateInfoGate? Gate,
    double? CoverageThreshold,
    IReadOnlyList<KitWarning> Warnings);
