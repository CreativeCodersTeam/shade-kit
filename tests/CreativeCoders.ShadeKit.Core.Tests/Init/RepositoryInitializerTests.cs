using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Templates;
using CreativeCoders.ShadeKit.Core.Tests.Store;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Init;

/// <summary>
/// Integration tests for <see cref="RepositoryInitializer" /> in a temporary git repository, with the real locator,
/// store factory, serializer, format registry, template reader, and writers. Only the engine version is faked.
/// </summary>
public class RepositoryInitializerTests
{
    private const string EngineVersion = "1.2.3";

    private const string GitIgnore = ".gitignore";

    private const string ClaudeMd = "CLAUDE.md";

    private const string AgentsMd = "AGENTS.md";

    private const string KitRoot = ".shadekit";

    private static readonly FormatRegistry Registry = FormatRegistry.CreateDefault();

    private static readonly string ExpectedSectionFile = ManagedInstructionSection.Text + "\n";

    public static TheoryData<string> InstructionFileNames { get; } = new() { ClaudeMd, AgentsMd };

    public static TheoryData<string> RepositoryFileNames { get; } = new() { GitIgnore, ClaudeMd, AgentsMd };

    [Fact]
    [Trait("Requirement", "FP02-AC1")]
    public async Task InitializeAsync_GitRepositoryWithoutKitRoot_CreatesKitRootContent()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var kitRootPath = Path.Combine(tempDirectory.RootPath, KitRoot);

        File.Exists(Path.Combine(kitRootPath, "config.json"))
            .Should().BeTrue();

        File.Exists(Path.Combine(kitRootPath, "gates.json"))
            .Should().BeTrue();

        File.Exists(Path.Combine(kitRootPath, "templates.lock.json"))
            .Should().BeTrue();

        GetRelativeFilePaths(tempDirectory.RootPath, Path.Combine(kitRootPath, "templates"))
            .Should().BeEquivalentTo(ShippedTemplates.All.Select(x => x.RepositoryPath));

        Directory.Exists(Path.Combine(kitRootPath, "features"))
            .Should().BeTrue();

        Directory.EnumerateFileSystemEntries(Path.Combine(kitRootPath, "features"))
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP02-AC1")]
    public async Task InitializeAsync_GitRepositoryWithoutKitRoot_ReturnsRepositoryRootAndFilesInWriteOrder()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        // Act
        var result = await CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.RepositoryRootPath
            .Should().Be(tempDirectory.RootPath);

        result.Files
            .Should().Equal(
            [
                Created(".shadekit/config.json"),
                Created(".shadekit/gates.json"),
                .. ShippedTemplates.All.Select(x => Created(x.RepositoryPath)),
                Created(".shadekit/templates.lock.json"),
                Created(".shadekit/features/"),
                Created(GitIgnore),
                Created(ClaudeMd),
                Created(AgentsMd)
            ]);
    }

    [Fact]
    public async Task InitializeAsync_ExistingRepositoryFiles_ReportsAppendedAndUpdatedFiles()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("bin/\n", GitIgnore);
        tempDirectory.WriteFile("# Project\n", ClaudeMd);
        tempDirectory.WriteFile("# Agents\n<!-- shade-kit:begin -->\nold\n<!-- shade-kit:end -->\n", AgentsMd);

        // Act
        var result = await CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.Files.TakeLast(3)
            .Should().Equal(
                new InitializedFile(GitIgnore, InitializedFileAction.Appended),
                new InitializedFile(ClaudeMd, InitializedFileAction.Appended),
                new InitializedFile(AgentsMd, InitializedFileAction.Updated));
    }

    [Fact]
    public async Task InitializeAsync_RepositoryFilesAlreadyComplete_ListsNoRepositoryFiles()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile(".shadekit/local.json\n.shadekit/features/current-feature.json\n", GitIgnore);
        tempDirectory.WriteFile(ExpectedSectionFile, ClaudeMd);
        tempDirectory.WriteFile(ExpectedSectionFile, AgentsMd);

        // Act
        var result = await CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.Files.Select(x => x.Path)
            .Should().NotContain([GitIgnore, ClaudeMd, AgentsMd]);

        result.Files[^1]
            .Should().Be(Created(".shadekit/features/"));
    }

    [Fact]
    [Trait("Requirement", "FP02-AC2")]
    public async Task InitializeAsync_GitRepositoryWithoutKitRoot_EveryJsonFileCarriesSchemaVersion()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var jsonFiles = Directory.GetFiles(Path.Combine(tempDirectory.RootPath, KitRoot), "*.json",
            SearchOption.AllDirectories);

        jsonFiles.Select(x => Path.GetFileName(x))
            .Should().BeEquivalentTo("config.json", "gates.json", "templates.lock.json");

        foreach (var jsonFile in jsonFiles)
        {
            var root = JsonNode.Parse(await File.ReadAllTextAsync(jsonFile, TestContext.Current.CancellationToken))!;

            root["schemaVersion"]!.GetValue<int>()
                .Should().Be(1, jsonFile);

            root["minEngineVersion"]!.GetValue<string>()
                .Should().NotBeNullOrWhiteSpace(jsonFile);
        }
    }

    [Fact]
    public async Task InitializeAsync_GitRepositoryWithoutKitRoot_WritesConfigWithInitContent()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(KitRoot, "config.json")
            .Should().Equal(Encoding.UTF8.GetBytes(
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                $"  \"minEngineVersion\": \"{GetMinEngineVersion(FormatIds.Config)}\",\n" +
                "  \"kitVersion\": \"1.2.3\",\n" +
                "  \"defaultProfile\": \"feature\",\n" +
                "  \"outdatedFormatOnRead\": \"migrate-and-warn\",\n" +
                "  \"trackers\": {\n" +
                "    \"github.com\": \"gh\"\n" +
                "  },\n" +
                "  \"report\": \"ask\"\n" +
                "}\n"));
    }

    [Fact]
    public async Task InitializeAsync_GitRepositoryWithoutKitRoot_WritesGatesWithVersionFieldsOnly()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(KitRoot, "gates.json")
            .Should().Equal(Encoding.UTF8.GetBytes(
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                $"  \"minEngineVersion\": \"{GetMinEngineVersion(FormatIds.Gates)}\"\n" +
                "}\n"));
    }

    [Fact]
    [Trait("Requirement", "FP02-AC3")]
    public async Task InitializeAsync_GitRepositoryWithoutKitRoot_LockHasOneEntryPerTemplateWithHashOfWrittenFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var root = JsonNode.Parse(tempDirectory.ReadFileText(KitRoot, "templates.lock.json"))!;
        var entries = root["templates"]!.AsArray().Select(x => x!.AsObject()).ToArray();

        entries.Select(x => x["path"]!.GetValue<string>())
            .Should().Equal(ShippedTemplates.All.Select(x => x.RepositoryPath).Order(StringComparer.Ordinal));

        foreach (var entry in entries)
        {
            var path = entry["path"]!.GetValue<string>();
            var writtenBytes = tempDirectory.ReadFileBytes(path.Split('/'));

            entry.Select(x => x.Key)
                .Should().Equal("path", "kitVersion", "hash");

            entry["kitVersion"]!.GetValue<string>()
                .Should().Be(EngineVersion, path);

            entry["hash"]!.GetValue<string>()
                .Should().Be("sha256:" + Convert.ToHexStringLower(SHA256.HashData(writtenBytes)), path)
                .And.MatchRegex("^sha256:[0-9a-f]{64}$");
        }
    }

    [Fact]
    [Trait("Requirement", "FP02-AC4")]
    public async Task InitializeAsync_NoGitIgnore_GitIgnoreContainsEachEntryExactlyOnce()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var lines = tempDirectory.ReadFileText(GitIgnore).Split('\n');

        foreach (var entry in KitGitIgnoreEntries.All)
        {
            lines.Count(x => x == entry)
                .Should().Be(1, entry);
        }
    }

    [Theory]
    [Trait("Requirement", "FP02-AC4")]
    [InlineData("bin/\n.shadekit/local.json\n.shadekit/features/current-feature.json\n")]
    [InlineData("/.shadekit/local.json\r\n/.shadekit/features/current-feature.json")]
    public async Task InitializeAsync_GitIgnoreHasEntries_LeavesGitIgnoreUnchanged(string gitIgnoreText)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile(gitIgnoreText, GitIgnore);
        var original = tempDirectory.ReadFileBytes(GitIgnore);

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(GitIgnore)
            .Should().Equal(original);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC4")]
    public async Task InitializeAsync_GitIgnoreHasOneEntry_AppendsOnlyTheMissingEntry()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("bin/\n/.shadekit/local.json\n", GitIgnore);

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(GitIgnore)
            .Should().Be("bin/\n/.shadekit/local.json\n\n# SHADE kit: local-only files\n" +
                         ".shadekit/features/current-feature.json\n");
    }

    [Theory]
    [Trait("Requirement", "FP02-AC5")]
    [MemberData(nameof(InstructionFileNames))]
    public async Task InitializeAsync_NoInstructionFile_CreatesFileWithOnlyTheSection(string fileName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(fileName)
            .Should().Equal(Encoding.UTF8.GetBytes(ExpectedSectionFile));
    }

    [Theory]
    [Trait("Requirement", "FP02-AC5")]
    [MemberData(nameof(InstructionFileNames))]
    public async Task InitializeAsync_InstructionFileWithContent_AppendsExactlyOneSectionAndKeepsContent(
        string fileName)
    {
        // Arrange
        const string existingText = "# Project rules\n\nUse tabs.";
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile(existingText, fileName);

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var text = tempDirectory.ReadFileText(fileName);

        text
            .Should().Be(existingText + "\n\n" + ExpectedSectionFile);

        CountLinesStartingWith(text, ManagedInstructionSection.BeginMarkerPrefix)
            .Should().Be(1);

        CountLinesStartingWith(text, ManagedInstructionSection.EndMarker)
            .Should().Be(1);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public async Task InitializeAsync_AgentsMdIsSymlinkToClaudeMd_AppendsExactlyOneSectionAndKeepsSymlink()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("# Project\n", ClaudeMd);
        var agentsMdPath = tempDirectory.CreateSymbolicLinkOrSkip(ClaudeMd, AgentsMd);

        // Act
        var result = await CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(ClaudeMd)
            .Should().Be("# Project\n\n" + ExpectedSectionFile);

        new FileInfo(agentsMdPath).LinkTarget
            .Should().Be(ClaudeMd);

        result.Files.TakeLast(2)
            .Should().Equal(Created(GitIgnore), new InitializedFile(ClaudeMd, InitializedFileAction.Appended));
    }

    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public async Task InitializeAsync_AgentsMdIsSymlinkToClaudeMdWithOutdatedSection_UpdatesSectionOnceAndKeepsSymlink()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("# Project\n<!-- shade-kit:begin -->\nold\n<!-- shade-kit:end -->\nrest\n", ClaudeMd);
        var agentsMdPath = tempDirectory.CreateSymbolicLinkOrSkip(ClaudeMd, AgentsMd);

        // Act
        var result = await CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(ClaudeMd)
            .Should().Be("# Project\n" + ExpectedSectionFile + "rest\n");

        new FileInfo(agentsMdPath).LinkTarget
            .Should().Be(ClaudeMd);

        result.Files.TakeLast(2)
            .Should().Equal(Created(GitIgnore), new InitializedFile(ClaudeMd, InitializedFileAction.Updated));
    }

    [Theory]
    [Trait("Requirement", "FP02-AC5")]
    [MemberData(nameof(InstructionFileNames))]
    public async Task InitializeAsync_InstructionFileWithOutdatedSection_ReplacesSectionAndKeepsOtherContent(
        string fileName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("# Before\n<!-- shade-kit:begin -->\nold\n<!-- shade-kit:end -->\n# After\n",
            fileName);

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(fileName)
            .Should().Be("# Before\n" + ExpectedSectionFile + "# After\n");
    }

    [Fact]
    [Trait("Requirement", "FP02-AC6")]
    public async Task InitializeAsync_AlreadyInitialised_ThrowsKitAlreadyInitializedExceptionAndChangesNoFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var initializer = CreateInitializer();
        await initializer.InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);
        var snapshot = TakeSnapshot(tempDirectory.RootPath);

        // Act
        var act = () => initializer.InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<KitAlreadyInitializedException>()).Which;

        exception.KitRootPath
            .Should().Be(Path.Combine(tempDirectory.RootPath, KitRoot));

        exception.Message
            .Should().Contain("shade-kit upgrade");

        TakeSnapshot(tempDirectory.RootPath)
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [Trait("Requirement", "FP02-AC6")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InitializeAsync_KitRootIsFileOrEmptyDirectory_ThrowsKitAlreadyInitializedException(
        bool kitRootIsFile)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        if (kitRootIsFile)
        {
            tempDirectory.WriteFile("not a directory", KitRoot);
        }
        else
        {
            tempDirectory.CreateDirectory(KitRoot);
        }

        var snapshot = TakeSnapshot(tempDirectory.RootPath);

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<KitAlreadyInitializedException>())
            .Which.KitRootPath.Should().Be(Path.Combine(tempDirectory.RootPath, KitRoot));

        TakeSnapshot(tempDirectory.RootPath)
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC7")]
    public async Task InitializeAsync_NotInsideGitRepository_ThrowsNotAGitRepositoryExceptionAndCreatesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.SkipWhenGitRootExistsAbove();

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<NotAGitRepositoryException>())
            .Which.StartPath.Should().Be(tempDirectory.RootPath);

        Directory.EnumerateFileSystemEntries(tempDirectory.RootPath)
            .Should().BeEmpty();
    }

    [Theory]
    [Trait("Requirement", "FP02-AC8")]
    [InlineData("spec.template.md", ".shadekit/templates/spec.md")]
    [InlineData("checks/definition-of-ready.template.md", ".shadekit/templates/checks/definition-of-ready.md")]
    public async Task InitializeAsync_ShippedTemplate_WritesReaderContentToRepositoryPath(string resourcePath,
        string repositoryPath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var shippedTemplate = ShippedTemplates.All.Single(x => x.ResourcePath == resourcePath);

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(repositoryPath.Split('/'))
            .Should().Be(new ShippedTemplateReader().ReadContent(shippedTemplate));
    }

    [Fact]
    [Trait("Requirement", "FP02-AC8")]
    public async Task InitializeAsync_EveryShippedTemplate_WritesReaderContentAsUtf8WithoutBomAndCarriageReturn()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var reader = new ShippedTemplateReader();

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        foreach (var shippedTemplate in ShippedTemplates.All)
        {
            var writtenBytes = tempDirectory.ReadFileBytes(shippedTemplate.RepositoryPath.Split('/'));

            writtenBytes
                .Should().Equal(new UTF8Encoding(false).GetBytes(reader.ReadContent(shippedTemplate)));

            writtenBytes.Take(3)
                .Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, shippedTemplate.RepositoryPath);

            writtenBytes
                .Should().NotContain((byte)'\r', shippedTemplate.RepositoryPath);
        }
    }

    [Fact]
    public async Task InitializeAsync_PathIsSubdirectory_InitialisesAtRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var subdirectoryPath = tempDirectory.CreateDirectory("src", "app");

        // Act
        var result = await CreateInitializer()
            .InitializeAsync(subdirectoryPath, TestContext.Current.CancellationToken);

        // Assert
        result.RepositoryRootPath
            .Should().Be(tempDirectory.RootPath);

        Directory.Exists(Path.Combine(tempDirectory.RootPath, KitRoot))
            .Should().BeTrue();

        File.Exists(Path.Combine(tempDirectory.RootPath, GitIgnore))
            .Should().BeTrue();

        Directory.EnumerateFileSystemEntries(subdirectoryPath)
            .Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InstructionFileNames))]
    public async Task InitializeAsync_InvalidMarkersInInstructionFile_ThrowsAndCreatesOrChangesNothing(
        string invalidFileName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("# Valid file\n", ClaudeMd);
        tempDirectory.WriteFile("# Valid file\n", AgentsMd);
        tempDirectory.WriteFile("# Broken\n<!-- shade-kit:begin -->\nno end marker\n", invalidFileName);
        var snapshot = TakeSnapshot(tempDirectory.RootPath);

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidManagedSectionException>())
            .Which.FilePath.Should().Be(Path.Combine(tempDirectory.RootPath, invalidFileName));

        TakeSnapshot(tempDirectory.RootPath)
            .Should().BeEquivalentTo(snapshot);

        Path.Exists(Path.Combine(tempDirectory.RootPath, KitRoot))
            .Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(RepositoryFileNames))]
    public async Task InitializeAsync_Utf16RepositoryFile_ThrowsUnsupportedFileEncodingExceptionAndWritesNothing(
        string utf16FileName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("# Valid file\n", ClaudeMd);
        tempDirectory.WriteFile("# Valid file\n", AgentsMd);
        tempDirectory.WriteFileBytes([0xFF, 0xFE, .. Encoding.Unicode.GetBytes("text\r\n")], utf16FileName);
        var snapshot = TakeSnapshot(tempDirectory.RootPath);

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UnsupportedFileEncodingException>()).Which;

        exception.FilePath
            .Should().Be(Path.Combine(tempDirectory.RootPath, utf16FileName));

        exception.EncodingName
            .Should().Be("UTF-16 LE");

        TakeSnapshot(tempDirectory.RootPath)
            .Should().BeEquivalentTo(snapshot);

        Path.Exists(Path.Combine(tempDirectory.RootPath, KitRoot))
            .Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(RepositoryFileNames))]
    public async Task InitializeAsync_Utf8RepositoryFileWithBom_ChangesFileAndKeepsBom(string fileName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        byte[] original = [0xEF, 0xBB, 0xBF, .. "# Existing\n"u8];
        tempDirectory.WriteFileBytes(original, fileName);

        // Act
        var result = await CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.Files
            .Should().ContainEquivalentOf(new InitializedFile(fileName, InitializedFileAction.Appended));

        var bytes = tempDirectory.ReadFileBytes(fileName);

        bytes.Take(original.Length)
            .Should().Equal(original);

        bytes.Length
            .Should().BeGreaterThan(original.Length);
    }

    [Theory]
    [MemberData(nameof(RepositoryFileNames))]
    public async Task InitializeAsync_RepositoryFileIsDirectory_ThrowsPathNotAFileExceptionAndWritesNothing(
        string directoryName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(directoryName);
        var snapshot = TakeSnapshot(tempDirectory.RootPath);

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PathNotAFileException>())
            .Which.Path.Should().Be(Path.Combine(tempDirectory.RootPath, directoryName));

        TakeSnapshot(tempDirectory.RootPath)
            .Should().BeEquivalentTo(snapshot);

        Path.Exists(Path.Combine(tempDirectory.RootPath, KitRoot))
            .Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_GitRepositoryWithoutKitRoot_StoreReadsWrittenFilesWithoutWarnings()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await CreateInitializer().InitializeAsync(tempDirectory.RootPath, cancellationToken);

        // Assert
        var kitStore = CreateKitStoreFactory().Create(tempDirectory.RootPath);

        var config = await kitStore.ReadJsonAsync<KitConfig>(KitStorePaths.ConfigFileName, FormatIds.Config,
            FormatAccess.Read, cancellationToken);
        var gates = await kitStore.ReadJsonAsync<GatesConfig>(KitStorePaths.GatesFileName, FormatIds.Gates,
            FormatAccess.Read, cancellationToken);
        var templatesLock = await kitStore.ReadJsonAsync<TemplatesLock>(KitStorePaths.TemplatesLockFileName,
            FormatIds.TemplatesLock, FormatAccess.Read, cancellationToken);

        config!.Warnings
            .Should().BeEmpty();

        config.Document.KitVersion
            .Should().Be(EngineVersion);

        config.Document.Trackers
            .Should().Equal(new Dictionary<string, string> { ["github.com"] = "gh" });

        config.Document.CoverageThreshold
            .Should().BeNull();

        gates!.Warnings
            .Should().BeEmpty();

        templatesLock!.Warnings
            .Should().BeEmpty();

        templatesLock.Document.Templates
            .Should().HaveCount(ShippedTemplates.All.Count);

        kitStore.GetFeatureNames()
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InitializeAsync_NullEmptyOrWhitespacePath_ThrowsArgumentException(string? path)
    {
        // Arrange
        var initializer = CreateInitializer();

        // Act
        var act = () => initializer.InitializeAsync(path!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName(nameof(path));
    }

    [Fact]
    public async Task InitializeAsync_CanceledToken_ThrowsOperationCanceledExceptionAndCreatesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var snapshot = TakeSnapshot(tempDirectory.RootPath);

        // Act
        var act = () => CreateInitializer().InitializeAsync(tempDirectory.RootPath, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        TakeSnapshot(tempDirectory.RootPath)
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task InitializeAsync_TokenCanceledDuringPreconditions_ThrowsOperationCanceledExceptionAndWritesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        using var cancellationTokenSource = new CancellationTokenSource();
        var snapshot = TakeSnapshot(tempDirectory.RootPath);

        // Every validation passes and cancels the token without observing it, so the preconditions complete and
        // the cancellation must stop the initialisation before its first write, whichever validation runs last.
        var gitIgnoreWriter = A.Fake<IGitIgnoreWriter>(x => x.Wrapping(new GitIgnoreWriter()));
        A.CallTo(() => gitIgnoreWriter.ValidateAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Returns(Task.CompletedTask);

        var instructionSectionWriter =
            A.Fake<IInstructionSectionWriter>(x => x.Wrapping(new InstructionSectionWriter()));
        A.CallTo(() => instructionSectionWriter.ValidateAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Returns(Task.CompletedTask);

        var initializer = CreateInitializer(gitIgnoreWriter, instructionSectionWriter);

        // Act
        var act = () => initializer.InitializeAsync(tempDirectory.RootPath, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        Path.Exists(Path.Combine(tempDirectory.RootPath, KitRoot))
            .Should().BeFalse();

        TakeSnapshot(tempDirectory.RootPath)
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task InitializeAsync_LastWriteFails_KeepsFilesWrittenBefore()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        var agentsPath = Path.Combine(tempDirectory.RootPath, AgentsMd);

        // The real writer validates and writes CLAUDE.md; only the write of AGENTS.md, the last write, fails.
        var instructionSectionWriter =
            A.Fake<IInstructionSectionWriter>(x => x.Wrapping(new InstructionSectionWriter()));
        A.CallTo(() => instructionSectionWriter.ApplyAsync(agentsPath, A<CancellationToken>._))
            .ThrowsAsync(new IOException("The disk is full."));

        var initializer = CreateInitializer(new GitIgnoreWriter(), instructionSectionWriter);

        // Act
        var act = () => initializer.InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>();

        File.Exists(Path.Combine(tempDirectory.RootPath, KitRoot, "templates.lock.json"))
            .Should().BeTrue();

        File.Exists(Path.Combine(tempDirectory.RootPath, GitIgnore))
            .Should().BeTrue();

        tempDirectory.ReadFileText(ClaudeMd)
            .Should().Be(ExpectedSectionFile);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Constructor_NullDependency_ThrowsArgumentNullException(int nullIndex)
    {
        // Arrange
        object?[] dependencies =
        [
            A.Fake<IGitRepositoryLocator>(),
            A.Fake<IKitStoreFactory>(),
            A.Fake<IShippedTemplateReader>(),
            A.Fake<IGitIgnoreWriter>(),
            A.Fake<IInstructionSectionWriter>(),
            A.Fake<IEngineVersionProvider>()
        ];
        dependencies[nullIndex] = null;

        // Act
        var act = () => new RepositoryInitializer(
            (IGitRepositoryLocator)dependencies[0]!,
            (IKitStoreFactory)dependencies[1]!,
            (IShippedTemplateReader)dependencies[2]!,
            (IGitIgnoreWriter)dependencies[3]!,
            (IInstructionSectionWriter)dependencies[4]!,
            (IEngineVersionProvider)dependencies[5]!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static RepositoryInitializer CreateInitializer()
    {
        return CreateInitializer(new GitIgnoreWriter(), new InstructionSectionWriter());
    }

    private static RepositoryInitializer CreateInitializer(IGitIgnoreWriter gitIgnoreWriter,
        IInstructionSectionWriter instructionSectionWriter)
    {
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).Returns(EngineVersion);

        return new RepositoryInitializer(new GitRepositoryLocator(), CreateKitStoreFactory(),
            new ShippedTemplateReader(), gitIgnoreWriter, instructionSectionWriter, engineVersionProvider);
    }

    private static KitStoreFactory CreateKitStoreFactory()
    {
        return new KitStoreFactory(new GitRepositoryLocator(), new VersionedJsonSerializer(Registry), Registry);
    }

    private static string GetMinEngineVersion(string formatId)
    {
        var format = Registry.GetFormat(formatId);

        return format.GetMinEngineVersion(format.CurrentVersion);
    }

    private static InitializedFile Created(string path)
    {
        return new InitializedFile(path, InitializedFileAction.Created);
    }

    private static int CountLinesStartingWith(string text, string prefix)
    {
        return text.Split('\n').Count(x => x.Trim().StartsWith(prefix, StringComparison.Ordinal));
    }

    private static IEnumerable<string> GetRelativeFilePaths(string rootPath, string directoryPath)
    {
        return Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Select(x => Path.GetRelativePath(rootPath, x).Replace(Path.DirectorySeparatorChar, '/'));
    }

    /// <summary>
    /// Captures every directory and the bytes of every file below a root directory, keyed by relative path.
    /// </summary>
    private static Dictionary<string, string> TakeSnapshot(string rootPath)
    {
        return Directory.EnumerateFileSystemEntries(rootPath, "*", SearchOption.AllDirectories)
            .ToDictionary(
                x => Path.GetRelativePath(rootPath, x),
                x => Directory.Exists(x) ? "<directory>" : Convert.ToBase64String(File.ReadAllBytes(x)));
    }
}
