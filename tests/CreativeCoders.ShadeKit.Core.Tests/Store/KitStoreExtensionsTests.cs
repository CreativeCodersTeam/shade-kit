using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

public class KitStoreExtensionsTests
{
    [Fact]
    public void GetRepositoryRootPath_StoreBelowRepository_ReturnsParentOfKitRoot()
    {
        // Arrange
        var repositoryRootPath = Path.Combine(Path.GetTempPath(), "repo");
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.RootPath).Returns(Path.Combine(repositoryRootPath, KitStorePaths.RootPath));

        // Act
        var result = store.GetRepositoryRootPath();

        // Assert
        result
            .Should().Be(repositoryRootPath);
    }

    [Fact]
    public void GetRepositoryRootPath_KitRootIsFileSystemRoot_ThrowsArgumentException()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.RootPath).Returns(Path.GetPathRoot(Path.GetTempPath())!);

        // Act
        var act = () => store.GetRepositoryRootPath();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("store");
    }

    [Fact]
    public void GetRepositoryRootPath_NullStore_ThrowsArgumentNullException()
    {
        // Act
        var act = () => KitStoreExtensions.GetRepositoryRootPath(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
