using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

public class KitStoreTests
{
    [Fact]
    public void GetFeatureNames_FeatureDirectoriesExist_ReturnsDirectoryNames()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory("features", "auth");
        tempDirectory.CreateDirectory("features", "billing");

        var kitStore = new KitStore(tempDirectory.RootPath);

        // Act
        var featureNames = kitStore.GetFeatureNames();

        // Assert
        featureNames
            .Should().BeEquivalentTo("auth", "billing");
    }

    [Fact]
    public void GetFeatureNames_FeaturesDirectoryContainsFiles_IgnoresFiles()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory("features", "auth");
        tempDirectory.CreateFile("features", "readme.md");

        var kitStore = new KitStore(tempDirectory.RootPath);

        // Act
        var featureNames = kitStore.GetFeatureNames();

        // Assert
        featureNames
            .Should().Equal("auth");
    }

    [Fact]
    public void GetFeatureNames_FeaturesDirectoryEmpty_ReturnsEmpty()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory("features");

        var kitStore = new KitStore(tempDirectory.RootPath);

        // Act
        var featureNames = kitStore.GetFeatureNames();

        // Assert
        featureNames
            .Should().BeEmpty();
    }

    [Fact]
    public void GetFeatureNames_FeaturesDirectoryMissing_ThrowsFeaturesDirectoryNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();

        var kitStore = new KitStore(tempDirectory.RootPath);

        // Act
        var act = () => kitStore.GetFeatureNames();

        // Assert
        act.Should().Throw<FeaturesDirectoryNotFoundException>()
            .Which.FeaturesPath.Should().Be(Path.Combine(tempDirectory.RootPath, "features"));
    }

    [Fact]
    public void GetFeatureNames_FeaturesIsAFile_ThrowsFeaturesDirectoryNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateFile("features");

        var kitStore = new KitStore(tempDirectory.RootPath);

        // Act
        var act = () => kitStore.GetFeatureNames();

        // Assert
        act.Should().Throw<FeaturesDirectoryNotFoundException>()
            .Which.FeaturesPath.Should().Be(Path.Combine(tempDirectory.RootPath, "features"));
    }

    [Fact]
    public void GetFeatureNames_StorePathDoesNotExist_ThrowsFeaturesDirectoryNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var missingStorePath = Path.Combine(tempDirectory.RootPath, ".shadekit");

        var kitStore = new KitStore(missingStorePath);

        // Act
        var act = () => kitStore.GetFeatureNames();

        // Assert
        var exception = act.Should().Throw<FeaturesDirectoryNotFoundException>().Which;

        exception.FeaturesPath
            .Should().Be(Path.Combine(missingStorePath, "features"));

        exception.Message
            .Should().Contain(exception.FeaturesPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_NullEmptyOrWhitespacePath_ThrowsArgumentException(string? kitStorePath)
    {
        // Act
        var act = () => new KitStore(kitStorePath!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
