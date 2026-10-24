using CreativeCoders.ShadeKit.Core.Tests.Store;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Templates;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using FakeItEasy.Configuration;

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

    [Theory]
    [InlineData(LineEnding.Lf, "\n")]
    [InlineData(LineEnding.CrLf, "\r\n")]
    public async Task InitializeAsync_WriterLineEnding_WritesWholeRepositoryFilesWithIt(LineEnding lineEnding,
        string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("bin/\r\nobj/\n", GitIgnore);
        tempDirectory.WriteFile("# Project\r\n", ClaudeMd);

        // Act
        await CreateInitializer(FixedLineEnding.CreateWriter(lineEnding))
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(GitIgnore)
            .Should().Be(string.Join(newLine, "bin/", "obj/", "", "# SHADE kit: local-only files",
                ".shadekit/local.json", ".shadekit/features/current-feature.json", ""));

        tempDirectory.ReadFileText(ClaudeMd)
            .Should().Be($"# Project{newLine}{newLine}" +
                         ExpectedSectionFile.Replace("\n", newLine, StringComparison.Ordinal));

        tempDirectory.ReadFileText(AgentsMd)
            .Should().Be(ExpectedSectionFile.Replace("\n", newLine, StringComparison.Ordinal));
    }

    [Fact]
    public async Task InitializeAsync_RepositoryFiles_WritesEachThroughWriterWithRootAndFileName()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var textFileWriter = A.Fake<ITextFileWriter>();
        CallToWrite(textFileWriter)
            .Returns(Warnings());

        // Act
        await CreateInitializer(textFileWriter)
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        foreach (var fileName in new[] { GitIgnore, ClaudeMd, AgentsMd })
        {
            A.CallTo(() => textFileWriter.WriteAsync(tempDirectory.RootPath, fileName, A<TextFileContent>._,
                    A<string?>._, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }
    }

    [Fact]
    public async Task InitializeAsync_NewRepository_PreparesLineEndingsOfEveryWrittenFileOnceBeforeTheFirstWrite()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var lineEndingResolver = A.Fake<ILineEndingResolver>();
        var textFileWriter =
            A.Fake<ITextFileWriter>(options => options.Wrapping(FixedLineEnding.CreateWriter(LineEnding.Lf)));
        var kitStoreTextFileWriter =
            A.Fake<ITextFileWriter>(options => options.Wrapping(FixedLineEnding.CreateWriter(LineEnding.Lf)));
        var initializer = CreateInitializer(new ShippedTemplateReader(), new GitIgnorePlanner(),
            new InstructionSectionPlanner(), textFileWriter, kitStoreTextFileWriter, lineEndingResolver);
        string[] expectedPaths =
        [
            ".shadekit/config.json",
            ".shadekit/gates.json",
            .. ShippedTemplates.All.Select(x => x.RepositoryPath),
            ".shadekit/templates.lock.json",
            GitIgnore,
            ClaudeMd,
            AgentsMd
        ];

        // Act
        await initializer.InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => lineEndingResolver.PrepareAsync(tempDirectory.RootPath,
                A<IReadOnlyCollection<string>>.That.IsSameSequenceAs(expectedPaths),
                TestContext.Current.CancellationToken))
            .MustHaveHappenedOnceExactly()
            .Then(CallToWrite(kitStoreTextFileWriter)
                .MustHaveHappened());

        CallToWrite(textFileWriter)
            .MustHaveHappened(3, Times.Exactly);
    }

    [Fact]
    public async Task InitializeAsync_WriterReturnsWarnings_ReturnsEachDistinctWarningOnceInFirstOccurrenceOrder()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var rootWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        var otherWarning = new EditorConfigUnreadableWarning("/repo/docs/.editorconfig", "Access denied.");

        var textFileWriter = A.Fake<ITextFileWriter>();
        CallToWrite(textFileWriter, GitIgnore)
            .Returns(Warnings(rootWarning));
        CallToWrite(textFileWriter, ClaudeMd)
            .Returns(Warnings(new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied."), otherWarning));
        CallToWrite(textFileWriter, AgentsMd)
            .Returns(Warnings(otherWarning, rootWarning));

        // Act
        var result = await CreateInitializer(textFileWriter)
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(rootWarning, otherWarning);
    }

    [Fact]
    public async Task InitializeAsync_SameFileWithDifferentReasons_KeepsBothWarnings()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var firstWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        var secondWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "The file is locked.");

        var textFileWriter = A.Fake<ITextFileWriter>();
        CallToWrite(textFileWriter)
            .ReturnsNextFromSequence(
                Warnings(firstWarning),
                Warnings(secondWarning),
                Warnings());

        // Act
        var result = await CreateInitializer(textFileWriter)
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(firstWarning, secondWarning);
    }

    [Fact]
    public async Task InitializeAsync_WriterReturnsNoWarnings_ReturnsNoWarnings()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        // Act
        var result = await CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_RepositoryFilesAlreadyComplete_DoesNotCallWriter()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile(".shadekit/local.json\r\n.shadekit/features/current-feature.json\r\n", GitIgnore);
        tempDirectory.WriteFile(ExpectedSectionFile.Replace("\n", "\r\n", StringComparison.Ordinal), ClaudeMd);
        tempDirectory.WriteFile(ExpectedSectionFile, AgentsMd);
        var textFileWriter = A.Fake<ITextFileWriter>();

        // Act
        var result = await CreateInitializer(textFileWriter)
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(textFileWriter)
            .MustNotHaveHappened();

        result.Warnings
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(ClaudeMd)]
    [InlineData(AgentsMd)]
    public async Task InitializeAsync_WriterThrowsForInstructionFile_RethrowsRemovesKitRootAndKeepsEarlierFiles(
        string failingFileName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var realWriter = FixedLineEnding.CreateWriter(LineEnding.Lf,
            new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied."));

        var textFileWriter = A.Fake<ITextFileWriter>();
        CallToWrite(textFileWriter)
            .ReturnsLazily((string root, string relativePath, TextFileContent content, string? lineEndingPath,
                CancellationToken token) => realWriter.WriteAsync(root, relativePath, content, lineEndingPath, token));
        CallToWrite(textFileWriter, failingFileName)
            .ThrowsAsync(new IOException("disk full"));

        // Act
        var act = () => CreateInitializer(textFileWriter)
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .WithMessage("disk full");

        Path.Exists(Path.Combine(tempDirectory.RootPath, KitRoot))
            .Should().BeFalse();

        File.Exists(Path.Combine(tempDirectory.RootPath, GitIgnore))
            .Should().BeTrue();

        File.Exists(Path.Combine(tempDirectory.RootPath, failingFileName))
            .Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WriterCanceled_RethrowsAndRemovesKitRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var textFileWriter = A.Fake<ITextFileWriter>();
        CallToWrite(textFileWriter)
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = () => CreateInitializer(textFileWriter)
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        Path.Exists(Path.Combine(tempDirectory.RootPath, KitRoot))
            .Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_Token_IsPassedToWriter()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        using var cancellationTokenSource = new CancellationTokenSource();
        var textFileWriter = A.Fake<ITextFileWriter>();
        CallToWrite(textFileWriter)
            .Returns(Warnings());

        // Act
        await CreateInitializer(textFileWriter).InitializeAsync(tempDirectory.RootPath, cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => textFileWriter.WriteAsync(A<string>._, A<string>._, A<TextFileContent>._,
                A<string?>._, cancellationTokenSource.Token))
            .MustHaveHappened(3, Times.Exactly);
    }

    [Fact]
    public async Task InitializeAsync_RepositoryFiles_PassesPlannedContentOfEachFileToWriter()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("bin/\n", GitIgnore);
        tempDirectory.WriteFile("# Agents\n", AgentsMd);
        var writtenContents = new Dictionary<string, TextFileContent>();
        var textFileWriter = A.Fake<ITextFileWriter>();
        CallToWrite(textFileWriter)
            .Invokes((string _, string relativePath, TextFileContent content, string? _, CancellationToken _) =>
                writtenContents[relativePath] = content)
            .Returns(Warnings());

        // Act
        await CreateInitializer(textFileWriter)
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var sectionLines = ManagedInstructionSection.Text.Split('\n');
        string[] expectedGitIgnoreLines = ["bin/", "", "# SHADE kit: local-only files", .. KitGitIgnoreEntries.All];
        string[] expectedAgentsLines = ["# Agents", "", .. sectionLines];

        writtenContents[GitIgnore].Lines
            .Should().Equal(expectedGitIgnoreLines);

        writtenContents[ClaudeMd].Lines
            .Should().Equal(sectionLines);

        writtenContents[AgentsMd].Lines
            .Should().Equal(expectedAgentsLines);
    }

    [Fact]
    public async Task InitializeAsync_OnlyOneInstructionFileNeedsChange_WritesOnlyItAndReturnsOnlyItsWarnings()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile(".shadekit/local.json\n.shadekit/features/current-feature.json\n", GitIgnore);
        tempDirectory.WriteFile(ExpectedSectionFile, ClaudeMd);
        var warning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        var textFileWriter = A.Fake<ITextFileWriter>();
        CallToWrite(textFileWriter)
            .Returns(Warnings(warning));

        // Act
        var result = await CreateInitializer(textFileWriter)
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        CallToWrite(textFileWriter)
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => textFileWriter.WriteAsync(tempDirectory.RootPath, AgentsMd, A<TextFileContent>._,
                A<string?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        result.Warnings
            .Should().Equal(warning);
    }

    [Fact]
    public async Task InitializeAsync_OneWriteReturnsSameWarningTwice_ReturnsItOnce()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var warning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        var textFileWriter = A.Fake<ITextFileWriter>();
        CallToWrite(textFileWriter)
            .ReturnsNextFromSequence(
                Warnings(warning, warning),
                Warnings(),
                Warnings());

        // Act
        var result = await CreateInitializer(textFileWriter)
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(warning);
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
            var root = JsonNode.Parse(await File.ReadAllTextAsync(jsonFile, TestContext.Current.CancellationToken));

            root
                .Should().NotBeNull(jsonFile);

            var schemaVersionNode = root["schemaVersion"];

            schemaVersionNode
                .Should().NotBeNull(jsonFile);

            schemaVersionNode.GetValue<int>()
                .Should().Be(1, jsonFile);

            var minEngineVersionNode = root["minEngineVersion"];

            minEngineVersionNode
                .Should().NotBeNull(jsonFile);

            minEngineVersionNode.GetValue<string>()
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
        var root = JsonNode.Parse(tempDirectory.ReadFileText(KitRoot, "templates.lock.json"));

        root
            .Should().NotBeNull();

        var templatesNode = root["templates"];

        templatesNode
            .Should().NotBeNull();

        var entries = templatesNode.AsArray().Select(x => x!.AsObject()).ToArray();

        entries.Select(x => x["path"]!.GetValue<string>())
            .Should().Equal(ShippedTemplates.All.Select(x => x.RepositoryPath).Order(StringComparer.Ordinal));

        foreach (var entry in entries)
        {
            var pathNode = entry["path"];

            pathNode
                .Should().NotBeNull();

            var path = pathNode.GetValue<string>();
            entry.Select(x => x.Key)
                .Should().Equal("path", "kitVersion", "hash");

            var kitVersionNode = entry["kitVersion"];

            kitVersionNode
                .Should().NotBeNull();

            kitVersionNode.GetValue<string>()
                .Should().Be(EngineVersion, path);

            var hashNode = entry["hash"];

            hashNode
                .Should().NotBeNull();

            var writtenContent = await TextFileContent.ReadAsync(Path.Combine([tempDirectory.RootPath, .. path.Split('/')]),
                TestContext.Current.CancellationToken);

            hashNode.GetValue<string>()
                .Should().Be(TemplatesLockEntry.ComputeHash(writtenContent!), path)
                .And.MatchRegex("^sha256:[0-9a-f]{64}$");
        }
    }

    [Theory]
    [InlineData(LineEnding.Lf, "\n")]
    [InlineData(LineEnding.CrLf, "\r\n")]
    public async Task InitializeAsync_KitStoreLineEnding_WritesTemplatesAndJsonFilesWithIt(LineEnding lineEnding,
        string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var reader = new ShippedTemplateReader();

        // Act
        await CreateInitializer(new ShippedTemplateReader(), new GitIgnorePlanner(), new InstructionSectionPlanner(),
                kitStoreTextFileWriter: FixedLineEnding.CreateWriter(lineEnding))
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        foreach (var shippedTemplate in ShippedTemplates.All)
        {
            tempDirectory.ReadFileText(shippedTemplate.RepositoryPath.Split('/'))
                .Should().Be(reader.ReadContent(shippedTemplate).Replace("\n", newLine, StringComparison.Ordinal),
                    shippedTemplate.RepositoryPath);
        }

        foreach (var fileName in new[] { "config.json", "gates.json", "templates.lock.json" })
        {
            var json = tempDirectory.ReadFileText(KitRoot, fileName);

            json
                .Should().Be(json.ReplaceLineEndings(newLine), fileName)
                .And.EndWith("}" + newLine, fileName);
        }
    }

    [Fact]
    public async Task InitializeAsync_KitStoreLineEndingCrLf_LockHashesEqualThoseOfLf()
    {
        // Arrange
        using var lfDirectory = new TempKitDirectory();
        lfDirectory.CreateGitRoot();
        using var crLfDirectory = new TempKitDirectory();
        crLfDirectory.CreateGitRoot();
        await CreateInitializer(new ShippedTemplateReader(), new GitIgnorePlanner(), new InstructionSectionPlanner(),
                kitStoreTextFileWriter: FixedLineEnding.CreateWriter(LineEnding.Lf))
            .InitializeAsync(lfDirectory.RootPath, TestContext.Current.CancellationToken);

        // Act
        await CreateInitializer(new ShippedTemplateReader(), new GitIgnorePlanner(), new InstructionSectionPlanner(),
                kitStoreTextFileWriter: FixedLineEnding.CreateWriter(LineEnding.CrLf))
            .InitializeAsync(crLfDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var reader = new ShippedTemplateReader();
        var expectedHashes = ShippedTemplates.All
            .OrderBy(x => x.RepositoryPath, StringComparer.Ordinal)
            .Select(x => TemplatesLockEntry.ComputeHash(TextFileContent.FromText(reader.ReadContent(x))));

        ReadLockHashes(crLfDirectory)
            .Should().Equal(ReadLockHashes(lfDirectory))
            .And.Equal(expectedHashes);
    }

    [Fact]
    public async Task InitializeAsync_KitStoreAndRepositoryWritesReturnWarnings_ReturnsStoreWarningsFirstEachOnce()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var kitWarning = new EditorConfigUnreadableWarning("/repo/.shadekit/.editorconfig", "Access denied.");
        var rootWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");

        // Act
        var result = await CreateInitializer(new ShippedTemplateReader(), new GitIgnorePlanner(),
                new InstructionSectionPlanner(), FixedLineEnding.CreateWriter(LineEnding.Lf, rootWarning, kitWarning),
                FixedLineEnding.CreateWriter(LineEnding.Lf, kitWarning))
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(kitWarning, rootWarning);
    }

    [Fact]
    public async Task InitializeAsync_RepositoryFilesCompleteAndKitStoreWarnings_ReturnsKitStoreWarnings()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile(".shadekit/local.json\n.shadekit/features/current-feature.json\n", GitIgnore);
        tempDirectory.WriteFile(ExpectedSectionFile, ClaudeMd);
        tempDirectory.WriteFile(ExpectedSectionFile, AgentsMd);
        var kitWarning = new EditorConfigUnreadableWarning("/repo/.shadekit/.editorconfig", "Access denied.");
        var textFileWriter = A.Fake<ITextFileWriter>();

        // Act
        var result = await CreateInitializer(new ShippedTemplateReader(), new GitIgnorePlanner(),
                new InstructionSectionPlanner(), textFileWriter, FixedLineEnding.CreateWriter(LineEnding.Lf, kitWarning))
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(textFileWriter)
            .MustNotHaveHappened();

        result.Warnings
            .Should().Equal(kitWarning);
    }

    [Fact]
    public async Task InitializeAsync_KitStoreUsedBeforeWithWarnings_ReturnsNoWarningOfEarlierWrite()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile(".shadekit/local.json\n.shadekit/features/current-feature.json\n", GitIgnore);
        tempDirectory.WriteFile(ExpectedSectionFile, ClaudeMd);
        tempDirectory.WriteFile(ExpectedSectionFile, AgentsMd);
        var earlierWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        var kitStore = TestKitStores.Create(Path.Combine(tempDirectory.RootPath, KitRoot),
            FixedLineEnding.CreateWriterWithWarningsOnFirstWrite(LineEnding.Lf, earlierWarning), Registry);
        var earlierWarnings = await kitStore.WriteFileAsync("earlier.md", TextFileContent.FromText("a\n"),
            cancellationToken: TestContext.Current.CancellationToken);
        Directory.Delete(kitStore.RootPath, recursive: true);
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.CreateForRepositoryRoot(A<string>._))
            .Returns(kitStore);
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).Returns(EngineVersion);
        var initializer = new RepositoryInitializer(new GitRepositoryLocator(), kitStoreFactory,
            new ShippedTemplateReader(), new GitIgnorePlanner(), new InstructionSectionPlanner(),
            engineVersionProvider, FixedLineEnding.CreateWriter(LineEnding.Lf), A.Fake<ILineEndingResolver>());

        // Act
        var result = await initializer.InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        earlierWarnings
            .Should().Equal(earlierWarning);

        result.Warnings
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(LineEnding.Lf, "\n")]
    [InlineData(LineEnding.CrLf, "\r\n")]
    public async Task InitializeAsync_ReaderContentWithMixedLineEndings_WritesResolvedLineEndingAndHashOfLfForm(
        LineEnding lineEnding, string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var reader = A.Fake<IShippedTemplateReader>();
        A.CallTo(() => reader.ReadContent(A<ShippedTemplate>._)).Returns("# Title\r\n\nText\r\n");

        // Act
        await CreateInitializer(reader, new GitIgnorePlanner(), new InstructionSectionPlanner(),
                kitStoreTextFileWriter: FixedLineEnding.CreateWriter(lineEnding))
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var specPath = ShippedTemplates.All.Single(x => x.ResourcePath == "spec.template.md").RepositoryPath;

        tempDirectory.ReadFileText(specPath.Split('/'))
            .Should().Be($"# Title{newLine}{newLine}Text{newLine}");

        ReadLockHashes(tempDirectory)
            .Should().AllBe(TemplatesLockEntry.ComputeHash(TextFileContent.FromText("# Title\n\nText\n")));
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

        TestText.CountLinesStartingWith(text, ManagedInstructionSection.BeginMarkerPrefix)
            .Should().Be(1);

        TestText.CountLinesStartingWith(text, ManagedInstructionSection.EndMarker)
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
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => initializer.InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<KitAlreadyInitializedException>()).Which;

        exception.KitRootPath
            .Should().Be(Path.Combine(tempDirectory.RootPath, KitRoot));

        exception.Message
            .Should().Contain(exception.KitRootPath)
            .And.NotContain("shade-kit upgrade");

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC6")]
    public async Task InitializeAsync_KitRootIsEmptyDirectory_ThrowsKitAlreadyInitializedException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(KitRoot);

        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<KitAlreadyInitializedException>())
            .Which.KitRootPath.Should().Be(Path.Combine(tempDirectory.RootPath, KitRoot));

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task InitializeAsync_KitRootIsFile_ThrowsKitRootNotADirectoryExceptionAndChangesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("not a directory", KitRoot);

        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<KitRootNotADirectoryException>())
            .Which.KitRootPath.Should().Be(Path.Combine(tempDirectory.RootPath, KitRoot));

        tempDirectory.TakeSnapshot()
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
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidManagedSectionException>())
            .Which.FilePath.Should().Be(Path.Combine(tempDirectory.RootPath, invalidFileName));

        tempDirectory.TakeSnapshot()
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
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UnsupportedFileEncodingException>()).Which;

        exception.FilePath
            .Should().Be(Path.Combine(tempDirectory.RootPath, utf16FileName));

        exception.EncodingName
            .Should().Be("UTF-16 LE");

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        Path.Exists(Path.Combine(tempDirectory.RootPath, KitRoot))
            .Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(RepositoryFileNames))]
    public async Task InitializeAsync_InvalidUtf8RepositoryFile_ThrowsUnsupportedFileEncodingExceptionAndWritesNothing(
        string invalidFileName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.WriteFile("# Valid file\n", ClaudeMd);
        tempDirectory.WriteFile("# Valid file\n", AgentsMd);
        tempDirectory.WriteFileBytes([.. Encoding.Latin1.GetBytes("# Grüße\n")], invalidFileName);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UnsupportedFileEncodingException>())
            .Which.FilePath.Should().Be(Path.Combine(tempDirectory.RootPath, invalidFileName));

        tempDirectory.TakeSnapshot()
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
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => CreateInitializer()
            .InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PathNotAFileException>())
            .Which.Path.Should().Be(Path.Combine(tempDirectory.RootPath, directoryName));

        tempDirectory.TakeSnapshot()
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
        var kitStore = KitStoreFactoryTests.CreateFactory().Create(tempDirectory.RootPath);

        var config = await kitStore.ReadJsonAsync<KitConfig>(KitStorePaths.ConfigFileName, FormatIds.Config,
            FormatAccess.Read, cancellationToken);
        var gates = await kitStore.ReadJsonAsync<GatesConfig>(KitStorePaths.GatesFileName, FormatIds.Gates,
            FormatAccess.Read, cancellationToken);
        var templatesLock = await kitStore.ReadJsonAsync<TemplatesLock>(KitStorePaths.TemplatesLockFileName,
            FormatIds.TemplatesLock, FormatAccess.Read, cancellationToken);

        config
            .Should().NotBeNull();

        config.Warnings
            .Should().BeEmpty();

        config.Document.KitVersion
            .Should().Be(EngineVersion);

        config.Document.Trackers
            .Should().Equal(new Dictionary<string, string> { ["github.com"] = "gh" });

        config.Document.CoverageThreshold
            .Should().BeNull();

        gates
            .Should().NotBeNull();

        gates.Warnings
            .Should().BeEmpty();

        templatesLock
            .Should().NotBeNull();

        templatesLock.Warnings
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
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => CreateInitializer().InitializeAsync(tempDirectory.RootPath, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task InitializeAsync_TokenCanceledDuringPreconditions_ThrowsOperationCanceledExceptionAndWritesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        using var cancellationTokenSource = new CancellationTokenSource();
        var snapshot = tempDirectory.TakeSnapshot();

        // Every plan passes and cancels the token without observing it, so the preconditions complete and the
        // cancellation must stop the initialisation before its first write, whichever plan runs last.
        var gitIgnorePlanner = A.Fake<IGitIgnorePlanner>();
        A.CallTo(() => gitIgnorePlanner.PlanAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Returns(Task.FromResult<PlannedFileChange?>(null));

        var instructionSectionPlanner = A.Fake<IInstructionSectionPlanner>();
        A.CallTo(() => instructionSectionPlanner.PlanAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Returns(Task.FromResult<PlannedFileChange?>(null));

        var initializer = CreateInitializer(gitIgnorePlanner, instructionSectionPlanner);

        // Act
        var act = () => initializer.InitializeAsync(tempDirectory.RootPath, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        Path.Exists(Path.Combine(tempDirectory.RootPath, KitRoot))
            .Should().BeFalse();

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task InitializeAsync_LastWriteFails_RemovesKitRootAndKeepsRepositoryFilesWrittenBefore()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        // AGENTS.md is written last; a read-only file can be planned but not written.
        var agentsPath = tempDirectory.WriteFile("# Agents\n", AgentsMd);
        File.SetAttributes(agentsPath, FileAttributes.ReadOnly);
        SkipWhenWritable(agentsPath);

        // Act
        var act = () => CreateInitializer().InitializeAsync(tempDirectory.RootPath,
            TestContext.Current.CancellationToken);

        // Assert
        try
        {
            await act.Should().ThrowAsync<UnauthorizedAccessException>();

            Path.Exists(Path.Combine(tempDirectory.RootPath, KitRoot))
                .Should().BeFalse();

            File.Exists(Path.Combine(tempDirectory.RootPath, GitIgnore))
                .Should().BeTrue();

            tempDirectory.ReadFileText(ClaudeMd)
                .Should().Be(ExpectedSectionFile);

            tempDirectory.ReadFileText(AgentsMd)
                .Should().Be("# Agents\n");
        }
        finally
        {
            File.SetAttributes(agentsPath, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task InitializeAsync_AfterLastWriteFailed_SecondRunCompletesOnlyTheMissingChanges()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        var agentsPath = tempDirectory.WriteFile("# Agents\n", AgentsMd);
        File.SetAttributes(agentsPath, FileAttributes.ReadOnly);
        SkipWhenWritable(agentsPath);

        try
        {
            var firstRun = () => CreateInitializer().InitializeAsync(tempDirectory.RootPath,
                TestContext.Current.CancellationToken);
            await firstRun.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        finally
        {
            File.SetAttributes(agentsPath, FileAttributes.Normal);
        }

        // Act
        var result = await CreateInitializer().InitializeAsync(tempDirectory.RootPath,
            TestContext.Current.CancellationToken);

        // Assert
        result.Files.Where(file => !file.Path.StartsWith(KitRoot + "/", StringComparison.Ordinal))
            .Should().Equal(new InitializedFile(AgentsMd, InitializedFileAction.Appended));

        File.Exists(Path.Combine(tempDirectory.RootPath, KitRoot, "templates.lock.json"))
            .Should().BeTrue();

        tempDirectory.ReadFileText(ClaudeMd)
            .Should().Be(ExpectedSectionFile);
    }

    [Fact]
    public async Task InitializeAsync_WriteInKitRootFails_RemovesKitRootAndWritesNoRepositoryFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        var shippedTemplateReader = A.Fake<IShippedTemplateReader>();
        A.CallTo(() => shippedTemplateReader.ReadContent(A<ShippedTemplate>._)).Throws(new IOException("disk full"));

        var initializer = CreateInitializer(shippedTemplateReader);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => initializer.InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .WithMessage("disk full");

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task InitializeAsync_FailsBeforeKitRootIsCreated_RethrowsOriginalExceptionAndWritesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).Throws(new InvalidOperationException("no version"));

        var initializer = new RepositoryInitializer(new GitRepositoryLocator(), KitStoreFactoryTests.CreateFactory(),
            new ShippedTemplateReader(), new GitIgnorePlanner(), new InstructionSectionPlanner(), engineVersionProvider,
            FixedLineEnding.CreateWriter(LineEnding.Lf), A.Fake<ILineEndingResolver>());
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => initializer.InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("no version");

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task InitializeAsync_RollbackFails_RethrowsWriteErrorAndNextRunReportsAlreadyInitialized()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make the kit root read-only.");
        Assert.SkipWhen(Environment.UserName == "root", "root can write to a read-only directory.");

        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        var kitRootPath = Path.Combine(tempDirectory.RootPath, KitRoot);

        // The first template is read after config.json and gates.json are written. A read-only kit root makes the
        // template write fail and keeps the rollback from removing the files in it.
        var shippedTemplateReader = A.Fake<IShippedTemplateReader>();
        A.CallTo(() => shippedTemplateReader.ReadContent(A<ShippedTemplate>._))
            .Invokes(() => File.SetUnixFileMode(kitRootPath, UnixFileMode.UserRead | UnixFileMode.UserExecute))
            .Returns("template");

        try
        {
            // Act
            var act = () => CreateInitializer(shippedTemplateReader).InitializeAsync(tempDirectory.RootPath,
                TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>();

            Directory.Exists(kitRootPath)
                .Should().BeTrue();

            var secondRun = () => CreateInitializer().InitializeAsync(tempDirectory.RootPath,
                TestContext.Current.CancellationToken);

            await secondRun.Should().ThrowAsync<KitAlreadyInitializedException>();
        }
        finally
        {
            File.SetUnixFileMode(kitRootPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task InitializeAsync_CanceledWhileWritingKitRoot_RemovesKitRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        using var cancellationTokenSource = new CancellationTokenSource();

        // The first template is read after config.json and gates.json are written; the cancellation stops the write
        // of the template.
        var shippedTemplateReader = A.Fake<IShippedTemplateReader>();
        A.CallTo(() => shippedTemplateReader.ReadContent(A<ShippedTemplate>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Returns("template");

        var initializer = CreateInitializer(shippedTemplateReader);
        var snapshot = tempDirectory.TakeSnapshot();

        // Act
        var act = () => initializer.InitializeAsync(tempDirectory.RootPath, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Constructor_NullDependency_ThrowsArgumentNullException(int nullIndex)
    {
        // Arrange
        object?[] dependencies =
        [
            A.Fake<IGitRepositoryLocator>(),
            A.Fake<IKitStoreFactory>(),
            A.Fake<IShippedTemplateReader>(),
            A.Fake<IGitIgnorePlanner>(),
            A.Fake<IInstructionSectionPlanner>(),
            A.Fake<IEngineVersionProvider>(),
            A.Fake<ITextFileWriter>(),
            A.Fake<ILineEndingResolver>()
        ];
        dependencies[nullIndex] = null;

        // Act
        var act = () => new RepositoryInitializer(
            (IGitRepositoryLocator)dependencies[0]!,
            (IKitStoreFactory)dependencies[1]!,
            (IShippedTemplateReader)dependencies[2]!,
            (IGitIgnorePlanner)dependencies[3]!,
            (IInstructionSectionPlanner)dependencies[4]!,
            (IEngineVersionProvider)dependencies[5]!,
            (ITextFileWriter)dependencies[6]!,
            (ILineEndingResolver)dependencies[7]!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Skips the current test if the process can still write a read-only file, for example when it runs as root.
    /// </summary>
    private static void SkipWhenWritable(string filePath)
    {
        try
        {
            File.OpenWrite(filePath).Dispose();
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        File.SetAttributes(filePath, FileAttributes.Normal);
        Assert.Skip($"The read-only file '{filePath}' is writable by this process; a failing write cannot be tested.");
    }

    private static RepositoryInitializer CreateInitializer()
    {
        return CreateInitializer(new GitIgnorePlanner(), new InstructionSectionPlanner());
    }

    private static RepositoryInitializer CreateInitializer(IGitIgnorePlanner gitIgnorePlanner,
        IInstructionSectionPlanner instructionSectionPlanner)
    {
        return CreateInitializer(new ShippedTemplateReader(), gitIgnorePlanner, instructionSectionPlanner);
    }

    private static RepositoryInitializer CreateInitializer(IShippedTemplateReader shippedTemplateReader)
    {
        return CreateInitializer(shippedTemplateReader, new GitIgnorePlanner(), new InstructionSectionPlanner());
    }

    private static RepositoryInitializer CreateInitializer(ITextFileWriter textFileWriter)
    {
        return CreateInitializer(new ShippedTemplateReader(), new GitIgnorePlanner(), new InstructionSectionPlanner(),
            textFileWriter);
    }

    // The repository files and the files of the kit store are written by separate writers, so a test can fake the
    // writes of one without affecting the other.
    private static RepositoryInitializer CreateInitializer(IShippedTemplateReader shippedTemplateReader,
        IGitIgnorePlanner gitIgnorePlanner, IInstructionSectionPlanner instructionSectionPlanner,
        ITextFileWriter? textFileWriter = null, ITextFileWriter? kitStoreTextFileWriter = null,
        ILineEndingResolver? lineEndingResolver = null)
    {
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).Returns(EngineVersion);

        // A fixed line ending keeps the tests independent of the operating system and the git configuration.
        return new RepositoryInitializer(new GitRepositoryLocator(),
            KitStoreFactoryTests.CreateFactory(textFileWriter: kitStoreTextFileWriter),
            shippedTemplateReader, gitIgnorePlanner, instructionSectionPlanner, engineVersionProvider,
            textFileWriter ?? FixedLineEnding.CreateWriter(LineEnding.Lf),
            lineEndingResolver ?? A.Fake<ILineEndingResolver>());
    }

    // Every write of the writer, or only the writes of the file with the specified name.
    private static IReturnValueArgumentValidationConfiguration<Task<IReadOnlyList<KitWarning>>> CallToWrite(
        ITextFileWriter textFileWriter, string? fileName = null)
    {
        return fileName is null
            ? A.CallTo(() => textFileWriter.WriteAsync(A<string>._, A<string>._, A<TextFileContent>._, A<string?>._,
                A<CancellationToken>._))
            : A.CallTo(() => textFileWriter.WriteAsync(A<string>._, fileName, A<TextFileContent>._, A<string?>._,
                A<CancellationToken>._));
    }

    private static Task<IReadOnlyList<KitWarning>> Warnings(params KitWarning[] warnings)
    {
        return Task.FromResult<IReadOnlyList<KitWarning>>(warnings);
    }

    private static IEnumerable<string> ReadLockHashes(TempKitDirectory tempDirectory)
    {
        var root = JsonNode.Parse(tempDirectory.ReadFileText(KitRoot, "templates.lock.json"))!;

        return [.. root["templates"]!.AsArray().Select(x => x!["hash"]!.GetValue<string>())];
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

    private static IEnumerable<string> GetRelativeFilePaths(string rootPath, string directoryPath)
    {
        return Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Select(x => Path.GetRelativePath(rootPath, x).Replace(Path.DirectorySeparatorChar, '/'));
    }
}
