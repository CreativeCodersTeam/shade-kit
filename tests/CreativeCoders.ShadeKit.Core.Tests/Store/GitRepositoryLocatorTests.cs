using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

public class GitRepositoryLocatorTests
{
    [Fact]
    public void GetRepositoryRootPath_PathIsRepositoryRoot_ReturnsPath()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        var locator = new GitRepositoryLocator();

        // Act
        var rootPath = locator.GetRepositoryRootPath(tempDirectory.RootPath);

        // Assert
        rootPath
            .Should().Be(tempDirectory.RootPath);
    }

    [Theory]
    [Trait("Requirement", "FP01-AC1")]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(25)]
    public void GetRepositoryRootPath_PathNestedBelowRepositoryRoot_ReturnsRepositoryRoot(int depth)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var nestedPath = tempDirectory.CreateDirectory(CreateNestedPathParts(depth));

        var locator = new GitRepositoryLocator();

        // Act
        var rootPath = locator.GetRepositoryRootPath(nestedPath);

        // Assert
        rootPath
            .Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC1")]
    public void GetRepositoryRootPath_GitEntryIsAFile_ReturnsDirectoryContainingTheFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("gitdir: ../main/.git/worktrees/feature", ".git");
        var nestedPath = tempDirectory.CreateDirectory("src");

        var locator = new GitRepositoryLocator();

        // Act
        var rootPath = locator.GetRepositoryRootPath(nestedPath);

        // Assert
        rootPath
            .Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    public void GetRepositoryRootPath_NestedRepository_ReturnsNearestRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var submodulePath = tempDirectory.CreateGitRoot("modules", "lib");
        var nestedPath = tempDirectory.CreateDirectory("modules", "lib", "src");

        var locator = new GitRepositoryLocator();

        // Act
        var rootPath = locator.GetRepositoryRootPath(nestedPath);

        // Assert
        rootPath
            .Should().Be(submodulePath);
    }

    [Fact]
    public void GetRepositoryRootPath_PathNextToNestedRepository_ReturnsOuterRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateGitRoot("modules", "lib");
        var siblingPath = tempDirectory.CreateDirectory("modules", "app");

        var locator = new GitRepositoryLocator();

        // Act
        var rootPath = locator.GetRepositoryRootPath(siblingPath);

        // Assert
        rootPath
            .Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    public void GetRepositoryRootPath_PathWithTrailingSeparator_ReturnsRootWithoutTrailingSeparator()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        var locator = new GitRepositoryLocator();

        // Act
        var rootPath = locator.GetRepositoryRootPath(tempDirectory.RootPath + Path.DirectorySeparatorChar);

        // Assert
        rootPath
            .Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    public void GetRepositoryRootPath_PathWithParentSegments_ReturnsNormalizedRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory("src", "app");
        var path = Path.Combine(tempDirectory.RootPath, "src", "app", "..", "..");

        var locator = new GitRepositoryLocator();

        // Act
        var rootPath = locator.GetRepositoryRootPath(path);

        // Assert
        rootPath
            .Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    public void GetRepositoryRootPath_StartDirectoryDoesNotExist_ReturnsRootOfRepositoryAroundIt()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var missingPath = Path.Combine(tempDirectory.RootPath, "src", "missing");

        var locator = new GitRepositoryLocator();

        // Act
        var rootPath = locator.GetRepositoryRootPath(missingPath);

        // Assert
        rootPath
            .Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    public void GetRepositoryRootPath_NotInsideGitRepository_ThrowsNotAGitRepositoryExceptionWithStartPath()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.SkipWhenGitRootExistsAbove();
        var nestedPath = tempDirectory.CreateDirectory("src");

        var locator = new GitRepositoryLocator();

        // Act
        var act = () => locator.GetRepositoryRootPath(nestedPath);

        // Assert
        act.Should().Throw<NotAGitRepositoryException>()
            .Which.StartPath.Should().Be(nestedPath);
    }

    [Fact]
    public void GetRepositoryRootPath_GitEntryOnlyBelowPath_ThrowsNotAGitRepositoryException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.SkipWhenGitRootExistsAbove();
        tempDirectory.CreateGitRoot("inner");

        var locator = new GitRepositoryLocator();

        // Act
        var act = () => locator.GetRepositoryRootPath(tempDirectory.RootPath);

        // Assert
        act.Should().Throw<NotAGitRepositoryException>()
            .Which.StartPath.Should().Be(tempDirectory.RootPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetRepositoryRootPath_NullEmptyOrWhitespacePath_ThrowsArgumentException(string? path)
    {
        // Arrange
        var locator = new GitRepositoryLocator();

        // Act
        var act = () => locator.GetRepositoryRootPath(path!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(path));
    }

    private static string[] CreateNestedPathParts(int depth)
    {
        return [.. Enumerable.Range(1, depth).Select(level => $"d{level}")];
    }
}
