using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

public class KitStoreFactoryTests
{
    [Fact]
    [Trait("Requirement", "FP01-AC1")]
    public void Create_PathIsGitRootWithKitRoot_ReturnsStoreOfShadeKitDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        tempDirectory.CreateDirectory(".shadekit", "features", "billing");

        var factory = CreateFactory();

        // Act
        var kitStore = factory.Create(tempDirectory.RootPath);

        // Assert
        kitStore.RootPath
            .Should().Be(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        kitStore.GetFeatureNames()
            .Should().BeEquivalentTo("auth", "billing");
    }

    [Theory]
    [Trait("Requirement", "FP01-AC1")]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(25)]
    public void Create_PathNestedBelowGitRoot_ReturnsStoreOfRepositoryRoot(int depth)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        var nestedPath = tempDirectory.CreateDirectory(CreateNestedPathParts(depth));

        var factory = CreateFactory();

        // Act
        var kitStore = factory.Create(nestedPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("auth");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC1")]
    public void Create_GitEntryIsAFile_TreatsDirectoryAsRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("gitdir: ../main/.git/worktrees/feature", ".git");
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        var nestedPath = tempDirectory.CreateDirectory("src");

        var factory = CreateFactory();

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
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "inside");
        tempDirectory.CreateDirectory("features", "outside");

        var factory = CreateFactory();

        // Act
        var kitStore = factory.Create(tempDirectory.RootPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("inside");
    }

    [Fact]
    public void Create_ShadeKitBelowRepositoryRoot_IgnoresItAndReturnsStoreOfRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "root");
        tempDirectory.CreateDirectory("inner", ".shadekit", "features", "inner");
        var nestedPath = tempDirectory.CreateDirectory("inner", "src");

        var factory = CreateFactory();

        // Act
        var kitStore = factory.Create(nestedPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("root");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Create_ShadeKitOnlyBelowRepositoryRoot_ThrowsKitRootPathNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory("inner", ".shadekit", "features", "inner");
        var nestedPath = tempDirectory.CreateDirectory("inner", "src");

        var factory = CreateFactory();

        // Act
        var act = () => factory.Create(nestedPath);

        // Assert
        var exception = act.Should().Throw<KitRootPathNotFoundException>().Which;

        exception.StartPath
            .Should().Be(nestedPath);

        exception.GitRootPath
            .Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Create_NestedRepositoryWithoutShadeKit_StopsAtNestedRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "outer");
        var submodulePath = tempDirectory.CreateGitRoot("modules", "lib");
        var nestedPath = tempDirectory.CreateDirectory("modules", "lib", "src");

        var factory = CreateFactory();

        // Act
        var act = () => factory.Create(nestedPath);

        // Assert
        act.Should().Throw<KitRootPathNotFoundException>()
            .Which.GitRootPath.Should().Be(submodulePath);
    }

    [Fact]
    public void Create_NotInsideGitRepository_ThrowsNotAGitRepositoryException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.SkipWhenGitRootExistsAbove();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        var nestedPath = tempDirectory.CreateDirectory("src");

        var factory = CreateFactory();

        // Act
        var act = () => factory.Create(nestedPath);

        // Assert
        act.Should().Throw<NotAGitRepositoryException>()
            .Which.StartPath.Should().Be(nestedPath);
    }

    [Fact]
    public void Create_ShadeKitWithoutFeaturesDirectory_ReturnsStoreThatThrowsFeaturesDirectoryNotFoundOnRead()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit");

        var factory = CreateFactory();

        // Act
        var kitStore = factory.Create(tempDirectory.RootPath);

        // Assert
        var act = () => kitStore.GetFeatureNames();

        act.Should().Throw<FeaturesDirectoryNotFoundException>();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Create_ShadeKitIsAFile_ThrowsKitRootPathNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateFile(".shadekit");

        var factory = CreateFactory();

        // Act
        var act = () => factory.Create(tempDirectory.RootPath);

        // Assert
        act.Should().Throw<KitRootPathNotFoundException>()
            .Which.GitRootPath.Should().Be(tempDirectory.RootPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_NullOrWhitespacePath_ThrowsArgumentException(string? startPath)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => factory.Create(startPath!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Create_GitRootWithTrailingSeparatorWithoutShadeKit_ReportsGitRootWithoutTrailingSeparator()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        var factory = CreateFactory();

        // Act
        var act = () => factory.Create(tempDirectory.RootPath + Path.DirectorySeparatorChar);

        // Assert
        act.Should().Throw<KitRootPathNotFoundException>()
            .Which.GitRootPath.Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    public void Ctor_NullSerializer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new KitStoreFactory(null!, FormatRegistry.CreateDefault());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serializer");
    }

    [Fact]
    public void Ctor_NullFormatRegistry_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new KitStoreFactory(new VersionedJsonSerializer(FormatRegistry.CreateDefault()), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formatRegistry");
    }

    internal static KitStoreFactory CreateFactory()
    {
        var formatRegistry = FormatRegistry.CreateDefault();

        return new KitStoreFactory(new VersionedJsonSerializer(formatRegistry), formatRegistry);
    }

    private static string[] CreateNestedPathParts(int depth)
    {
        return [.. Enumerable.Range(1, depth).Select(level => $"d{level}")];
    }
}
