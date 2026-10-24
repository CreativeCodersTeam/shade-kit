using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of <see cref="IFeatureStore.CreateAsync" />.
/// </summary>
/// <param name="Feature">
/// The <c>feature.json</c> document of the new feature, as it was written; its <see cref="FeatureDocument.Id" /> is the
/// name of the new feature folder.
/// </param>
/// <param name="Warnings">
/// The format warnings from reading <c>config.json</c>, followed by the warnings of the files that the create operation
/// wrote, for example an <see cref="EditorConfigUnreadableWarning" />; each warning only once.
/// </param>
public sealed record NewFeatureResult(
    FeatureDocument Feature,
    IReadOnlyList<KitWarning> Warnings)
{
    private const string SuggestedBranchPrefix = "feature/";

    /// <summary>
    /// Gets the paths of the folder and the files of the new feature.
    /// </summary>
    /// <value>The paths for the <see cref="FeatureDocument.Id" /> of <see cref="Feature" />.</value>
    /// <exception cref="InvalidFeatureIdException">
    /// The <see cref="FeatureDocument.Id" /> of <see cref="Feature" /> is not a valid feature id.
    /// </exception>
    public FeaturePaths Paths => FeaturePaths.For(Feature.Id);

    /// <summary>
    /// Gets the suggested name of the branch for the new feature. SHADE kit does not create it.
    /// </summary>
    /// <value><c>feature/&lt;id&gt;</c>, with the <see cref="FeatureDocument.Id" /> of <see cref="Feature" />.</value>
    public string SuggestedBranch => SuggestedBranchPrefix + Feature.Id;
}
