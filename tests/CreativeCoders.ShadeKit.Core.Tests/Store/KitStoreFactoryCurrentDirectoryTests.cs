using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

[Collection(CurrentDirectoryCollectionDefinition.Name)]
public class KitStoreFactoryCurrentDirectoryTests
{
    [Fact]
    public void Create_CurrentDirectoryBelowKitRoot_ReturnsStoreOfKitRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        var nestedPath = tempDirectory.CreateDirectory("src");

        var factory = new KitStoreFactory();
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(nestedPath);

            // Act
            var kitStore = factory.Create();

            // Assert
            kitStore.GetFeatureNames()
                .Should().Equal("auth");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Fact]
    public void Create_NoKitRootAboveCurrentDirectory_ThrowsKitRootPathNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.SkipWhenKitRootExistsAbove();

        var factory = new KitStoreFactory();
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDirectory.RootPath);

            // Act
            var act = () => factory.Create();

            // Assert
            act.Should().Throw<KitRootPathNotFoundException>();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }
}
