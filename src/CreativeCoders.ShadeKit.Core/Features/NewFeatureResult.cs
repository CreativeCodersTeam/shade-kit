using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of <see cref="IFeatureStore.CreateAsync" />.
/// </summary>
/// <param name="Feature">
/// The <c>feature.json</c> document of the new feature, as it was written; its <see cref="FeatureDocument.Id" /> is the
/// name of the new feature folder.
/// </param>
/// <param name="Paths">The paths of the folder and the files of the new feature.</param>
/// <param name="SuggestedBranch">
/// The suggested name of the branch for the new feature, <c>feature/&lt;id&gt;</c>. SHADE kit does not create it.
/// </param>
/// <param name="FormatWarnings">The format warnings from reading <c>config.json</c>.</param>
public sealed record NewFeatureResult(
    FeatureDocument Feature,
    FeaturePaths Paths,
    string SuggestedBranch,
    IReadOnlyList<FormatWarning> FormatWarnings);
