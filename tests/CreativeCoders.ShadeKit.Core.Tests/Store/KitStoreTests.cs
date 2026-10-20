using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Tests.Shared;

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

    [Fact]
    public async Task WriteFileAsync_NestedPathInMissingStore_CreatesDirectoriesAndWritesBytesUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));
        byte[] content = [0x23, 0x20, 0xC3, 0xA4, 0x0D, 0x0A];

        // Act
        await kitStore.WriteFileAsync("templates/checks/ready.md", content, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(".shadekit", "templates", "checks", "ready.md")
            .Should().Equal(content);
    }

    [Fact]
    public async Task WriteFileAsync_ExistingFile_ReplacesIt()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("old content", "templates", "spec.md");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        await kitStore.WriteFileAsync("templates/spec.md", "new"u8.ToArray(), TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText("templates", "spec.md")
            .Should().Be("new");
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("templates/../../outside.md")]
    public async Task WriteFileAsync_PathOutsideStore_ThrowsArgumentExceptionAndWritesNothing(string relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        var act = () => kitStore.WriteFileAsync(relativePath, [0x41], TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName(nameof(relativePath));

        Directory.EnumerateFileSystemEntries(tempDirectory.RootPath)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task WriteFileAsync_NullContent_ThrowsArgumentNullException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var act = () => kitStore.WriteFileAsync("spec.md", null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("content");
    }

    [Fact]
    public void CreateDirectory_PathInMissingStore_CreatesStoreAndDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        kitStore.CreateDirectory(KitStorePaths.FeaturesDirName);

        // Assert
        Directory.Exists(Path.Combine(tempDirectory.RootPath, ".shadekit", "features"))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../sibling")]
    public void CreateDirectory_PathOutsideStore_ThrowsArgumentExceptionAndCreatesNothing(string relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        var act = () => kitStore.CreateDirectory(relativePath);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(relativePath));

        Directory.EnumerateFileSystemEntries(tempDirectory.RootPath)
            .Should().BeEmpty();
    }

    private static KitStore CreateStore(string kitStorePath)
    {
        var formatRegistry = FormatRegistry.CreateDefault();

        return new KitStore(kitStorePath, new VersionedJsonSerializer(formatRegistry), formatRegistry);
    }
}
