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

    [Fact]
    public async Task ReadFileAsync_ExistingFile_ReturnsBytesUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        byte[] content = [0xEF, 0xBB, 0xBF, 0x23, 0x20, 0xC3, 0xA4, 0x0D, 0x0A];
        tempDirectory.WriteFileBytes(content, "features", "login", "request.md");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var result = await kitStore.ReadFileAsync("features/login/request.md", TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Equal(content);
    }

    [Fact]
    public async Task ReadFileAsync_EmptyFile_ReturnsEmptyArray()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateFile("request.md");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var result = await kitStore.ReadFileAsync("request.md", TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull()
            .And.BeEmpty();
    }

    [Fact]
    public async Task ReadFileAsync_MissingFile_ReturnsNull()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        var result = await kitStore.ReadFileAsync("features/login/request.md", TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public async Task ReadFileAsync_PathIsDirectory_ReturnsNull()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory("features", "login");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var result = await kitStore.ReadFileAsync("features/login", TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(InvalidRelativePaths))]
    public async Task ReadFileAsync_InvalidPath_ThrowsArgumentException(string? relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("outside", "outside.md");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        var act = () => kitStore.ReadFileAsync(relativePath!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName(nameof(relativePath));
    }

    [Fact]
    public void FileExists_ExistingFile_ReturnsTrue()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var exists = kitStore.FileExists("features/login/feature.json");

        // Assert
        exists
            .Should().BeTrue();
    }

    [Fact]
    public void FileExists_MissingFile_ReturnsFalse()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory("features", "login");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var exists = kitStore.FileExists("features/login/feature.json");

        // Assert
        exists
            .Should().BeFalse();
    }

    [Fact]
    public void FileExists_PathIsDirectory_ReturnsFalse()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory("features", "login");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var exists = kitStore.FileExists("features/login");

        // Assert
        exists
            .Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(InvalidRelativePaths))]
    public void FileExists_InvalidPath_ThrowsArgumentException(string? relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("outside", "outside.md");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        var act = () => kitStore.FileExists(relativePath!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(relativePath));
    }

    [Fact]
    public void DirectoryExists_ExistingDirectory_ReturnsTrue()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory("features", "login");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var exists = kitStore.DirectoryExists("features/login");

        // Assert
        exists
            .Should().BeTrue();
    }

    [Fact]
    public void DirectoryExists_MissingDirectory_ReturnsFalse()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        var exists = kitStore.DirectoryExists("features/login");

        // Assert
        exists
            .Should().BeFalse();
    }

    [Fact]
    public void DirectoryExists_PathIsFile_ReturnsFalse()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var exists = kitStore.DirectoryExists("features/login/feature.json");

        // Assert
        exists
            .Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(InvalidRelativePaths))]
    public void DirectoryExists_InvalidPath_ThrowsArgumentException(string? relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("outside", "outside.md");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        var act = () => kitStore.DirectoryExists(relativePath!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(relativePath));
    }

    [Fact]
    public void GetFiles_NestedFiles_ReturnsAllFilesRecursivelyAsKitRelativePathsInOrdinalOrder()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        tempDirectory.WriteFile("# Login", "features", "login", "request.md");
        tempDirectory.WriteFile("upper", "features", "login", "Z.md");
        tempDirectory.WriteFile("hidden", "features", "login", ".hidden");
        tempDirectory.WriteFile("nested", "features", "login", "notes", "deep", "a.md");
        tempDirectory.CreateDirectory("features", "login", "empty");
        tempDirectory.WriteFile("{}", "features", "billing", "feature.json");
        tempDirectory.WriteFile("{}", "features", "current-feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var files = kitStore.GetFiles("features/login");

        // Assert
        files
            .Should().Equal(
                "features/login/.hidden",
                "features/login/Z.md",
                "features/login/feature.json",
                "features/login/notes/deep/a.md",
                "features/login/request.md");
    }

    [Fact]
    public void GetFiles_PathWithTrailingSeparatorAndParentSegment_ReturnsNormalizedKitRelativePaths()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var files = kitStore.GetFiles("templates/../features/login/");

        // Assert
        files
            .Should().Equal("features/login/feature.json");
    }

    [Fact]
    public void GetFiles_EmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory("features", "login", "empty");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var files = kitStore.GetFiles("features/login");

        // Assert
        files
            .Should().BeEmpty();
    }

    [Fact]
    public void GetFiles_MissingDirectory_ReturnsEmptyWithoutCreatingDirectories()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        var files = kitStore.GetFiles("features/login");

        // Assert
        files
            .Should().BeEmpty();

        Directory.EnumerateFileSystemEntries(tempDirectory.RootPath)
            .Should().BeEmpty();
    }

    [Fact]
    public void GetFiles_PathIsFile_ReturnsEmpty()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var files = kitStore.GetFiles("features/login/feature.json");

        // Assert
        files
            .Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidRelativePaths))]
    public void GetFiles_InvalidPath_ThrowsArgumentException(string? relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("outside", "outside.md");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        var act = () => kitStore.GetFiles(relativePath!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(relativePath));
    }

    [Fact]
    public void DeleteFile_ExistingFile_DeletesOnlyThatFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "current-feature.json");
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        kitStore.DeleteFile("features/current-feature.json");

        // Assert
        File.Exists(Path.Combine(tempDirectory.RootPath, "features", "current-feature.json"))
            .Should().BeFalse();

        File.Exists(Path.Combine(tempDirectory.RootPath, "features", "login", "feature.json"))
            .Should().BeTrue();
    }

    [Fact]
    public void DeleteFile_MissingFile_DoesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.DeleteFile("features/current-feature.json");

        // Assert
        act.Should().NotThrow();

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void DeleteFile_PathIsDirectory_LeavesDirectoryUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.DeleteFile("features/login");

        // Assert
        act.Should().NotThrow();

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [MemberData(nameof(InvalidRelativePaths))]
    public void DeleteFile_InvalidPath_ThrowsArgumentExceptionAndDeletesNothing(string? relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("outside", "outside.md");
        tempDirectory.WriteFile("{}", ".shadekit", "config.json");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.DeleteFile(relativePath!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(relativePath));

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void DeleteFile_RootedPathInsideStore_ThrowsArgumentExceptionAndKeepsFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("{}", "features", "current-feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var act = () => kitStore.DeleteFile(filePath);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("relativePath");

        File.Exists(filePath)
            .Should().BeTrue();
    }

    [Fact]
    public void DeleteDirectory_DirectoryWithNestedContent_DeletesDirectoryAndContentOnly()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        tempDirectory.WriteFile("hidden", "features", "login", ".hidden");
        tempDirectory.WriteFile("nested", "features", "login", "notes", "deep", "a.md");
        tempDirectory.CreateDirectory("features", "login", "empty");
        tempDirectory.WriteFile("{}", "features", "login-2", "feature.json");
        tempDirectory.WriteFile("{}", "features", "current-feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        kitStore.DeleteDirectory("features/login");

        // Assert
        Directory.Exists(Path.Combine(tempDirectory.RootPath, "features", "login"))
            .Should().BeFalse();

        tempDirectory.TakeSnapshot().Keys
            .Should().BeEquivalentTo(
                "features",
                Path.Combine("features", "login-2"),
                Path.Combine("features", "login-2", "feature.json"),
                Path.Combine("features", "current-feature.json"));
    }

    [Fact]
    public void DeleteDirectory_MissingDirectory_DoesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.DeleteDirectory("features/billing");

        // Assert
        act.Should().NotThrow();

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void DeleteDirectory_PathIsFile_LeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "current-feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.DeleteDirectory("features/current-feature.json");

        // Assert
        act.Should().NotThrow();

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [MemberData(nameof(InvalidRelativePaths))]
    public void DeleteDirectory_InvalidPath_ThrowsArgumentExceptionAndDeletesNothing(string? relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("outside", "outside.md");
        tempDirectory.WriteFile("{}", ".shadekit", "features", "login", "feature.json");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.DeleteDirectory(relativePath!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(relativePath));

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task ReadFileAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("# Login", "request.md");
        var kitStore = CreateStore(tempDirectory.RootPath);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = () => kitStore.ReadFileAsync("request.md", cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void GetFiles_DirectoryContainsLinksToDirectories_DoesNotListOrFollowThem()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var outsidePath = tempDirectory.CreateDirectory("outside");
        tempDirectory.WriteFile("outside", "outside", "secret.md");
        var featurePath = tempDirectory.CreateDirectory(".shadekit", "features", "login");
        tempDirectory.WriteFile("{}", ".shadekit", "features", "login", "feature.json");
        tempDirectory.CreateDirectorySymbolicLinkOrSkip(outsidePath, ".shadekit", "features", "login", "outside-link");
        tempDirectory.CreateDirectorySymbolicLinkOrSkip(featurePath, ".shadekit", "features", "login", "loop-link");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        var files = kitStore.GetFiles("features/login");

        // Assert
        files
            .Should().Equal("features/login/feature.json");
    }

    [Fact]
    public void GetFiles_DirectoryContainsLinkToFile_ListsLinkAsFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        tempDirectory.CreateSymbolicLinkOrSkip("feature.json", "features", "login", "feature-link.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        var files = kitStore.GetFiles("features/login");

        // Assert
        files
            .Should().Equal("features/login/feature-link.json", "features/login/feature.json");
    }

    [Fact]
    public void DeleteDirectory_PathIsLinkToDirectoryOutsideStore_RemovesOnlyLink()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var outsidePath = tempDirectory.CreateDirectory("outside");
        var outsideFilePath = tempDirectory.WriteFile("outside", "outside", "secret.md");
        tempDirectory.CreateDirectory(".shadekit", "features");
        var linkPath = tempDirectory.CreateDirectorySymbolicLinkOrSkip(outsidePath, ".shadekit", "features", "login");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        kitStore.DeleteDirectory("features/login");

        // Assert
        Path.Exists(linkPath)
            .Should().BeFalse();

        File.Exists(outsideFilePath)
            .Should().BeTrue();
    }

    [Fact]
    public void DeleteDirectory_DirectoryContainsLinkToDirectoryOutsideStore_KeepsLinkTargetContent()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var outsidePath = tempDirectory.CreateDirectory("outside");
        var outsideFilePath = tempDirectory.WriteFile("outside", "outside", "secret.md");
        tempDirectory.WriteFile("{}", ".shadekit", "features", "login", "feature.json");
        tempDirectory.CreateDirectorySymbolicLinkOrSkip(outsidePath, ".shadekit", "features", "login", "outside-link");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        // Act
        kitStore.DeleteDirectory("features/login");

        // Assert
        Directory.Exists(Path.Combine(tempDirectory.RootPath, ".shadekit", "features", "login"))
            .Should().BeFalse();

        File.Exists(outsideFilePath)
            .Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDirectory_ThenQueryThroughStore_ReportsDirectoryAndFilesAsMissing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));
        await kitStore.WriteFileAsync("features/login/request.md", "# Login"u8.ToArray(),
            TestContext.Current.CancellationToken);

        // Act
        kitStore.DeleteDirectory("features/login");

        // Assert
        kitStore.DirectoryExists("features/login")
            .Should().BeFalse();

        kitStore.FileExists("features/login/request.md")
            .Should().BeFalse();

        kitStore.GetFiles("features")
            .Should().BeEmpty();

        (await kitStore.ReadFileAsync("features/login/request.md", TestContext.Current.CancellationToken))
            .Should().BeNull();
    }

    [Fact]
    public void MoveDirectory_DirectoryWithNestedContent_MovesDirectoryAndContent()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", ".login.1a2b3c4d.tmp", "feature.json");
        tempDirectory.WriteFile("nested", "features", ".login.1a2b3c4d.tmp", "notes", "a.md");
        tempDirectory.WriteFile("{}", "features", "current-feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        kitStore.MoveDirectory("features/.login.1a2b3c4d.tmp", "features/login");

        // Assert
        tempDirectory.TakeSnapshot().Keys
            .Should().BeEquivalentTo(
                "features",
                Path.Combine("features", "login"),
                Path.Combine("features", "login", "feature.json"),
                Path.Combine("features", "login", "notes"),
                Path.Combine("features", "login", "notes", "a.md"),
                Path.Combine("features", "current-feature.json"));

        tempDirectory.ReadFileText("features", "login", "notes", "a.md")
            .Should().Be("nested");
    }

    [Fact]
    public void MoveDirectory_DestinationParentMissing_CreatesParentAndMovesDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "staging", "feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);

        // Act
        kitStore.MoveDirectory("staging", "features/login");

        // Assert
        File.Exists(Path.Combine(tempDirectory.RootPath, "features", "login", "feature.json"))
            .Should().BeTrue();

        Directory.Exists(Path.Combine(tempDirectory.RootPath, "staging"))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MoveDirectory_DestinationDirectoryExists_ThrowsIOExceptionAndMovesNothing(bool destinationIsEmpty)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("new", "features", ".login.1a2b3c4d.tmp", "feature.json");
        tempDirectory.CreateDirectory("features", "login");

        if (!destinationIsEmpty)
        {
            tempDirectory.WriteFile("existing", "features", "login", "feature.json");
        }

        var kitStore = CreateStore(tempDirectory.RootPath);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.MoveDirectory("features/.login.1a2b3c4d.tmp", "features/login");

        // Assert
        act.Should().Throw<IOException>()
            .Which.Should().NotBeOfType<DirectoryNotFoundException>();

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void MoveDirectory_DestinationIsFile_ThrowsIOExceptionAndMovesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("new", "features", ".login.1a2b3c4d.tmp", "feature.json");
        tempDirectory.WriteFile("file", "features", "login");
        var kitStore = CreateStore(tempDirectory.RootPath);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.MoveDirectory("features/.login.1a2b3c4d.tmp", "features/login");

        // Assert
        act.Should().Throw<IOException>()
            .WithMessage("*'.shadekit/features/login' already exists*");

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void MoveDirectory_SourceMissing_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory("features");
        var kitStore = CreateStore(tempDirectory.RootPath);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.MoveDirectory("features/.login.1a2b3c4d.tmp", "features/login");

        // Assert
        act.Should().Throw<DirectoryNotFoundException>()
            .WithMessage("*'.shadekit/features/.login.1a2b3c4d.tmp'*");

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void MoveDirectory_SourceIsFile_ThrowsDirectoryNotFoundExceptionAndMovesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "current-feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.MoveDirectory("features/current-feature.json", "features/login");

        // Assert
        act.Should().Throw<DirectoryNotFoundException>();

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void MoveDirectory_DestinationInsideSource_ThrowsIOExceptionAndMovesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", "features", "login", "feature.json");
        var kitStore = CreateStore(tempDirectory.RootPath);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.MoveDirectory("features/login", "features/login/nested");

        // Assert
        act.Should().Throw<IOException>();

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [MemberData(nameof(InvalidRelativePaths))]
    public void MoveDirectory_InvalidSourcePath_ThrowsArgumentExceptionAndMovesNothing(string? relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("outside", "outside.md");
        tempDirectory.WriteFile("{}", ".shadekit", "features", "login", "feature.json");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.MoveDirectory(relativePath!, "features/moved");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(relativePath));

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [MemberData(nameof(InvalidRelativePaths))]
    public void MoveDirectory_InvalidDestinationPath_ThrowsArgumentExceptionAndMovesNothing(string? relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", ".shadekit", "features", "login", "feature.json");
        var kitStore = CreateStore(Path.Combine(tempDirectory.RootPath, ".shadekit"));
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => kitStore.MoveDirectory("features/login", relativePath!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(relativePath));

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    public static TheoryData<string?> InvalidRelativePaths =>
    [
        null!,
        "",
        "   ",
        "/outside.md",
        ".",
        "features/..",
        "..",
        "../outside.md",
        "features/../../outside.md",
        "../.shadekit-backup/outside.md"
    ];

    private static KitStore CreateStore(string kitStorePath)
    {
        var formatRegistry = FormatRegistry.CreateDefault();

        return new KitStore(kitStorePath, new VersionedJsonSerializer(formatRegistry), formatRegistry);
    }
}
