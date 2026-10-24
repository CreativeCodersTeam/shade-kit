using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

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
        using var currentDirectory = new CurrentDirectoryScope(nestedPath);

        // Act
        var kitStore = factory.Create();

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("auth");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Create_RepositoryRootWithoutShadeKit_ThrowsKitRootPathNotFoundExceptionWithCurrentDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        var factory = KitStoreFactoryTests.CreateFactory();
        using var currentDirectory = new CurrentDirectoryScope(tempDirectory.RootPath);

        // Act
        var act = () => factory.Create();

        // Assert
        act.Should().Throw<KitRootPathNotFoundException>()
            .Which.StartPath.Should().Be(Directory.GetCurrentDirectory());
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
        using var currentDirectory = new CurrentDirectoryScope(tempDirectory.RootPath);

        // Act
        var kitStore = factory.Create(Path.Combine("src", "app"));

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("auth");
    }

    [Fact]
    public void Create_WithoutPath_PassesCurrentDirectoryToLocator()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory(".shadekit");

        var locator = A.Fake<IGitRepositoryLocator>();
        A.CallTo(() => locator.GetRepositoryRootPath(A<string>._)).Returns(tempDirectory.RootPath);

        var factory = KitStoreFactoryTests.CreateFactory(locator);
        using var currentDirectory = new CurrentDirectoryScope(tempDirectory.RootPath);

        // Act
        factory.Create();

        // Assert
        A.CallTo(() => locator.GetRepositoryRootPath(Directory.GetCurrentDirectory()))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void CreateForRepositoryRoot_RelativePath_ReturnsStoreWithFullPathResolvedAgainstCurrentDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();

        var factory = KitStoreFactoryTests.CreateFactory();
        using var currentDirectory = new CurrentDirectoryScope(tempDirectory.RootPath);

        // Act
        var kitStore = factory.CreateForRepositoryRoot(".");

        // Assert
        kitStore.RootPath
            .Should().Be(Path.Combine(Directory.GetCurrentDirectory(), ".shadekit"));
    }
}
