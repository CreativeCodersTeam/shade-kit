using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
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

        var kitStore = CreateStore(tempDirectory.RootPath);

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

        var kitStore = CreateStore(tempDirectory.RootPath);

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

        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var featureNames = kitStore.GetFeatureNames();

        // Assert
        featureNames
            .Should().BeEmpty();
    }

    [Fact]
    public void GetFeatureNames_FeaturesDirectoryMissing_ReturnsEmpty()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();

        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var featureNames = kitStore.GetFeatureNames();

        // Assert
        featureNames
            .Should().BeEmpty();
    }

    [Fact]
    public void GetFeatureNames_FeaturesIsAFile_ReturnsEmpty()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateFile("features");

        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var featureNames = kitStore.GetFeatureNames();

        // Assert
        featureNames
            .Should().BeEmpty();
    }

    [Fact]
    public void GetFeatureNames_StorePathDoesNotExist_ReturnsEmptyWithoutCreatingDirectories()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var missingStorePath = Path.Combine(tempDirectory.RootPath, ".shadekit");

        var kitStore = CreateStore(missingStorePath);

        // Act
        var featureNames = kitStore.GetFeatureNames();

        // Assert
        featureNames
            .Should().BeEmpty();

        Path.Exists(missingStorePath)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_NullEmptyOrWhitespacePath_ThrowsArgumentException(string? kitStorePath)
    {
        // Act
        var act = () => CreateStore(kitStorePath!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullSerializer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new KitStore(".shadekit", null!, FormatRegistry.CreateDefault());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serializer");
    }

    [Fact]
    public void Ctor_NullFormatRegistry_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new KitStore(".shadekit", new VersionedJsonSerializer(FormatRegistry.CreateDefault()), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formatRegistry");
    }

    [Fact]
    public void RootPath_PathWithTrailingSeparator_ReturnsFullPathWithoutTrailingSeparator()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStorePath = tempDirectory.CreateDirectory(".shadekit");

        // Act
        var kitStore = CreateStore(kitStorePath + Path.DirectorySeparatorChar);

        // Assert
        kitStore.RootPath
            .Should().Be(kitStorePath);
    }

    [Fact]
    public void RootPath_PathWithParentSegments_ReturnsNormalizedFullPath()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStorePath = tempDirectory.CreateDirectory(".shadekit");

        // Act
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, "src", "..", ".shadekit"));

        // Assert
        kitStore.RootPath
            .Should().Be(kitStorePath);
    }

    private static KitStore CreateStore(string kitStorePath)
    {
        var formatRegistry = FormatRegistry.CreateDefault();

        return new KitStore(kitStorePath, new VersionedJsonSerializer(formatRegistry), formatRegistry);
    }
}
