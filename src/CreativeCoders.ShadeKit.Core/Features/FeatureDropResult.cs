namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of <see cref="IFeatureStore.DropAsync" />.
/// </summary>
/// <param name="FeatureId">The id of the feature that was dropped.</param>
/// <param name="Files">
/// Every file that was below the feature folder, relative to the repository root with <c>/</c> as separator, sorted
/// ordinally.
/// </param>
/// <param name="OverrideAffected">
/// <see langword="true" /> if the local override selected the feature, under the same rule as
/// <see cref="FeatureDropPreview.OverrideAffected" />, and was deleted; otherwise, <see langword="false" />.
/// </param>
public sealed record FeatureDropResult(string FeatureId, IReadOnlyList<string> Files, bool OverrideAffected);
