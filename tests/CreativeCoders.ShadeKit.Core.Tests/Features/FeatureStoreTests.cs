using System.Runtime.Versioning;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Core.Tests.Store;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public sealed class FeatureStoreTests : IDisposable
{
    private const string FeatureId = "gh-142-login-sso";

    private const string OtherFeatureId = "gh-7-checkout";

    // A folder whose name differs from FeatureId only in case; it is not the feature on any file system.
    private const string CaseVariantFolderName = "GH-142-Login-SSO";

    private readonly FeatureStoreTestContext _context = new FeatureStoreTestContext();

    public void Dispose()
    {
        _context.Dispose();
    }

    public static TheoryData<string> InvalidFeatureIds =>
    [
        "..", "A", "../outside", "gh-142/../x", "gh_142", "", ".gh-7-checkout.1a2b3c4d.tmp",
        ".gh-7-checkout.1a2b3c4d.dropping"
    ];

    [Fact]
    public async Task ListAsync_SeveralFeatures_ReturnsSummariesSortedOrdinallyById()
    {
        // Arrange
        _context.WriteFeature("b-feature", "Beta", WorkflowProfiles.Quick, "done");
        _context.WriteFeature("a-feature-2", "Alpha 2", WorkflowProfiles.Bugfix);
        _context.WriteFeature("a-feature", "Alpha");

        // Act
        var result = await _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Features
            .Should().Equal(
                new FeatureSummary("a-feature", "Alpha", WorkflowProfiles.Feature, FeatureStatus.Active),
                new FeatureSummary("a-feature-2", "Alpha 2", WorkflowProfiles.Bugfix, FeatureStatus.Active),
                new FeatureSummary("b-feature", "Beta", WorkflowProfiles.Quick, FeatureStatus.Done));

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_FeatureFolderWithoutFeatureFile_SkipsFolderWithWarning()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.CreateFeatureFolder("aaa-broken");

        // Act
        var result = await _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Features.Select(x => x.Id)
            .Should().Equal(FeatureId);

        result.Warnings
            .Should().Equal(new FeatureFileMissingWarning("aaa-broken"));
    }

    [Fact]
    public async Task ListAsync_NoFeaturesFolder_ReturnsEmptyResult()
    {
        // Act
        var result = await _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new FeatureListResult([], []));
    }

    [Fact]
    public async Task ListAsync_InvalidFeatureFile_ThrowsInvalidKitFileException()
    {
        // Arrange
        _context.WriteFeature("a-feature");
        _context.WriteKitFile("{", "features", FeatureId, "feature.json");

        // Act
        var act = () => _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>())
            .Which.FilePath
            .Should().Be(".shadekit/features/gh-142-login-sso/feature.json");
    }

    [Fact]
    public async Task ListAsync_TooNewFeatureFile_ThrowsFormatTooNewException()
    {
        // Arrange
        _context.WriteKitFile("""{ "schemaVersion": 99, "minEngineVersion": "99.0.0" }""", "features", FeatureId,
            "feature.json");

        // Act
        var act = () => _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<FormatTooNewException>();
    }

    [Fact]
    public async Task ShowAsync_ExistingFeature_ReturnsDocumentAndPaths()
    {
        // Arrange
        _context.WriteFeature(FeatureId, "Login SSO", WorkflowProfiles.Bugfix, "done");

        // Act
        var result = await _context.CreateFeatureStore().ShowAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(FeatureId);

        result.Feature.Profile
            .Should().Be(WorkflowProfiles.Bugfix);

        result.Feature.Status
            .Should().Be(FeatureStatus.Done);

        result.Feature.CreatedBy.Email
            .Should().Be("jane@example.com");

        result.Paths
            .Should().Be(FeaturePaths.For(FeatureId));

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ShowAsync_UnknownFeature_ThrowsFeatureNotFoundException()
    {
        // Act
        var act = () => _context.CreateFeatureStore().ShowAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<FeatureNotFoundException>())
            .Which.FeatureId
            .Should().Be(FeatureId);
    }

    [Fact]
    public async Task ShowAsync_FeatureFolderWithoutFeatureFile_ThrowsInvalidKitFileException()
    {
        // Arrange
        _context.CreateFeatureFolder(FeatureId);

        // Act
        var act = () => _context.CreateFeatureStore().ShowAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidKitFileException>()).Which;

        exception.FilePath
            .Should().Be(".shadekit/features/gh-142-login-sso/feature.json");

        exception.Reason
            .Should().Be("The file is missing, although the feature folder exists.");
    }

    [Theory]
    [MemberData(nameof(InvalidFeatureIds))]
    public async Task ShowAsync_InvalidFeatureId_ThrowsInvalidFeatureIdExceptionWithoutStoreAccess(string featureId)
    {
        // Arrange
        var store = A.Fake<IKitStore>();

        // Act
        var act = () => _context.CreateFeatureStore().ShowAsync(store, featureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidFeatureIdException>();

        Fake.GetCalls(store)
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC6")]
    public async Task GetCurrentAsync_NoBranchMatchAndNoOverride_ReturnsNotResolved()
    {
        // Arrange
        _context.WriteFeature(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new CurrentFeatureResult(null, null, null, []));

        result.IsResolved
            .Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentAsync_OverrideSelectsMissingFeature_ReturnsNotResolvedWithWarning()
    {
        // Arrange
        _context.WriteOverride(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsResolved
            .Should().BeFalse();

        result.Warnings
            .Should().Equal(new CurrentFeatureMissingWarning(FeatureId));
    }

    [Fact]
    [Trait("Requirement", "FP03-AC5")]
    public async Task GetCurrentAsync_BranchAndOverrideDiffer_ReturnsBranchFeatureWithMismatchWarning()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteFeature(OtherFeatureId, "Checkout");
        _context.WriteOverride(OtherFeatureId);
        _context.SetBranch($"feature/{FeatureId}");

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsResolved
            .Should().BeTrue();

        result.Feature?.Id
            .Should().Be(FeatureId);

        result.Origin
            .Should().Be(CurrentFeatureOrigin.Branch);

        result.Paths
            .Should().Be(FeaturePaths.For(FeatureId));

        result.Warnings
            .Should().Equal(new CurrentFeatureMismatchWarning(OtherFeatureId, FeatureId));
    }

    [Fact]
    [Trait("Requirement", "FP03-AC11")]
    public async Task GetCurrentAsync_FeatureFileWithStatusDone_ReturnsStatusDone()
    {
        // Arrange
        _context.WriteFeature(FeatureId, status: "done");
        _context.WriteOverride(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature?.Status
            .Should().Be(FeatureStatus.Done);
    }

    [Fact]
    public async Task GetCurrentAsync_CurrentFeatureWithoutFeatureFile_ThrowsInvalidKitFileException()
    {
        // Arrange
        _context.CreateFeatureFolder(FeatureId);
        _context.SetBranch($"feature/{FeatureId}");

        // Act
        var act = () => _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>())
            .Which.FilePath
            .Should().Be(".shadekit/features/gh-142-login-sso/feature.json");
    }

    [Fact]
    public async Task SetCurrentAsync_ExistingFeature_WritesOverride()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteFeature(OtherFeatureId);
        _context.WriteOverride(OtherFeatureId);

        // Act
        var result = await _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new SetCurrentFeatureResult(FeatureId, []));

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(FeatureId);
    }

    [Fact]
    public async Task SetCurrentAsync_BranchSelectsSameFeature_ReturnsNoWarning()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.SetBranch($"feature/{FeatureId}");

        // Act
        var result = await _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SetCurrentAsync_BranchSelectsOtherFeature_WritesOverrideWithMismatchWarning()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteFeature(OtherFeatureId);
        _context.SetBranch($"feature/{OtherFeatureId}");

        // Act
        var result = await _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new CurrentFeatureMismatchWarning(FeatureId, OtherFeatureId));

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(FeatureId);
    }

    [Fact]
    public async Task SetCurrentAsync_StoreWritesWithWarnings_ReturnsMismatchWarningFirstAndEachStoreWarningOnce()
    {
        // Arrange
        var editorConfigWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        _context.WriteWarnings = [editorConfigWarning, editorConfigWarning];
        _context.WriteFeature(FeatureId);
        _context.WriteFeature(OtherFeatureId);
        _context.SetBranch($"feature/{OtherFeatureId}");

        // Act
        var result = await _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new CurrentFeatureMismatchWarning(FeatureId, OtherFeatureId), editorConfigWarning);
    }

    [Fact]
    public async Task SetCurrentAsync_StoreUsedBeforeWithWarnings_ReturnsNoWarningOfEarlierOperation()
    {
        // Arrange
        var earlierWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        var store = TestKitStores.Create(Path.Combine(_context.RepositoryRootPath, KitStorePaths.RootPath),
            FixedLineEnding.CreateWriterWithWarningsOnFirstWrite(LineEnding.Lf, earlierWarning), _context.Registry);
        var featureStore = _context.CreateFeatureStore();
        var earlierResult = await featureStore.CreateAsync(store,
            _context.CreateRequest("https://github.com/acme/shop/issues/142", "Login SSO"),
            TestContext.Current.CancellationToken);

        // Act
        var result = await featureStore.SetCurrentAsync(store, FeatureId, TestContext.Current.CancellationToken);

        // Assert
        earlierResult.Warnings
            .Should().Equal(earlierWarning);

        result.Warnings
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(LineEnding.Lf, "\n")]
    [InlineData(LineEnding.CrLf, "\r\n")]
    public async Task SetCurrentAsync_ExistingFeature_WritesOverrideWithResolvedLineEnding(LineEnding lineEnding,
        string newLine)
    {
        // Arrange
        _context.LineEnding = lineEnding;
        _context.WriteFeature(FeatureId);

        // Act
        await _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        var overrideJson = _context.Repository.ReadFileText(".shadekit", "features", "current-feature.json");

        overrideJson
            .Should().Be(overrideJson.ReplaceLineEndings(newLine), "every line ends with the resolved line ending")
            .And.Contain(newLine + "  \"featureId\"")
            .And.EndWith("}" + newLine);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC7")]
    public async Task SetCurrentAsync_UnknownFeature_ThrowsFeatureNotFoundExceptionAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(OtherFeatureId);
        _context.WriteOverride(OtherFeatureId);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<FeatureNotFoundException>())
            .Which.FeatureId
            .Should().Be(FeatureId);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task SetCurrentAsync_GitUnavailable_ThrowsAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        A.CallTo(() => _context.GitRepositoryInfo.GetCurrentBranchAsync(A<string>._, A<CancellationToken>._))
            .Throws(new GitUnavailableException(["symbolic-ref", "--short", "-q", "HEAD"], "git was not found."));
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitUnavailableException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [MemberData(nameof(InvalidFeatureIds))]
    public async Task SetCurrentAsync_InvalidFeatureId_ThrowsInvalidFeatureIdExceptionWithoutStoreAccess(
        string featureId)
    {
        // Arrange
        var store = A.Fake<IKitStore>();

        // Act
        var act = () => _context.CreateFeatureStore().SetCurrentAsync(store, featureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidFeatureIdException>();

        Fake.GetCalls(store)
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task PreviewDropAsync_FeatureSelectedByOverride_ListsFilesAndDeletesNothing()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteKitFile("# Request", "features", FeatureId, "request.md");
        _context.WriteKitFile("notes", "features", FeatureId, "notes", "a.md");
        _context.WriteOverride(FeatureId);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var preview = await _context.CreateFeatureStore().PreviewDropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        preview.FeatureId
            .Should().Be(FeatureId);

        preview.Files
            .Should().Equal(
                ".shadekit/features/gh-142-login-sso/feature.json",
                ".shadekit/features/gh-142-login-sso/notes/a.md",
                ".shadekit/features/gh-142-login-sso/request.md");

        preview.OverrideAffected
            .Should().BeTrue();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task PreviewDropAsync_OverrideSelectsOtherFeature_OverrideNotAffected()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteFeature(OtherFeatureId);
        _context.WriteOverride(OtherFeatureId);

        // Act
        var preview = await _context.CreateFeatureStore().PreviewDropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        preview.OverrideAffected
            .Should().BeFalse();
    }

    [Fact]
    public async Task PreviewDropAsync_NoOverride_OverrideNotAffected()
    {
        // Arrange
        _context.WriteFeature(FeatureId);

        // Act
        var preview = await _context.CreateFeatureStore().PreviewDropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        preview.OverrideAffected
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{ "schemaVersion": 99, "minEngineVersion": "99.0.0", "featureId": "gh-142-login-sso" }""")]
    public async Task PreviewDropAsync_BrokenOverride_OverrideNotAffected(string overrideContent)
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverrideContent(overrideContent);

        // Act
        var preview = await _context.CreateFeatureStore().PreviewDropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        preview.OverrideAffected
            .Should().BeFalse();
    }

    [Fact]
    public async Task PreviewDropAsync_UnknownFeature_ThrowsFeatureNotFoundException()
    {
        // Act
        var act = () => _context.CreateFeatureStore().PreviewDropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<FeatureNotFoundException>();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task DropAsync_FeatureSelectedByOverride_DeletesFolderAndClearsOverride()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteKitFile("# Request", "features", FeatureId, "request.md");
        _context.WriteFeature(OtherFeatureId);
        _context.WriteOverride(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().DropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.FeatureId
            .Should().Be(FeatureId);

        result.Files
            .Should().Equal(
                ".shadekit/features/gh-142-login-sso/feature.json",
                ".shadekit/features/gh-142-login-sso/request.md");

        result.OverrideAffected
            .Should().BeTrue();

        _context.KitDirectoryExists("features", FeatureId)
            .Should().BeFalse();

        _context.KitFileExists("features", "current-feature.json")
            .Should().BeFalse();

        _context.KitDirectoryExists("features", OtherFeatureId)
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task DropAsync_OverrideSelectsOtherFeature_DeletesFolderAndKeepsOverride()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteFeature(OtherFeatureId);
        _context.WriteOverride(OtherFeatureId);

        // Act
        var result = await _context.CreateFeatureStore().DropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.OverrideAffected
            .Should().BeFalse();

        _context.KitDirectoryExists("features", FeatureId)
            .Should().BeFalse();

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(OtherFeatureId);
    }

    [Fact]
    public async Task DropAsync_BrokenFeatureFile_DeletesFolder()
    {
        // Arrange
        _context.WriteKitFile("{", "features", FeatureId, "feature.json");

        // Act
        var result = await _context.CreateFeatureStore().DropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.Files
            .Should().Equal(".shadekit/features/gh-142-login-sso/feature.json");

        _context.KitDirectoryExists("features", FeatureId)
            .Should().BeFalse();
    }

    [Fact]
    public async Task DropAsync_EmptyFeatureFolder_DeletesFolderWithEmptyFileList()
    {
        // Arrange
        _context.CreateFeatureFolder(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().DropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new FeatureDropResult(FeatureId, [], false));

        _context.KitDirectoryExists("features", FeatureId)
            .Should().BeFalse();
    }

    [Fact]
    public async Task DropAsync_BrokenOverride_DeletesFolderAndLeavesOverride()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverrideContent("{");

        // Act
        var result = await _context.CreateFeatureStore().DropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.OverrideAffected
            .Should().BeFalse();

        _context.KitDirectoryExists("features", FeatureId)
            .Should().BeFalse();

        _context.Repository.ReadFileText(".shadekit", "features", "current-feature.json")
            .Should().Be("{");
    }

    [Fact]
    public async Task DropAsync_UnknownFeature_ThrowsFeatureNotFoundExceptionAndChangesNothing()
    {
        // Arrange
        _context.WriteFeature(OtherFeatureId);
        _context.WriteOverride(OtherFeatureId);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateFeatureStore().DropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<FeatureNotFoundException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task ShowAsync_FolderNameDiffersOnlyInCase_ThrowsFeatureNotFoundException()
    {
        // Arrange
        _context.WriteFeature(CaseVariantFolderName, recordedId: FeatureId);

        // Act
        var act = () => _context.CreateFeatureStore().ShowAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<FeatureNotFoundException>())
            .Which.FeatureId
            .Should().Be(FeatureId);
    }

    [Fact]
    public async Task SetCurrentAsync_FolderNameDiffersOnlyInCase_ThrowsFeatureNotFoundExceptionAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(CaseVariantFolderName, recordedId: FeatureId);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<FeatureNotFoundException>())
            .Which.FeatureId
            .Should().Be(FeatureId);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task PreviewDropAsync_FolderNameDiffersOnlyInCase_ThrowsFeatureNotFoundException()
    {
        // Arrange
        _context.WriteFeature(CaseVariantFolderName, recordedId: FeatureId);

        // Act
        var act = () => _context.CreateFeatureStore().PreviewDropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<FeatureNotFoundException>())
            .Which.FeatureId
            .Should().Be(FeatureId);
    }

    [Fact]
    public async Task DropAsync_FolderNameDiffersOnlyInCase_ThrowsFeatureNotFoundExceptionAndChangesNothing()
    {
        // Arrange
        _context.WriteFeature(CaseVariantFolderName, recordedId: FeatureId);
        _context.WriteOverride(FeatureId);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateFeatureStore().DropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<FeatureNotFoundException>())
            .Which.FeatureId
            .Should().Be(FeatureId);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.KitDirectoryExists("features", CaseVariantFolderName)
            .Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidFeatureIds))]
    public async Task DropAsync_InvalidFeatureId_ThrowsInvalidFeatureIdExceptionWithoutStoreAccess(string featureId)
    {
        // Arrange
        var store = A.Fake<IKitStore>();

        // Act
        var act = () => _context.CreateFeatureStore().DropAsync(store, featureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidFeatureIdException>();

        Fake.GetCalls(store)
            .Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidFeatureIds))]
    public async Task PreviewDropAsync_InvalidFeatureId_ThrowsInvalidFeatureIdExceptionWithoutStoreAccess(
        string featureId)
    {
        // Arrange
        var store = A.Fake<IKitStore>();

        // Act
        var act = () => _context.CreateFeatureStore().PreviewDropAsync(store, featureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidFeatureIdException>();

        Fake.GetCalls(store)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("Draft Folder")]
    [InlineData("Login")]
    [InlineData("gh_142")]
    [InlineData("gh-142-")]
    public async Task ListAsync_FolderNameIsNoValidId_SkipsFolderWithInvalidFolderWarning(string folderName)
    {
        // Arrange
        _context.WriteFeature(folderName, "Draft");
        _context.WriteFeature(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Features.Select(x => x.Id)
            .Should().Equal(FeatureId);

        result.Warnings
            .Should().Equal(new FeatureFolderInvalidWarning(folderName));
    }

    [Theory]
    [InlineData(".gh-7-checkout.1a2b3c4d.tmp")]
    [InlineData(".gh-7-checkout.1a2b3c4d.dropping")]
    [InlineData(".hidden")]
    public async Task ListAsync_FolderNameStartsWithDot_IgnoresFolderWithoutWarning(string folderName)
    {
        // Arrange
        _context.WriteFeature(folderName, "Leftover");
        _context.CreateFeatureFolder(".empty-leftover");
        _context.WriteFeature(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Features.Select(x => x.Id)
            .Should().Equal(FeatureId);

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_FeatureFileWithOtherId_ListsFolderNameWithIdMismatchWarning()
    {
        // Arrange
        _context.WriteFeature("copy-of-login", "Copy", recordedId: FeatureId);
        _context.WriteFeature(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Features.Select(x => (x.Id, x.Title))
            .Should().Equal(("copy-of-login", "Copy"), (FeatureId, "Login SSO"));

        result.Warnings
            .Should().Equal(new FeatureIdMismatchWarning("copy-of-login", FeatureId));

        (await _context.ReadFeatureFileAsync("copy-of-login")).Id
            .Should().Be(FeatureId, "the file is not changed");
    }

    [Fact]
    public async Task ShowAsync_FeatureFileWithOtherId_ReturnsFolderIdWithIdMismatchWarning()
    {
        // Arrange
        _context.WriteFeature("copy-of-login", "Copy", recordedId: FeatureId);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateFeatureStore().ShowAsync(_context.CreateStore(), "copy-of-login",
            TestContext.Current.CancellationToken);

        // Assert
        result.FeatureId
            .Should().Be("copy-of-login");

        result.Feature.Id
            .Should().Be(FeatureId);

        result.Paths
            .Should().Be(FeaturePaths.For("copy-of-login"));

        result.Warnings
            .Should().Equal(new FeatureIdMismatchWarning("copy-of-login", FeatureId));

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task GetCurrentAsync_FeatureFileWithOtherId_ReturnsFolderIdWithIdMismatchWarning()
    {
        // Arrange
        _context.WriteFeature("copy-of-login", "Copy", recordedId: FeatureId);
        _context.WriteOverride("copy-of-login");

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.FeatureId
            .Should().Be("copy-of-login");

        result.Paths
            .Should().Be(FeaturePaths.For("copy-of-login"));

        result.Warnings
            .Should().Equal(new FeatureIdMismatchWarning("copy-of-login", FeatureId));
    }

    [Fact]
    public async Task GetCurrentAsync_BranchAndOverrideDifferAndFeatureFileWithOtherId_ReturnsWarningsInOrder()
    {
        // Arrange
        _context.WriteFeature("copy-of-login", "Copy", recordedId: FeatureId);
        _context.WriteFeature(OtherFeatureId, "Checkout");
        _context.WriteOverride(OtherFeatureId);
        _context.SetBranch("feature/copy-of-login");

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new CurrentFeatureMismatchWarning(OtherFeatureId, "copy-of-login"),
                new FeatureIdMismatchWarning("copy-of-login", FeatureId));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{ "schemaVersion": 99, "minEngineVersion": "99.0.0", "featureId": "x" }""")]
    public async Task GetCurrentAsync_BrokenOverrideAndBranchFeature_ReturnsBranchFeatureWithInvalidWarning(
        string overrideContent)
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverrideContent(overrideContent);
        _context.SetBranch($"feature/{FeatureId}");
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.FeatureId
            .Should().Be(FeatureId);

        result.Origin
            .Should().Be(CurrentFeatureOrigin.Branch);

        result.Warnings
            .Should().ContainSingle()
            .Which.Should().BeOfType<CurrentFeatureInvalidWarning>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task ListAsync_OutdatedFeatureFilesAndConfig_ReturnsEachWarningOnce()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Feature, FormatIds.Config);
        _context.WriteConfig(string.Empty);
        _context.WriteFeature("a-feature");
        _context.WriteFeature("b-feature");

        // Act
        var result = await _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/features/a-feature/feature.json", 1, 2),
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/config.json", 1, 2),
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/features/b-feature/feature.json", 1, 2));
    }

    [Fact]
    public async Task ListAsync_OutdatedFeatureFilesAndSkippedFolders_ReturnsFormatWarningsBeforeOtherWarnings()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Feature);
        _context.CreateFeatureFolder("A-invalid");
        _context.WriteFeature("b-copy", recordedId: FeatureId);
        _context.CreateFeatureFolder("c-empty");
        _context.WriteFeature("d-feature");

        // Act
        var result = await _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/features/b-copy/feature.json", 1, 2),
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/features/d-feature/feature.json", 1, 2),
                new FeatureFolderInvalidWarning("A-invalid"),
                new FeatureIdMismatchWarning("b-copy", FeatureId),
                new FeatureFileMissingWarning("c-empty"));
    }

    [Fact]
    public async Task ShowAsync_OutdatedFeatureFileWithOtherId_ReturnsFormatWarningBeforeIdMismatchWarning()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Feature);
        _context.WriteFeature("copy-of-login", "Copy", recordedId: FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().ShowAsync(_context.CreateStore(), "copy-of-login",
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/features/copy-of-login/feature.json", 1,
                    2),
                new FeatureIdMismatchWarning("copy-of-login", FeatureId));
    }

    [Fact]
    public async Task GetCurrentAsync_OutdatedFilesWithMismatchAndOtherId_ReturnsFormatWarningsBeforeOtherWarnings()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature,
            FormatIds.Feature);
        _context.WriteFeature("copy-of-login", "Copy", recordedId: FeatureId);
        _context.WriteFeature(OtherFeatureId, "Checkout");
        _context.WriteOverride(OtherFeatureId);
        _context.SetBranch("feature/copy-of-login");

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.FeatureId
            .Should().Be("copy-of-login");

        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/features/current-feature.json", 1, 2),
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/features/copy-of-login/feature.json", 1,
                    2),
                new CurrentFeatureMismatchWarning(OtherFeatureId, "copy-of-login"),
                new FeatureIdMismatchWarning("copy-of-login", FeatureId));
    }

    [Fact]
    public async Task ShowAsync_InvalidFeatureFile_ThrowsInvalidKitFileException()
    {
        // Arrange
        _context.WriteKitFile("{", "features", FeatureId, "feature.json");

        // Act
        var act = () => _context.CreateFeatureStore().ShowAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidKitFileException>();
    }

    [Fact]
    public async Task GetCurrentAsync_OutdatedOverrideFeatureFileAndConfig_ReturnsMergedWarningsOnce()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature,
            FormatIds.Feature, FormatIds.Config);
        _context.WriteConfig(string.Empty);
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature?.Id
            .Should().Be(FeatureId);

        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/features/current-feature.json", 1, 2),
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/config.json", 1, 2),
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/features/gh-142-login-sso/feature.json",
                    1, 2));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "featureId": null }""")]
    [InlineData("""{ "schemaVersion": 99, "minEngineVersion": "99.0.0", "featureId": "x" }""")]
    public async Task SetCurrentAsync_BrokenOverride_ReplacesOverride(string overrideContent)
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverrideContent(overrideContent);

        // Act
        var result = await _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new SetCurrentFeatureResult(FeatureId, []));

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(FeatureId);
    }

    [Fact]
    public async Task SetCurrentAsync_OutdatedOverride_ReplacesOverrideInCurrentVersion()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature);
        _context.WriteConfig(""", "outdatedFormatOnRead": "stop" """);
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(OtherFeatureId);

        // Act
        await _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(FeatureId, "the override is read without a format warning, so it is in the current version");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{ "schemaVersion": 99, "minEngineVersion": "99.0.0" }""")]
    public async Task SetCurrentAsync_OutdatedOverrideAndUnusableConfig_ReplacesOverride(string configContent)
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature);
        _context.WriteKitFile(configContent, KitStorePaths.ConfigFileName);
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(OtherFeatureId);

        // Act
        await _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(FeatureId);

        _context.Repository.ReadFileText(".shadekit", "config.json")
            .Should().Be(configContent);
    }

    [Fact]
    public async Task SetCurrentAsync_BrokenOverrideAndGitUnavailable_ThrowsAndKeepsOverride()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverrideContent("{");
        A.CallTo(() => _context.GitRepositoryInfo.GetCurrentBranchAsync(A<string>._, A<CancellationToken>._))
            .Throws(new GitUnavailableException(["symbolic-ref", "--short", "-q", "HEAD"], "git was not found."));
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateFeatureStore().SetCurrentAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitUnavailableException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task DropAsync_OutdatedOverrideSelectsFeature_ClearsOverride()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature);
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(FeatureId);
        var featureStore = _context.CreateFeatureStore();
        var store = _context.CreateStore();

        // Act
        var preview = await featureStore.PreviewDropAsync(store, FeatureId, TestContext.Current.CancellationToken);
        var result = await featureStore.DropAsync(store, FeatureId, TestContext.Current.CancellationToken);

        // Assert
        preview.OverrideAffected
            .Should().BeTrue();

        result.OverrideAffected
            .Should().BeTrue();

        _context.KitFileExists("features", "current-feature.json")
            .Should().BeFalse();
    }

    [Fact]
    public async Task DropAsync_OutdatedOverrideAndInvalidConfig_DeletesFolderAndLeavesOverride()
    {
        // Arrange
        // The outdated override needs config.json for the read policy; a broken config.json makes the override
        // unreadable, so it is left alone like any other broken override.
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature);
        _context.WriteKitFile("{", "config.json");
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().DropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.OverrideAffected
            .Should().BeFalse();

        _context.KitDirectoryExists("features", FeatureId)
            .Should().BeFalse();

        _context.KitFileExists("features", "current-feature.json")
            .Should().BeTrue();
    }

    [Fact]
    public async Task DropAsync_OutdatedOverrideWithStopPolicy_DeletesFolderAndLeavesOverride()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature);
        _context.WriteConfig(""", "outdatedFormatOnRead": "stop" """);
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().DropAsync(_context.CreateStore(), FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.OverrideAffected
            .Should().BeFalse();

        _context.KitDirectoryExists("features", FeatureId)
            .Should().BeFalse();

        _context.KitFileExists("features", "current-feature.json")
            .Should().BeTrue();
    }

    [Fact]
    public async Task DropAsync_DeleteAfterRenameFails_ThrowsAndClearsOverride()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.GetFeatureNames()).Returns([FeatureId]);
        A.CallTo(() => store.GetFiles(A<string>._)).Returns([$"features/{FeatureId}/feature.json"]);
        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(new VersionedReadResult<CurrentFeatureDocument>(
                new CurrentFeatureDocument { FeatureId = FeatureId }, []));
        A.CallTo(() => store.DeleteDirectory(A<string>._)).Throws(new IOException("The folder is locked."));

        // Act
        var act = () => _context.CreateFeatureStore().DropAsync(store, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The folder is locked.");

        A.CallTo(() => store.MoveDirectory($"features/{FeatureId}", A<string>.That.Matches(x => IsDroppingPath(x))))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => store.DeleteDirectory(A<string>.That.Matches(x => IsDroppingPath(x))))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => store.DeleteFile("features/current-feature.json"))
                .MustHaveHappenedOnceExactly());

        A.CallTo(() => store.DeleteDirectory($"features/{FeatureId}"))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task DropAsync_DeleteAfterRenameFails_FeatureNoLongerExistsAndHiddenLeftoverIsIgnored()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteFeature(OtherFeatureId, "Checkout");
        _context.WriteOverride(FeatureId);
        var realStore = _context.CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        A.CallTo(() => store.DeleteDirectory(A<string>._)).Throws(new IOException("The folder is locked."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.DropAsync(store, FeatureId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>();

        var list = await featureStore.ListAsync(realStore, TestContext.Current.CancellationToken);
        var show = () => featureStore.ShowAsync(realStore, FeatureId, TestContext.Current.CancellationToken);
        var current = await featureStore.GetCurrentAsync(realStore, TestContext.Current.CancellationToken);

        list.Features.Select(x => x.Id)
            .Should().Equal(OtherFeatureId);

        list.Warnings
            .Should().BeEmpty();

        await show.Should().ThrowAsync<FeatureNotFoundException>();

        current.IsResolved
            .Should().BeFalse();

        current.Warnings
            .Should().BeEmpty("the override was cleared although the hidden folder could not be deleted");

        Directory.EnumerateDirectories(Path.Combine(_context.RepositoryRootPath, ".shadekit", "features"),
                $".{FeatureId}.*.dropping")
            .Should().ContainSingle();
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(DirectoryNotFoundException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task DropAsync_RenameFails_ThrowsFeatureIoFailedExceptionAndChangesNothing(Type exceptionType)
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(FeatureId);
        var renameException = (Exception)Activator.CreateInstance(exceptionType, "The folder is locked.")!;
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._)).Throws(renameException);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateFeatureStore().DropAsync(store, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<FeatureIoFailedException>()).Which;

        exception.FeatureId
            .Should().Be(FeatureId);

        exception.FolderPath
            .Should().Be($".shadekit/features/{FeatureId}/");

        exception.Operation
            .Should().Be(FeatureIoFailedException.DropOperation);

        exception.InnerException
            .Should().BeSameAs(renameException);

        exception.Message
            .Should().Be($"Could not drop the feature '{FeatureId}', because its folder " +
                $"'.shadekit/features/{FeatureId}/' cannot be changed: The folder is locked.");

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        A.CallTo(() => store.DeleteDirectory(A<string>._))
            .MustNotHaveHappened();

        A.CallTo(() => store.DeleteFile(A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task DropAsync_RetryAfterRenameFailure_DropsFeatureAndClearsOverride()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(FeatureId);
        var failRename = true;
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .WhenArgumentsMatch(_ => failRename)
            .Throws(new IOException("The folder is locked."));
        var featureStore = _context.CreateFeatureStore();
        var firstAttempt = () => featureStore.DropAsync(store, FeatureId, TestContext.Current.CancellationToken);
        await firstAttempt.Should().ThrowAsync<FeatureIoFailedException>();
        failRename = false;

        // Act
        var result = await featureStore.DropAsync(store, FeatureId, TestContext.Current.CancellationToken);

        // Assert
        result.OverrideAffected
            .Should().BeTrue();

        Directory.EnumerateFileSystemEntries(Path.Combine(_context.RepositoryRootPath, ".shadekit", "features"))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task DropAsync_RenameFailsWithOtherException_RethrowsItUnwrapped()
    {
        // Arrange
        var store = CreateDropStore(FeatureId);
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .Throws(new InvalidOperationException("Unexpected."));

        // Act
        var act = () => _context.CreateFeatureStore().DropAsync(store, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unexpected.");

        A.CallTo(() => store.DeleteDirectory(A<string>._))
            .MustNotHaveHappened();

        A.CallTo(() => store.DeleteFile(A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task DropAsync_FeaturesFolderReadOnly_ThrowsFeatureIoFailedExceptionAndChangesNothing()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a folder read-only.");
        _context.WriteFeature(FeatureId);
        _context.WriteKitFile("# Login", "features", FeatureId, "request.md");
        _context.WriteOverride(FeatureId);
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        Exception? thrown;

        using (UnixFileModeScope.MakeReadOnly(Path.Combine(_context.RepositoryRootPath, ".shadekit", "features")))
        {
            // Act
            thrown = await Record.ExceptionAsync(() => featureStore.DropAsync(_context.CreateStore(), FeatureId,
                TestContext.Current.CancellationToken));
        }

        // Assert
        var exception = thrown
            .Should().BeOfType<FeatureIoFailedException>().Subject;

        exception.Operation
            .Should().Be(FeatureIoFailedException.DropOperation);

        exception.InnerException
            .Should().Match(x => x is IOException || x is UnauthorizedAccessException);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(FeatureId);
    }

    [Fact]
    public async Task DropAsync_ExistingFeature_RenamesFolderThenDeletesItAndListsOriginalPaths()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteKitFile("# Login", "features", FeatureId, "request.md");
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));

        // Act
        var result = await _context.CreateFeatureStore().DropAsync(store, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        result.Files
            .Should().Equal($".shadekit/features/{FeatureId}/feature.json",
                $".shadekit/features/{FeatureId}/request.md");

        A.CallTo(() => store.MoveDirectory($"features/{FeatureId}", A<string>.That.Matches(x => IsDroppingPath(x))))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => store.DeleteDirectory(A<string>.That.Matches(x => IsDroppingPath(x))))
                .MustHaveHappenedOnceExactly());

        Directory.EnumerateFileSystemEntries(Path.Combine(_context.RepositoryRootPath, ".shadekit", "features"))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task DropAsync_CanceledDuringRename_FinishesDropAndClearsOverride()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(FeatureId);
        using var cancellationSource = new CancellationTokenSource();
        var realStore = _context.CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .Invokes((string sourcePath, string destinationPath) =>
            {
                realStore.MoveDirectory(sourcePath, destinationPath);
                cancellationSource.Cancel();
            });

        // Act
        var result = await _context.CreateFeatureStore().DropAsync(store, FeatureId, cancellationSource.Token);

        // Assert
        result.OverrideAffected
            .Should().BeTrue();

        Directory.EnumerateFileSystemEntries(Path.Combine(_context.RepositoryRootPath, ".shadekit", "features"))
            .Should().BeEmpty("the rename is the point of no return, so a later cancellation must not stop the drop");
    }

    [Fact]
    public async Task DropAsync_Cancelled_DeletesNothing()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateFeatureStore().DropAsync(_context.CreateStore(), FeatureId,
            new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task DropAsync_CurrentFeature_LeavesNoCurrentFeatureAndSecondDropThrows()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(FeatureId);
        var featureStore = _context.CreateFeatureStore();
        var store = _context.CreateStore();

        // Act
        await featureStore.DropAsync(store, FeatureId, TestContext.Current.CancellationToken);

        var current = await featureStore.GetCurrentAsync(store, TestContext.Current.CancellationToken);
        var secondDrop = () => featureStore.DropAsync(store, FeatureId, TestContext.Current.CancellationToken);

        // Assert
        current
            .Should().BeEquivalentTo(new CurrentFeatureResult(null, null, null, []));

        await secondDrop.Should().ThrowAsync<FeatureNotFoundException>();
    }

    [Theory]
    [InlineData(nameof(FeatureStore.ShowAsync))]
    [InlineData(nameof(FeatureStore.SetCurrentAsync))]
    [InlineData(nameof(FeatureStore.PreviewDropAsync))]
    [InlineData(nameof(FeatureStore.DropAsync))]
    public async Task FeatureIdMethods_NullFeatureId_ThrowArgumentNullException(string methodName)
    {
        // Arrange
        var featureStore = _context.CreateFeatureStore();
        var store = _context.CreateStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        Func<Task> act = methodName switch
        {
            nameof(FeatureStore.ShowAsync) => () => featureStore.ShowAsync(store, null!, cancellationToken),
            nameof(FeatureStore.SetCurrentAsync) => () => featureStore.SetCurrentAsync(store, null!, cancellationToken),
            nameof(FeatureStore.PreviewDropAsync) => () =>
                featureStore.PreviewDropAsync(store, null!, cancellationToken),
            nameof(FeatureStore.DropAsync) => () => featureStore.DropAsync(store, null!, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unknown feature store method.")
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void FeaturePathsFor_ValidId_ReturnsRepositoryRelativePaths()
    {
        // Act
        var paths = FeaturePaths.For(FeatureId);

        // Assert
        paths
            .Should().Be(new FeaturePaths(".shadekit/features/gh-142-login-sso/",
                ".shadekit/features/gh-142-login-sso/feature.json", ".shadekit/features/gh-142-login-sso/request.md"));
    }

    [Theory]
    [MemberData(nameof(InvalidFeatureIds))]
    public void FeaturePathsFor_InvalidId_ThrowsInvalidFeatureIdException(string featureId)
    {
        // Act
        var act = () => FeaturePaths.For(featureId);

        // Assert
        act.Should().Throw<InvalidFeatureIdException>();
    }

    [Fact]
    public async Task DropAsync_DeleteAfterRenameAndOverrideClearingFail_RethrowsDeleteException()
    {
        // Arrange
        var store = CreateDropStore(FeatureId);
        A.CallTo(() => store.DeleteDirectory(A<string>._)).Throws(new IOException("The folder is locked."));
        A.CallTo(() => store.DeleteFile(A<string>._)).Throws(new UnauthorizedAccessException("Access denied."));

        // Act
        var act = () => _context.CreateFeatureStore().DropAsync(store, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The folder is locked.");

        A.CallTo(() => store.DeleteFile("features/current-feature.json"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DropAsync_DeleteAfterRenameFailsWithUnauthorizedAccess_ClearsOverrideAndRethrows()
    {
        // Arrange
        var store = CreateDropStore(FeatureId);
        A.CallTo(() => store.DeleteDirectory(A<string>._)).Throws(new UnauthorizedAccessException("Access denied."));

        // Act
        var act = () => _context.CreateFeatureStore().DropAsync(store, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        A.CallTo(() => store.DeleteFile("features/current-feature.json"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DropAsync_DeleteAfterRenameFailsAndOverrideSelectsOtherFeature_KeepsOverride()
    {
        // Arrange
        var store = CreateDropStore(OtherFeatureId);
        A.CallTo(() => store.DeleteDirectory(A<string>._)).Throws(new IOException("The folder is locked."));

        // Act
        var act = () => _context.CreateFeatureStore().DropAsync(store, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>();

        A.CallTo(() => store.DeleteFile(A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task DropAsync_OverrideDeleteFailsAfterFolderDeleted_ThrowsAndFeatureIsGone()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(FeatureId);
        var realStore = _context.CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        A.CallTo(() => store.DeleteFile(A<string>._)).Throws(new IOException("The file is locked."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.DropAsync(store, FeatureId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The file is locked.");

        _context.KitDirectoryExists("features", FeatureId)
            .Should().BeFalse();

        (await featureStore.GetCurrentAsync(realStore, TestContext.Current.CancellationToken)).Warnings
            .Should().Equal(new CurrentFeatureMissingWarning(FeatureId));
    }

    [Fact]
    public async Task GetCurrentAsync_OutdatedOverrideAndOutdatedConfigWithStopPolicy_ThrowsUpgradeNeededForConfig()
    {
        // Arrange
        _context.Registry =
            FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature, FormatIds.Config);
        _context.WriteConfig(""", "outdatedFormatOnRead": "stop" """);
        _context.WriteFeature(FeatureId);
        _context.WriteOverride(FeatureId);

        // Act
        var act = () => _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UpgradeNeededException>())
            .Which.FilePath
            .Should().Be(".shadekit/config.json");
    }

    [Fact]
    public async Task GetCurrentAsync_BrokenOverrideAndNoBranchFeature_ReturnsNotResolvedWithInvalidWarning()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverrideContent("{");

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsResolved
            .Should().BeFalse();

        result.Warnings
            .Should().ContainSingle()
            .Which.Should().BeOfType<CurrentFeatureInvalidWarning>();
    }

    [Fact]
    public async Task GetCurrentAsync_BranchNamesHiddenStagingFolder_DoesNotSelectIt()
    {
        // Arrange
        var stagingName = $".{FeatureId}.1a2b3c4d.tmp";
        _context.WriteFeature(stagingName);
        _context.SetBranch($"feature/{stagingName}");

        // Act
        var result = await _context.CreateFeatureStore().GetCurrentAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new CurrentFeatureResult(null, null, null, []));
    }

    [Fact]
    public async Task ListAsync_HiddenAndInvalidFoldersWithBrokenFeatureFile_SkipsThemWithoutReadingTheFile()
    {
        // Arrange
        _context.WriteKitFile("{", "features", ".gh-7-checkout.1a2b3c4d.tmp", "feature.json");
        _context.WriteKitFile("{", "features", "Draft", "feature.json");
        _context.WriteFeature(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Features.Select(x => x.Id)
            .Should().Equal(FeatureId);

        result.Warnings
            .Should().Equal(new FeatureFolderInvalidWarning("Draft"));
    }

    [Fact]
    public async Task ListAsync_MixedFolders_ReturnsOneWarningPerFolderInOrdinalOrder()
    {
        // Arrange
        _context.WriteFeature("b-copy", recordedId: FeatureId);
        _context.CreateFeatureFolder("c-empty");
        _context.CreateFeatureFolder("A-invalid");
        _context.CreateFeatureFolder(".hidden");
        _context.WriteFeature(FeatureId);

        // Act
        var result = await _context.CreateFeatureStore().ListAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Features.Select(x => x.Id)
            .Should().Equal("b-copy", FeatureId);

        result.Warnings
            .Should().Equal(new FeatureFolderInvalidWarning("A-invalid"),
                new FeatureIdMismatchWarning("b-copy", FeatureId), new FeatureFileMissingWarning("c-empty"));
    }

    [Fact]
    public async Task SetCurrentAsync_BrokenOverrideDeletedButWriteFails_ThrowsAndLeavesNoOverride()
    {
        // Arrange
        _context.WriteFeature(FeatureId);
        _context.WriteOverrideContent("{");
        // The store deletes the broken override for the replacement, then the write fails.
        var store = A.Fake<IKitStore>(options =>
            options.Wrapping(_context.CreateStoreFailingOverrideWrite(new IOException("The disk is full."))));

        // Act
        var act = () => _context.CreateFeatureStore().SetCurrentAsync(store, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The disk is full.");

        A.CallTo(() => store.WriteJsonAsync("features/current-feature.json", FormatIds.CurrentFeature,
                A<CurrentFeatureDocument>.That.Matches(x => x.FeatureId == FeatureId), FormatAccess.Replace,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        _context.KitFileExists("features", "current-feature.json")
            .Should().BeFalse();
    }

    private static IKitStore CreateDropStore(string overrideFeatureId)
    {
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.GetFeatureNames()).Returns([FeatureId]);
        A.CallTo(() => store.GetFiles(A<string>._)).Returns([]);
        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(new VersionedReadResult<CurrentFeatureDocument>(
                new CurrentFeatureDocument { FeatureId = overrideFeatureId }, []));

        return store;
    }

    private static bool IsDroppingPath(string kitRelativePath)
    {
        return kitRelativePath.StartsWith($"features/.{FeatureId}.", StringComparison.Ordinal)
               && kitRelativePath.EndsWith(".dropping", StringComparison.Ordinal);
    }
}
