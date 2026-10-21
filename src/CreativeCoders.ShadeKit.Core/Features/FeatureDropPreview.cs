namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents what <see cref="IFeatureStore.DropAsync" /> would delete, as returned by
/// <see cref="IFeatureStore.PreviewDropAsync" />.
/// </summary>
/// <param name="FeatureId">The id of the feature that would be dropped.</param>
/// <param name="Files">
/// Every file below the feature folder, relative to the repository root with <c>/</c> as separator, sorted ordinally.
/// </param>
/// <param name="OverrideAffected">
/// <see langword="true" /> if the local override selects the feature and would be cleared; otherwise,
/// <see langword="false" />.
/// </param>
public sealed record FeatureDropPreview(string FeatureId, IReadOnlyList<string> Files, bool OverrideAffected);
