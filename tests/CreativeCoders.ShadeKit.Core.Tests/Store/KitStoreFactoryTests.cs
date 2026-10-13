using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

public class KitStoreFactoryTests
{
    [Fact]
    public void Create_PathIsKitRoot_ReturnsStoreReadingFeaturesFromShadeKitDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        tempDirectory.CreateDirectory(".shadekit", "features", "billing");

        var factory = new KitStoreFactory();

        // Act
        var kitStore = factory.Create(tempDirectory.RootPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().BeEquivalentTo("auth", "billing");
    }

    [Fact]
    public void Create_PathIsNestedBelowKitRoot_ReturnsStoreOfKitRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        var nestedPath = tempDirectory.CreateDirectory("src", "app", "deeper");

        var factory = new KitStoreFactory();

        // Act
        var kitStore = factory.Create(nestedPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("auth");
    }

    [Fact]
    public void Create_FeaturesDirectoryNextToShadeKitDirectory_IgnoresIt()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory(".shadekit", "features", "inside");
        tempDirectory.CreateDirectory("features", "outside");

        var factory = new KitStoreFactory();

        // Act
        var kitStore = factory.Create(tempDirectory.RootPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("inside");
    }

    [Fact]
    public void Create_NoKitRootInPathOrParents_ThrowsKitRootPathNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.SkipWhenKitRootExistsAbove();
        var nestedPath = tempDirectory.CreateDirectory("src");

        var factory = new KitStoreFactory();

        // Act
        var act = () => factory.Create(nestedPath);

        // Assert
        act.Should().Throw<KitRootPathNotFoundException>()
            .Which.StartPath.Should().Be(nestedPath);
    }

    [Fact]
    public void Create_KitRootNineteenLevelsAbovePath_ReturnsStoreOfKitRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        var nestedPath = tempDirectory.CreateDirectory(CreateNestedPathParts(19));

        var factory = new KitStoreFactory();

        // Act
        var kitStore = factory.Create(nestedPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("auth");
    }

    [Fact]
    public void Create_KitRootTwentyLevelsAbovePath_ThrowsKitRootPathNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.SkipWhenKitRootExistsAbove();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        var nestedPath = tempDirectory.CreateDirectory(CreateNestedPathParts(20));

        var factory = new KitStoreFactory();

        // Act
        var act = () => factory.Create(nestedPath);

        // Assert
        act.Should().Throw<KitRootPathNotFoundException>()
            .Which.StartPath.Should().Be(nestedPath);
    }

    [Fact]
    public void Create_NestedKitRoots_ReturnsStoreOfClosestKitRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory(".shadekit", "features", "outer");
        tempDirectory.CreateDirectory("inner", ".shadekit", "features", "inner");
        var nestedPath = tempDirectory.CreateDirectory("inner", "src");

        var factory = new KitStoreFactory();

        // Act
        var kitStore = factory.Create(nestedPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("inner");
    }

    [Fact]
    public void Create_ShadeKitWithoutFeaturesDirectory_ReturnsStoreThatThrowsFeaturesDirectoryNotFoundOnRead()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory(".shadekit");

        var factory = new KitStoreFactory();

        // Act
        var kitStore = factory.Create(tempDirectory.RootPath);

        // Assert
        var act = () => kitStore.GetFeatureNames();

        act.Should().Throw<FeaturesDirectoryNotFoundException>();
    }

    [Fact]
    public void Create_ShadeKitIsAFile_ThrowsKitRootPathNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.SkipWhenKitRootExistsAbove();
        tempDirectory.CreateFile(".shadekit");

        var factory = new KitStoreFactory();

        // Act
        var act = () => factory.Create(tempDirectory.RootPath);

        // Assert
        act.Should().Throw<KitRootPathNotFoundException>();
    }

    private static string[] CreateNestedPathParts(int depth)
    {
        return [.. Enumerable.Range(1, depth).Select(level => $"d{level}")];
    }
}
