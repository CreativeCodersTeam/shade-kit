using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of <see cref="IFeatureStore.ShowAsync" />.
/// </summary>
/// <remarks>
/// The name of the feature folder is the id of the feature. <paramref name="Feature" /> is the document as it is
/// stored, so its <see cref="FeatureDocument.Id" /> can differ from <paramref name="FeatureId" />, for example after
/// the folder was copied or renamed; <paramref name="Warnings" /> then contains a
/// <see cref="FeatureWarningKind.FeatureIdMismatch" /> warning.
/// </remarks>
/// <param name="FeatureId">The id of the feature, which is the name of its folder.</param>
/// <param name="Feature">The <c>feature.json</c> document of the feature, as it is stored.</param>
/// <param name="Paths">The paths of the folder and the files of the feature.</param>
/// <param name="Warnings">The warnings about the feature.</param>
/// <param name="FormatWarnings">The format warnings from reading <c>feature.json</c>.</param>
public sealed record FeatureShowResult(
    string FeatureId,
    FeatureDocument Feature,
    FeaturePaths Paths,
    IReadOnlyList<FeatureWarning> Warnings,
    IReadOnlyList<FormatWarning> FormatWarnings);
