using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

[Collection(CurrentDirectoryCollectionDefinition.Name)]
public class KitStoreFactoryCurrentDirectoryTests
{
    [Fact]
    [Trait("Requirement", "FP01-AC1")]
    public void Create_CurrentDirectoryBelowRepositoryRoot_ReturnsStoreOfKitRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        var nestedPath = tempDirectory.CreateDirectory("src");

        var factory = KitStoreFactoryTests.CreateFactory();
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
    [Trait("Requirement", "FP01-AC2")]
    public void Create_RepositoryRootWithoutShadeKit_ThrowsKitRootPathNotFoundExceptionWithCurrentDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        var factory = KitStoreFactoryTests.CreateFactory();
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDirectory.RootPath);

            // Act
            var act = () => factory.Create();

            // Assert
            act.Should().Throw<KitRootPathNotFoundException>()
                .Which.StartPath.Should().Be(Directory.GetCurrentDirectory());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Fact]
    public void Create_RelativePath_ResolvesAgainstCurrentDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        tempDirectory.CreateDirectory("src", "app");

        var factory = KitStoreFactoryTests.CreateFactory();
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDirectory.RootPath);

            // Act
            var kitStore = factory.Create(Path.Combine("src", "app"));

            // Assert
            kitStore.GetFeatureNames()
                .Should().Equal("auth");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }
}
