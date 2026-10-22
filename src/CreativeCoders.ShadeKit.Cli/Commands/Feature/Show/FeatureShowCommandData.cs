using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit feature show &lt;id&gt; --json</c> returns.
/// </summary>
/// <param name="Id">The id of the feature, which is the name of its folder.</param>
/// <param name="Title">The title of the feature.</param>
/// <param name="Profile">The workflow profile of the feature.</param>
/// <param name="Status">The status of the feature, written as <c>active</c> or <c>done</c>.</param>
/// <param name="Source">The source of the request of the feature.</param>
/// <param name="CreatedAt">The point in time, with its UTC offset, at which the feature was created.</param>
/// <param name="CreatedBy">The git user who created the feature.</param>
/// <param name="Paths">The repository-relative paths of the feature folder and its files.</param>
public sealed record FeatureShowCommandData(
    string Id,
    string Title,
    string Profile,
    FeatureStatus Status,
    FeatureSourceData Source,
    DateTimeOffset CreatedAt,
    FeatureCreatorData CreatedBy,
    FeaturePaths Paths)
{
    /// <summary>
    /// Creates the data from the result of reading a feature.
    /// </summary>
    /// <param name="result">The result of reading the feature.</param>
    /// <returns>The data with the fields of the feature and the paths of <paramref name="result" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static FeatureShowCommandData FromResult(FeatureShowResult result)
    {
        Ensure.NotNull(result);

        var feature = result.Feature;

        return new FeatureShowCommandData(result.FeatureId, feature.Title, feature.Profile, feature.Status,
            FeatureSourceData.FromSource(feature.Source), feature.CreatedAt,
            FeatureCreatorData.FromCreator(feature.CreatedBy), result.Paths);
    }
}
