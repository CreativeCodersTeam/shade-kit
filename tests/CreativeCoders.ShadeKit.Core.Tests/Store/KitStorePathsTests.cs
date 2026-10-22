using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

public class KitStorePathsTests
{
    [Theory]
    [InlineData("gh-142-login-sso", "features/gh-142-login-sso/feature.json")]
    [InlineData("Draft Folder", "features/Draft Folder/feature.json")]
    public void GetFeatureFilePath_FeatureName_ReturnsKitRelativeFeatureFilePath(string featureName, string expected)
    {
        // Act
        var path = KitStorePaths.GetFeatureFilePath(featureName);

        // Assert
        path
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(KitStorePaths.ConfigFileName, ".shadekit/config.json")]
    [InlineData("features/gh-142-login-sso/feature.json", ".shadekit/features/gh-142-login-sso/feature.json")]
    [InlineData(KitStorePaths.CurrentFeatureFilePath, ".shadekit/features/current-feature.json")]
    public void GetDisplayPath_KitRelativePathWithSlashes_ReturnsRepositoryRelativePath(string kitRelativePath,
        string expected)
    {
        // Act
        var path = KitStorePaths.GetDisplayPath(kitRelativePath);

        // Assert
        path
            .Should().Be(expected);
    }

    [Fact]
    public void GetDisplayPath_KitRelativePathWithPlatformSeparators_ReturnsRepositoryRelativePathWithSlashes()
    {
        // Arrange
        var kitRelativePath = string.Join(Path.DirectorySeparatorChar, "features", "gh-142-login-sso", "feature.json");

        // Act
        var path = KitStorePaths.GetDisplayPath(kitRelativePath);

        // Assert
        path
            .Should().Be(".shadekit/features/gh-142-login-sso/feature.json");
    }

    [Theory]
    [InlineData("gh-142-login-sso", ".shadekit/features/gh-142-login-sso/")]
    [InlineData("Draft Folder", ".shadekit/features/Draft Folder/")]
    [InlineData("../x", ".shadekit/features/../x/")]
    public void GetRepositoryFeatureDirPath_FeatureName_ReturnsRepositoryRelativePathWithTrailingSlashUnchecked(
        string featureName, string expected)
    {
        // Act
        var path = KitStorePaths.GetRepositoryFeatureDirPath(featureName);

        // Assert
        path
            .Should().Be(expected);
    }
}
