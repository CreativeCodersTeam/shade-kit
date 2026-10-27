using System.Diagnostics.CodeAnalysis;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of <see cref="IFeatureStore.GetCurrentAsync" />.
/// </summary>
/// <remarks>
/// If no current feature is resolved, <paramref name="FeatureId" />, <paramref name="Feature" />,
/// <paramref name="Origin" />, <paramref name="PhaseState" />, and <see cref="Paths" /> are <see langword="null" />. A
/// <see cref="FeatureDocument.Id" /> that differs from the folder name gives a <see cref="FeatureIdMismatchWarning" />.
/// </remarks>
/// <param name="FeatureId">
/// The id of the current feature, which is the name of its folder; or <see langword="null" /> if no current feature is
/// resolved.
/// </param>
/// <param name="Feature">
/// The <c>feature.json</c> document of the current feature, as it is stored; or <see langword="null" /> if no current feature is
/// resolved.
/// </param>
/// <param name="Origin">
/// One of the enumeration values that specifies where the current feature comes from; or <see langword="null" /> if no
/// current feature is resolved.
/// </param>
/// <param name="PhaseState">
/// The phase state of the current feature, normalized against its workflow profile as the phase commands see it: the
/// initial state of the profile for a <paramref name="Feature" /> without phase fields; or <see langword="null" /> if
/// no current feature is resolved. <paramref name="Feature" /> is not changed.
/// </param>
/// <param name="Warnings">
/// The format warnings from reading the local override and the <c>feature.json</c> of the current feature first, then
/// the warnings about the current branch, the local override, and the current feature.
/// </param>
public sealed record CurrentFeatureResult(
    string? FeatureId,
    FeatureDocument? Feature,
    CurrentFeatureOrigin? Origin,
    FeaturePhaseState? PhaseState,
    IReadOnlyList<KitWarning> Warnings)
{
    /// <summary>
    /// Gets the paths of the folder and the files of the current feature.
    /// </summary>
    /// <value>
    /// The paths for <see cref="FeatureId" />; or <see langword="null" /> if no current feature is resolved.
    /// </value>
    /// <exception cref="InvalidFeatureIdException"><see cref="FeatureId" /> is not a valid feature id.</exception>
    public FeaturePaths? Paths => FeatureId is null ? null : FeaturePaths.For(FeatureId);

    /// <summary>
    /// Gets a value that indicates whether a current feature is resolved.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if <see cref="FeatureId" /> is not <see langword="null" />; otherwise,
    /// <see langword="false" />.
    /// </value>
    [MemberNotNullWhen(true, nameof(FeatureId), nameof(Feature), nameof(Origin), nameof(PhaseState), nameof(Paths))]
    public bool IsResolved => FeatureId is not null;
}
