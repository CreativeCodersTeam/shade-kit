using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of <see cref="IFeatureStore.ShowAsync" />.
/// </summary>
/// <remarks>
/// The <see cref="FeatureDocument.Id" /> of <paramref name="Feature" /> can differ from the folder name, for
/// example after a rename; <paramref name="Warnings" /> then contains a <see cref="FeatureIdMismatchWarning" />.
/// </remarks>
/// <param name="FeatureId">The id of the feature, which is the name of its folder.</param>
/// <param name="Feature">The <c>feature.json</c> document of the feature, as it is stored.</param>
/// <param name="Warnings">
/// The format warnings from reading <c>feature.json</c> first, then the warnings about the feature.
/// </param>
public sealed record FeatureShowResult(
    string FeatureId,
    FeatureDocument Feature,
    IReadOnlyList<KitWarning> Warnings)
{
    /// <summary>
    /// Gets the paths of the folder and the files of the feature.
    /// </summary>
    /// <value>The paths for <see cref="FeatureId" />.</value>
    /// <exception cref="InvalidFeatureIdException"><see cref="FeatureId" /> is not a valid feature id.</exception>
    public FeaturePaths Paths => FeaturePaths.For(FeatureId);
}
