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

    [Fact]
    public void ChecksDirName_Contract_IsChecks()
    {
        // Assert
        KitStorePaths.ChecksDirName
            .Should().Be("checks");
    }

    [Theory]
    [InlineData("checks/definition-of-ready.md", "templates/checks/definition-of-ready.md")]
    [InlineData("checks/team/sign-off.md", "templates/checks/team/sign-off.md")]
    [InlineData("request.md", "templates/request.md")]
    public void GetTemplatePath_TemplateRelativePath_ReturnsKitRelativeTemplatePath(string templateRelativePath,
        string expected)
    {
        // Act
        var path = KitStorePaths.GetTemplatePath(templateRelativePath);

        // Assert
        path
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("gh-142-login-sso", "checks/definition-of-ready.md",
        "features/gh-142-login-sso/checks/definition-of-ready.md")]
    [InlineData("login", "checks/implement.md", "features/login/checks/implement.md")]
    [InlineData("login", "checks/team/sign-off.md", "features/login/checks/team/sign-off.md")]
    public void GetFeatureChecklistPath_FeatureIdAndChecklist_ReturnsChecklistPathMirroredInFeatureFolder(
        string featureId, string checklist, string expected)
    {
        // Act
        var path = KitStorePaths.GetFeatureChecklistPath(featureId, checklist);

        // Assert
        path
            .Should().Be(expected);
    }
}
