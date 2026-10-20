using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

[Collection(CurrentDirectoryCollectionDefinition.Name)]
public class GitRepositoryLocatorCurrentDirectoryTests
{
    [Fact]
    public void GetRepositoryRootPath_RelativePath_ResolvesAgainstCurrentDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory("src", "app");

        var locator = new GitRepositoryLocator();
        using var currentDirectory = new CurrentDirectoryScope(tempDirectory.RootPath);

        // Act
        var rootPath = locator.GetRepositoryRootPath(Path.Combine("src", "app"));

        // Assert
        rootPath
            .Should().Be(Directory.GetCurrentDirectory());
    }
}
