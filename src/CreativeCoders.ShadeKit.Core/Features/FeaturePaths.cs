using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the paths of the folder and the files of a feature, relative to the repository root with <c>/</c> as
/// separator.
/// </summary>
/// <param name="FeatureDir">
/// The path of the feature folder with a trailing <c>/</c>, for example <c>.shadekit/features/gh-142-login-sso/</c>.
/// </param>
/// <param name="FeatureFile">
/// The path of the <c>feature.json</c> of the feature, for example
/// <c>.shadekit/features/gh-142-login-sso/feature.json</c>.
/// </param>
/// <param name="Request">
/// The path of the <c>request.md</c> of the feature, for example <c>.shadekit/features/gh-142-login-sso/request.md</c>.
/// </param>
public sealed record FeaturePaths(string FeatureDir, string FeatureFile, string Request)
{
    /// <summary>
    /// Creates the paths of the feature with the specified id.
    /// </summary>
    /// <param name="featureId">The id of the feature.</param>
    /// <returns>The paths of the folder and the files of the feature.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="featureId" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidFeatureIdException"><paramref name="featureId" /> is not a valid feature id.</exception>
    public static FeaturePaths For(string featureId)
    {
        FeatureIds.EnsureValid(featureId);

        var featureDir = KitStorePaths.GetRepositoryFeatureDirPath(featureId);

        return new FeaturePaths(featureDir, featureDir + KitStorePaths.FeatureFileName,
            featureDir + KitStorePaths.RequestFileName);
    }
}
