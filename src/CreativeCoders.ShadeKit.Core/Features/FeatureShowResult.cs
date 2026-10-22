namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of <see cref="IFeatureStore.ShowAsync" />.
/// </summary>
/// <remarks>
/// The name of the feature folder is the id of the feature. <paramref name="Feature" /> is the document as it is
/// stored, so its <see cref="FeatureDocument.Id" /> can differ from <paramref name="FeatureId" />, for example after
/// the folder was copied or renamed; <paramref name="Warnings" /> then contains a
/// <see cref="FeatureIdMismatchWarning" /> warning.
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
