using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of <see cref="IFeatureStore.ListAsync" />.
/// </summary>
/// <param name="Features">The features of the store, sorted ordinally by id.</param>
/// <param name="Warnings">The warnings about feature folders that were skipped.</param>
/// <param name="FormatWarnings">The format warnings from reading the <c>feature.json</c> files.</param>
public sealed record FeatureListResult(
    IReadOnlyList<FeatureSummary> Features,
    IReadOnlyList<FeatureWarning> Warnings,
    IReadOnlyList<FormatWarning> FormatWarnings);
