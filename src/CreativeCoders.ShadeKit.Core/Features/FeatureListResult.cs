using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of <see cref="IFeatureStore.ListAsync" />.
/// </summary>
/// <param name="Features">The features of the store, sorted ordinally by id.</param>
/// <param name="Warnings">
/// The format warnings from reading the <c>feature.json</c> files first, then the warnings about feature folders that
/// were skipped or whose <c>feature.json</c> records another id.
/// </param>
public sealed record FeatureListResult(
    IReadOnlyList<FeatureSummary> Features,
    IReadOnlyList<KitWarning> Warnings);
