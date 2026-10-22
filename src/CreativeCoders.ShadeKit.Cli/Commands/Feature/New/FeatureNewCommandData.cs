using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.New;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit feature new --json</c> returns.
/// </summary>
/// <param name="Id">The id of the new feature.</param>
/// <param name="Title">The title of the new feature.</param>
/// <param name="Profile">The workflow profile of the new feature.</param>
/// <param name="Status">The status of the new feature, written as <c>active</c>.</param>
/// <param name="Source">The source of the request of the new feature.</param>
/// <param name="Paths">The repository-relative paths of the feature folder and its files.</param>
/// <param name="SuggestedBranch">The suggested branch name <c>feature/&lt;id&gt;</c>.</param>
public sealed record FeatureNewCommandData(
    string Id,
    string Title,
    string Profile,
    FeatureStatus Status,
    FeatureSourceData Source,
    FeaturePaths Paths,
    string SuggestedBranch)
{
    /// <summary>
    /// Creates the data from the result of a feature creation.
    /// </summary>
    /// <param name="result">The result of the creation.</param>
    /// <returns>The data with the feature, the paths, and the suggested branch of <paramref name="result" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static FeatureNewCommandData FromResult(NewFeatureResult result)
    {
        Ensure.NotNull(result);

        var feature = result.Feature;

        return new FeatureNewCommandData(feature.Id, feature.Title, feature.Profile, feature.Status,
            FeatureSourceData.FromSource(feature.Source), result.Paths, result.SuggestedBranch);
    }
}
