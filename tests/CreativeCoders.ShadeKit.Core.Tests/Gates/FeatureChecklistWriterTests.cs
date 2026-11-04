using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Gates.Exceptions;
using CreativeCoders.ShadeKit.Core.Git.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Core.Templates;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Core.Tests.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Gates;

public sealed class FeatureChecklistWriterTests : IDisposable
{
    private const string FeatureId = "gh-142-login-sso";

    private const string Checklist = "checks/definition-of-ready.md";

    private const string TemplateText =
        "# Definition of Ready: {{feature-id}}\n" +
        "\n" +
        "<!--\n" +
        "Guidance stays: the engine copies this template for {{feature-id}}.\n" +
        "-->\n" +
        "\n" +
        "Feature: `{{feature-id}}` · Again: {{feature-id}}{{feature-id}}\n" +
        "Unknown placeholder {{feature-title}} and {{Feature-Id}} stay.\n";

    private const string ExpectedCopyText =
        "# Definition of Ready: gh-142-login-sso\n" +
        "\n" +
        "<!--\n" +
        "Guidance stays: the engine copies this template for gh-142-login-sso.\n" +
        "-->\n" +
        "\n" +
        "Feature: `gh-142-login-sso` · Again: gh-142-login-ssogh-142-login-sso\n" +
        "Unknown placeholder {{feature-title}} and {{Feature-Id}} stay.\n";

    private readonly TempKitDirectory _repository = new TempKitDirectory();

    private readonly string _kitStorePath;

    public FeatureChecklistWriterTests()
    {
        _repository.CreateGitRoot();
        _kitStorePath = _repository.CreateDirectory(KitStorePaths.RootPath);
    }

    public static TheoryData<string> InvalidChecklists { get; } = new TheoryData<string>
    {
        "definition-of-ready.md",
        "checks/../../gates.json",
        "checks/../config.md",
        "checks\\definition-of-ready.md",
        "checks/definition-of-ready.txt",
        "/checks/definition-of-ready.md",
        "checks//definition-of-ready.md",
        "checks/.md",
        "checks/definition-of-ready.md:hidden.md",
        "checks/definition\u001b-of-ready.md"
    };

    public void Dispose()
    {
        _repository.Dispose();
    }

    [Fact]
    public async Task EnsureCopyAsync_CopyMissing_CreatesCopyWithEveryFeatureIdPlaceholderReplaced()
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new FeatureChecklistCopyResult(Created: true, Warnings: []));

        ReadCopyText(Checklist)
            .Should().Be(ExpectedCopyText);
    }

    [Fact]
    [Trait("Requirement", "FP05-AC2")]
    public async Task EnsureCopyAsync_ShippedDefinitionOfReadyTemplate_CreatesCopyWithoutPlaceholderAndKeepsGuidance()
    {
        // Arrange
        var shippedTemplate = ShippedTemplates.All.Single(x =>
            x.KitPath == KitStorePaths.GetTemplatePath(GatesConfig.DefinitionOfReadyChecklist));
        var shippedText = new ShippedTemplateReader().ReadContent(shippedTemplate);
        WriteTemplate(GatesConfig.DefinitionOfReadyChecklist, shippedText);
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(CreateStore(), FeatureId, GatesConfig.DefinitionOfReadyChecklist,
            TestContext.Current.CancellationToken);

        // Assert
        result.Created
            .Should().BeTrue();

        var copy = ReadCopyText(GatesConfig.DefinitionOfReadyChecklist);

        copy
            .Should().StartWith($"# Definition of Ready: {FeatureId}\n")
            .And.Contain("<!--\nEntry gate of plan.")
            .And.NotContain(FeatureChecklistWriter.FeatureIdPlaceholder);

        copy
            .Should().Be(shippedText.Replace(FeatureChecklistWriter.FeatureIdPlaceholder, FeatureId,
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EnsureCopyAsync_TemplateBomAndFinalLineEnding_KeepsBothFlags(bool hasUtf8Bom,
        bool hasFinalLineEnding)
    {
        // Arrange
        var templateText = "# {{feature-id}}\n<!-- guidance -->" + (hasFinalLineEnding ? "\n" : string.Empty);
        byte[] templateBytes = [.. hasUtf8Bom ? TestText.Utf8Bom : [], .. Encoding.UTF8.GetBytes(templateText)];
        WriteTemplateBytes(Checklist, templateBytes);
        var writer = new FeatureChecklistWriter();

        // Act
        await writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist, TestContext.Current.CancellationToken);

        // Assert
        var expectedText = $"# {FeatureId}\n<!-- guidance -->" + (hasFinalLineEnding ? "\n" : string.Empty);
        byte[] expectedBytes = [.. hasUtf8Bom ? TestText.Utf8Bom : [], .. Encoding.UTF8.GetBytes(expectedText)];

        ReadCopyBytes(Checklist)
            .Should().Equal(expectedBytes);
    }

    [Fact]
    public async Task EnsureCopyAsync_WriterResolvesCrLf_WritesCopyThroughStoreWithCrLf()
    {
        // Arrange
        WriteTemplate(Checklist, "# {{feature-id}}\n\nText\n");
        var store = TestKitStores.Create(_kitStorePath, FixedLineEnding.CreateWriter(LineEnding.CrLf));
        var writer = new FeatureChecklistWriter();

        // Act
        await writer.EnsureCopyAsync(store, FeatureId, Checklist, TestContext.Current.CancellationToken);

        // Assert
        ReadCopyText(Checklist)
            .Should().Be($"# {FeatureId}\r\n\r\nText\r\n");
    }

    [Fact]
    public async Task EnsureCopyAsync_WriteReturnsWarnings_ReturnsWriteWarnings()
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        var warning = new GitAttributesUnavailableWarning(_repository.RootPath, "git check-attr", "git failed");
        var store = TestKitStores.Create(_kitStorePath, FixedLineEnding.CreateWriter(LineEnding.Lf, warning));
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(store, FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        result.Created
            .Should().BeTrue();

        result.Warnings
            .Should().Equal(warning);
    }

    [Fact]
    [Trait("Requirement", "FP05-AC8")]
    public async Task EnsureCopyAsync_TeamDefinedChecklist_CopiesTemplateToMirroredPathInFeatureFolder()
    {
        // Arrange
        const string teamChecklist = "checks/implement.md";
        WriteTemplate(teamChecklist, "# Implement check: {{feature-id}}\n- [ ] Team item\n");
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(CreateStore(), FeatureId, teamChecklist,
            TestContext.Current.CancellationToken);

        // Assert
        result.Created
            .Should().BeTrue();

        _repository.ReadFileText(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, FeatureId,
                KitStorePaths.ChecksDirName, "implement.md")
            .Should().Be($"# Implement check: {FeatureId}\n- [ ] Team item\n");
    }

    [Fact]
    public async Task EnsureCopyAsync_NestedChecklistPath_CreatesMissingDirectories()
    {
        // Arrange
        const string nestedChecklist = "checks/team/release/sign-off.md";
        WriteTemplate(nestedChecklist, "Sign-off for {{feature-id}}\n");
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(CreateStore(), FeatureId, nestedChecklist,
            TestContext.Current.CancellationToken);

        // Assert
        result.Created
            .Should().BeTrue();

        ReadCopyText(nestedChecklist)
            .Should().Be($"Sign-off for {FeatureId}\n");
    }

    [Fact]
    [Trait("Requirement", "FP05-AC3")]
    public async Task EnsureCopyAsync_CopyExists_LeavesCopyByteForByteUnchangedEvenIfTemplateChanged()
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        var writer = new FeatureChecklistWriter();
        await writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist, TestContext.Current.CancellationToken);
        byte[] filledCopy = [.. TestText.Utf8Bom, .. "# Filled by the agent\r\n| DOR-01 | pass |"u8];
        WriteCopyBytes(Checklist, filledCopy);
        WriteTemplate(Checklist, "# Changed template {{feature-id}}\n");

        // Act
        var result = await writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new FeatureChecklistCopyResult(Created: false, Warnings: []));

        ReadCopyBytes(Checklist)
            .Should().Equal(filledCopy);
    }

    [Fact]
    public async Task EnsureCopyAsync_CopyExistsAndTemplateMissing_ReturnsNotCreated()
    {
        // Arrange
        WriteCopyBytes(Checklist, "# Filled\n"u8.ToArray());
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        result.Created
            .Should().BeFalse();
    }

    [Fact]
    public async Task EnsureCopyAsync_CopyExistsAndIsNotUtf8_DoesNotReadCopy()
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        byte[] invalidCopy = [(byte)'#', 0xC3, 0x28, (byte)'\n'];
        WriteCopyBytes(Checklist, invalidCopy);
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        result.Created
            .Should().BeFalse();

        ReadCopyBytes(Checklist)
            .Should().Equal(invalidCopy);
    }

    [Fact]
    public async Task EnsureCopyAsync_DirectoryAtCopyPath_ThrowsPathNotAFileExceptionAndWritesNothing()
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        var copyDirectoryPath = _repository.CreateDirectory(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName,
            FeatureId, KitStorePaths.ChecksDirName, "definition-of-ready.md");
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PathNotAFileException>())
            .Which.Path.Should().Be(copyDirectoryPath);

        Directory.EnumerateFileSystemEntries(copyDirectoryPath)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureCopyAsync_DirectoryAtCopyPathAndTemplateMissing_ThrowsPathNotAFileException()
    {
        // Arrange
        _repository.CreateDirectory(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, FeatureId,
            KitStorePaths.ChecksDirName, "definition-of-ready.md");
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<PathNotAFileException>();
    }

    [Fact]
    public async Task EnsureCopyAsync_TemplateMissing_ThrowsChecklistTemplateNotFoundExceptionAndWritesNothing()
    {
        // Arrange
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ChecklistTemplateNotFoundException>())
            .Which.TemplatePath.Should().Be(".shadekit/templates/checks/definition-of-ready.md");

        AssertNothingWrittenInFeatureFolder();
    }

    [Fact]
    public async Task EnsureCopyAsync_TemplateIsDirectory_ThrowsPathNotAFileExceptionAndWritesNothing()
    {
        // Arrange
        var templateDirectoryPath = _repository.CreateDirectory(KitStorePaths.RootPath,
            KitStorePaths.TemplatesDirName, KitStorePaths.ChecksDirName, "definition-of-ready.md");
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PathNotAFileException>())
            .Which.Path.Should().Be(templateDirectoryPath);

        AssertNothingWrittenInFeatureFolder();
    }

    [Fact]
    public async Task EnsureCopyAsync_TemplateIsNotUtf8_ThrowsInvalidKitFileExceptionAndWritesNothing()
    {
        // Arrange
        WriteTemplateBytes(Checklist, [(byte)'#', 0xC3, 0x28, (byte)'\n']);
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>())
            .Which.FilePath.Should().Be(".shadekit/templates/checks/definition-of-ready.md");

        AssertNothingWrittenInFeatureFolder();
    }

    [Fact]
    public async Task EnsureCopyAsync_NullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(null!, FeatureId, Checklist, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("store");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("gh-142\nlogin")]
    [InlineData("gh-142\rlogin")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../templates")]
    [InlineData("../../outside")]
    [InlineData("gh-142/login")]
    [InlineData(" gh-142-login")]
    [InlineData("gh-142-login.")]
    [InlineData("GH-142-Login")]
    [InlineData("Draft Folder")]
    public async Task EnsureCopyAsync_InvalidFeatureId_ThrowsArgumentExceptionAndWritesNothing(string? featureId)
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        var templateBytes = _repository.ReadFileBytes(KitStorePaths.RootPath, KitStorePaths.TemplatesDirName,
            KitStorePaths.ChecksDirName, "definition-of-ready.md");
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), featureId!, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName(nameof(featureId));

        Directory.EnumerateFileSystemEntries(_repository.RootPath)
            .Select(Path.GetFileName)
            .Should().BeEquivalentTo(".git", KitStorePaths.RootPath);

        Directory.EnumerateFileSystemEntries(_kitStorePath, "*", SearchOption.AllDirectories)
            .Select(x => Path.GetRelativePath(_kitStorePath, x))
            .Should().BeEquivalentTo(
                KitStorePaths.TemplatesDirName,
                Path.Combine(KitStorePaths.TemplatesDirName, KitStorePaths.ChecksDirName),
                Path.Combine(KitStorePaths.TemplatesDirName, KitStorePaths.ChecksDirName, "definition-of-ready.md"));

        _repository.ReadFileBytes(KitStorePaths.RootPath, KitStorePaths.TemplatesDirName, KitStorePaths.ChecksDirName,
                "definition-of-ready.md")
            .Should().Equal(templateBytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EnsureCopyAsync_MissingChecklist_ThrowsArgumentException(string? checklist)
    {
        // Arrange
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), FeatureId, checklist!,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName(nameof(checklist));
    }

    [Theory]
    [MemberData(nameof(InvalidChecklists))]
    public async Task EnsureCopyAsync_InvalidChecklistPath_ThrowsArgumentExceptionAndWritesNothing(string checklist)
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), FeatureId, checklist,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName(nameof(checklist));

        Directory.Exists(Path.Combine(_kitStorePath, KitStorePaths.FeaturesDirName))
            .Should().BeFalse();
    }

    [Fact]
    public async Task EnsureCopyAsync_CanceledToken_ThrowsOperationCanceledExceptionAndWritesNothing()
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        var writer = new FeatureChecklistWriter();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        AssertNothingWrittenInFeatureFolder();
    }

    [Fact]
    public async Task EnsureCopyAsync_FeatureFolderHasOtherFiles_CreatesCopyAndLeavesOtherFilesUnchanged()
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        byte[] featureFile = [.. """{ "id": "gh-142-login-sso" }"""u8, (byte)'\r', (byte)'\n'];
        byte[] otherChecklist = [.. "# Done\n"u8];
        _repository.WriteFileBytes(featureFile, KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, FeatureId,
            KitStorePaths.FeatureFileName);
        WriteCopyBytes(GatesConfig.DefinitionOfDoneChecklist, otherChecklist);
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        result.Created
            .Should().BeTrue();

        ReadCopyText(Checklist)
            .Should().Be(ExpectedCopyText);

        _repository.ReadFileBytes(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, FeatureId,
                KitStorePaths.FeatureFileName)
            .Should().Equal(featureFile);

        ReadCopyBytes(GatesConfig.DefinitionOfDoneChecklist)
            .Should().Equal(otherChecklist);
    }

    [Fact]
    public async Task EnsureCopyAsync_EmptyTemplate_CreatesEmptyCopy()
    {
        // Arrange
        WriteTemplateBytes(Checklist, []);
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        result.Created
            .Should().BeTrue();

        ReadCopyBytes(Checklist)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("# {{feature-id}}\r\n\r\nText\r\n")]
    [InlineData("# {{feature-id}}\r\rText\r")]
    public async Task EnsureCopyAsync_TemplateWithOtherLineEnding_WritesCopyWithResolvedLineEnding(
        string templateText)
    {
        // Arrange
        WriteTemplate(Checklist, templateText);
        var writer = new FeatureChecklistWriter();

        // Act
        await writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist, TestContext.Current.CancellationToken);

        // Assert
        ReadCopyText(Checklist)
            .Should().Be($"# {FeatureId}\n\nText\n");
    }

    [Fact]
    public async Task EnsureCopyAsync_Token_PassesTokenToReadAndWritesCopyAsNewFile()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var copyPath = KitStorePaths.GetFeatureChecklistPath(FeatureId, Checklist);
        var templatePath = KitStorePaths.GetTemplatePath(Checklist);
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;
        A.CallTo(() => store.ReadFileAsync(templatePath, token))
            .Returns(TextFileContent.FromText("# {{feature-id}}\n"));
        A.CallTo(() => store.WriteFileAsync(copyPath, A<TextFileContent>._, FileWriteMode.CreateNew, token))
            .Returns(Array.Empty<KitWarning>());
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(store, FeatureId, Checklist, token);

        // Assert
        result.Created
            .Should().BeTrue();

        A.CallTo(() => store.WriteFileAsync(copyPath,
                A<TextFileContent>.That.Matches(x => x.Lines.SequenceEqual(new[] { $"# {FeatureId}" })),
                FileWriteMode.CreateNew, token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task EnsureCopyAsync_WriteFailsWithIOException_ThrowsKitFileWriteFailedExceptionNamingCopy()
    {
        // Arrange
        var store = CreateFakeStoreWhoseWriteThrows(new IOException("disk full"));
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(store, FeatureId, Checklist, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<KitFileWriteFailedException>()).Which;

        exception.FilePath
            .Should().Be(".shadekit/features/gh-142-login-sso/checks/definition-of-ready.md");

        exception.InnerException
            .Should().BeOfType<IOException>()
            .Which.Message.Should().Be("disk full");
    }

    [Fact]
    public async Task EnsureCopyAsync_WriteFailsBecauseCopyExists_ReturnsNotCreatedWithoutWarnings()
    {
        // Arrange
        var store = CreateFakeStoreWhoseWriteThrows(new FileAlreadyExistsException("/repo/copy.md", null));
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(store, FeatureId, Checklist, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new FeatureChecklistCopyResult(Created: false, Warnings: []));
    }

    [Fact]
    public async Task EnsureCopyAsync_WriteFailsWithUnauthorizedAccess_PropagatesExceptionUnchanged()
    {
        // Arrange
        var failure = new UnauthorizedAccessException("access denied");
        var store = CreateFakeStoreWhoseWriteThrows(failure);
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(store, FeatureId, Checklist, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task EnsureCopyAsync_WriteCanceled_PropagatesOperationCanceledExceptionUnchanged()
    {
        // Arrange
        var failure = new OperationCanceledException("canceled");
        var store = CreateFakeStoreWhoseWriteThrows(failure);
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(store, FeatureId, Checklist, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task EnsureCopyAsync_FileAtChecksDirectory_ThrowsKitFileWriteFailedExceptionAndKeepsFile()
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        byte[] strayFile = [.. "stray\n"u8];
        _repository.WriteFileBytes(strayFile, KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, FeatureId,
            KitStorePaths.ChecksDirName);
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), FeatureId, Checklist,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<KitFileWriteFailedException>())
            .Which.FilePath.Should().Be(".shadekit/features/gh-142-login-sso/checks/definition-of-ready.md");

        _repository.ReadFileBytes(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, FeatureId,
                KitStorePaths.ChecksDirName)
            .Should().Equal(strayFile);
    }

    [Fact]
    public async Task EnsureCopyAsync_FileAtNestedChecklistDirectory_ThrowsKitFileWriteFailedException()
    {
        // Arrange
        const string nestedChecklist = "checks/team/sign-off.md";
        WriteTemplate(nestedChecklist, "Sign-off for {{feature-id}}\n");
        _repository.WriteFileBytes([.. "stray\n"u8], KitStorePaths.RootPath, KitStorePaths.FeaturesDirName,
            FeatureId, KitStorePaths.ChecksDirName, "team");
        var writer = new FeatureChecklistWriter();

        // Act
        var act = () => writer.EnsureCopyAsync(CreateStore(), FeatureId, nestedChecklist,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<KitFileWriteFailedException>())
            .Which.FilePath.Should().Be(".shadekit/features/gh-142-login-sso/checks/team/sign-off.md");
    }

    [Fact]
    public async Task EnsureCopyAsync_CopyCreatedConcurrentlyBeforeWrite_ReturnsNotCreatedAndKeepsOtherCopy()
    {
        // Arrange
        WriteTemplate(Checklist, TemplateText);
        byte[] concurrentCopy = [.. "# Created by another phase start\n"u8];
        var realWriter = FixedLineEnding.CreateWriter(LineEnding.Lf);
        var textFileWriter = A.Fake<ITextFileWriter>();
        A.CallTo(() => textFileWriter.WriteAsync(A<RepositoryFile>._, A<TextFileContent>._, A<RepositoryFile?>._,
                A<FileWriteMode>._, A<CancellationToken>._))
            .ReturnsLazily((RepositoryFile file, TextFileContent content, RepositoryFile? lineEndingFile,
                FileWriteMode mode, CancellationToken token) =>
            {
                // The other call creates the copy after the existence check of this call.
                WriteCopyBytes(Checklist, concurrentCopy);

                return realWriter.WriteAsync(file, content, lineEndingFile, mode, token);
            });
        var writer = new FeatureChecklistWriter();

        // Act
        var result = await writer.EnsureCopyAsync(TestKitStores.Create(_kitStorePath, textFileWriter), FeatureId,
            Checklist, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new FeatureChecklistCopyResult(Created: false, Warnings: []));

        ReadCopyBytes(Checklist)
            .Should().Equal(concurrentCopy);
    }

    private static IKitStore CreateFakeStoreWhoseWriteThrows(Exception exception)
    {
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.ReadFileAsync(A<string>._, A<CancellationToken>._))
            .Returns(TextFileContent.FromText("# {{feature-id}}\n"));
        A.CallTo(() => store.WriteFileAsync(A<string>._, A<TextFileContent>._, A<FileWriteMode>._,
                A<CancellationToken>._))
            .ThrowsAsync(exception);

        return store;
    }

    private KitStore CreateStore()
    {
        return TestKitStores.Create(_kitStorePath);
    }

    private void WriteTemplate(string checklist, string text)
    {
        WriteTemplateBytes(checklist, Encoding.UTF8.GetBytes(text));
    }

    private void WriteTemplateBytes(string checklist, byte[] bytes)
    {
        _repository.WriteFileBytes(bytes, [KitStorePaths.RootPath, KitStorePaths.TemplatesDirName, .. checklist.Split('/')]);
    }

    private void WriteCopyBytes(string checklist, byte[] bytes)
    {
        _repository.WriteFileBytes(bytes, GetCopyPathParts(checklist));
    }

    private string ReadCopyText(string checklist)
    {
        return Encoding.UTF8.GetString(ReadCopyBytes(checklist));
    }

    private byte[] ReadCopyBytes(string checklist)
    {
        return _repository.ReadFileBytes(GetCopyPathParts(checklist));
    }

    private static string[] GetCopyPathParts(string checklist)
    {
        return [KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, FeatureId, .. checklist.Split('/')];
    }

    private void AssertNothingWrittenInFeatureFolder()
    {
        Directory.Exists(Path.Combine(_kitStorePath, KitStorePaths.FeaturesDirName))
            .Should().BeFalse();
    }
}
