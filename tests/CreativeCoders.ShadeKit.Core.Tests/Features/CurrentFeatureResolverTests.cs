using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Formats;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public sealed class CurrentFeatureResolverTests : IDisposable
{
    private const string CurrentFeatureDisplayPath = ".shadekit/features/current-feature.json";

    private readonly TempKitDirectory _repository = new();

    private readonly IGitRepositoryInfo _gitRepositoryInfo = A.Fake<IGitRepositoryInfo>();

    public void Dispose()
    {
        _repository.Dispose();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC4")]
    public async Task ResolveAsync_OverrideOnlyAndBranchMatchesNoFeature_ReturnsOverrideFeature()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        SetBranch("main");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution("gh-142-login-sso", CurrentFeatureOrigin.Override,
                []));
    }

    [Fact]
    [Trait("Requirement", "FP03-AC5")]
    public async Task ResolveAsync_BranchAndOverrideSelectDifferentFeatures_ReturnsBranchFeatureWithMismatchWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        CreateFeature("gh-7-checkout");
        WriteOverride("gh-7-checkout");
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.FeatureId
            .Should().Be("gh-142-login-sso");

        resolution.Origin
            .Should().Be(CurrentFeatureOrigin.Branch);

        resolution.Warnings
            .Should().Equal(new CurrentFeatureMismatchWarning("gh-7-checkout", "gh-142-login-sso"));

    }

    [Fact]
    [Trait("Requirement", "FP03-AC6")]
    public async Task ResolveAsync_NoBranchMatchAndNoOverride_ReturnsNotResolved()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        SetBranch("feature/other-work");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution(null, null, []));

        resolution.IsResolved
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC6")]
    public async Task ResolveAsync_NoFeaturesDirectory_ReturnsNotResolved()
    {
        // Arrange
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution(null, null, []));
    }

    [Theory]
    [InlineData("gh-142-login-sso")]
    [InlineData("feature/gh-142-login-sso")]
    [InlineData("users/jane/feature/gh-142-login-sso")]
    public async Task ResolveAsync_BranchEndsWithExistingFeatureId_ReturnsBranchFeature(string branchName)
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        SetBranch(branchName);
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution("gh-142-login-sso", CurrentFeatureOrigin.Branch,
                []));

        resolution.IsResolved
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("feature/GH-142-Login-SSO")]
    [InlineData("feature/gh_142")]
    [InlineData("feature/gh-142-")]
    [InlineData("feature/")]
    [InlineData("feature/..")]
    public async Task ResolveAsync_BranchCandidateIsInvalidId_IgnoresBranchWithoutWarning(string branchName)
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        SetBranch(branchName);
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution("gh-142-login-sso", CurrentFeatureOrigin.Override,
                []));
    }

    [Fact]
    public async Task ResolveAsync_BranchCandidateIsInvalidId_DoesNotCheckAnyFeaturePath()
    {
        // Arrange
        var store = CreateFakeStore();
        SetBranch("feature/..");

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.IsResolved
            .Should().BeFalse();

        A.CallTo(() => store.DirectoryExists(A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ResolveAsync_BranchCandidateWithoutFolder_IgnoresBranchWithoutWarning()
    {
        // Arrange
        CreateFeature("gh-7-checkout");
        WriteOverride("gh-7-checkout");
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution("gh-7-checkout", CurrentFeatureOrigin.Override,
                []));
    }

    [Fact]
    public async Task ResolveAsync_BranchCandidateIsFileInsteadOfFolder_IgnoresBranch()
    {
        // Arrange
        _repository.WriteFile("{}", ".shadekit", "features", "gh-142-login-sso");
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.IsResolved
            .Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_DetachedHead_ReturnsOverrideFeatureWithoutWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        SetBranch(null);
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution("gh-142-login-sso", CurrentFeatureOrigin.Override,
                []));
    }

    [Fact]
    public async Task ResolveAsync_DetachedHeadAndNoOverride_ReturnsNotResolved()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        SetBranch(null);
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution(null, null, []));
    }

    [Fact]
    public async Task ResolveAsync_OverrideToMissingFolder_ReturnsNotResolvedWithMissingWarning()
    {
        // Arrange
        WriteOverride("gh-142-login-sso");
        SetBranch("main");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution(null, null,
                [new CurrentFeatureMissingWarning("gh-142-login-sso")]),
                options => options.PreferringRuntimeMemberTypes());
    }

    [Fact]
    public async Task ResolveAsync_OverrideToMissingFolder_LeavesOverrideFileUnchanged()
    {
        // Arrange
        var overridePath = WriteOverride("gh-142-login-sso");
        var originalContent = await File.ReadAllBytesAsync(overridePath, TestContext.Current.CancellationToken);
        SetBranch("main");
        var store = CreateStore();

        // Act
        await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        (await File.ReadAllBytesAsync(overridePath, TestContext.Current.CancellationToken))
            .Should().Equal(originalContent);
    }

    [Fact]
    public async Task ResolveAsync_OverrideToMissingFolderAndBranchFeature_ReturnsBranchFeatureWithMissingWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-7-checkout");
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution("gh-142-login-sso", CurrentFeatureOrigin.Branch,
                [new CurrentFeatureMissingWarning("gh-7-checkout")]),
                options => options.PreferringRuntimeMemberTypes());
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("x/../x")]
    [InlineData("X")]
    [InlineData("")]
    public async Task ResolveAsync_OverrideWithInvalidId_ReturnsNotResolvedWithMissingWarning(string overrideId)
    {
        // Arrange
        CreateFeature("x");
        _repository.CreateDirectory(".shadekit", "x");
        WriteOverride(overrideId);
        SetBranch("main");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution(null, null,
                [new CurrentFeatureMissingWarning(overrideId)]),
                options => options.PreferringRuntimeMemberTypes());
    }

    [Fact]
    public async Task ResolveAsync_OverrideWithInvalidId_DoesNotCheckAnyFeaturePath()
    {
        // Arrange
        var store = CreateFakeStore();
        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>("features/current-feature.json",
                FormatIds.CurrentFeature, FormatAccess.Read, A<CancellationToken>._))
            .Returns(new VersionedReadResult<CurrentFeatureDocument>(new CurrentFeatureDocument { FeatureId = "../x" },
                []));

        SetBranch(null);

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.Warnings
            .Should().Equal(new CurrentFeatureMissingWarning("../x"));

        A.CallTo(() => store.DirectoryExists(A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ResolveAsync_BranchAndOverrideSelectSameFeature_ReturnsBranchFeatureWithoutWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution("gh-142-login-sso", CurrentFeatureOrigin.Branch,
                []));
    }

    [Fact]
    public async Task ResolveAsync_OutdatedOverride_ReturnsOverrideFeatureWithFormatWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        SetBranch("main");
        var store = CreateStore(CreateRegistryWithCurrentFeatureVersion2());

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution("gh-142-login-sso", CurrentFeatureOrigin.Override,
                [new FormatWarning(FormatWarningKind.UpgradeNeeded, CurrentFeatureDisplayPath, 1, 2)]),
                options => options.PreferringRuntimeMemberTypes());
    }

    [Fact]
    public async Task ResolveAsync_OutdatedOverrideAndBranchFeature_ReturnsBranchFeatureWithFormatWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore(CreateRegistryWithCurrentFeatureVersion2());

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution("gh-142-login-sso", CurrentFeatureOrigin.Branch,
                [new FormatWarning(FormatWarningKind.UpgradeNeeded, CurrentFeatureDisplayPath, 1, 2)]),
                options => options.PreferringRuntimeMemberTypes());
    }

    [Fact]
    public async Task ResolveAsync_OutdatedOverrideWithStopSetting_IgnoresOverrideWithInvalidWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        _repository.WriteFile(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "outdatedFormatOnRead": "stop" }""",
            ".shadekit", "config.json");

        SetBranch("main");
        var store = CreateStore(CreateRegistryWithCurrentFeatureVersion2());

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.IsResolved
            .Should().BeFalse();

        var problem = GetSingleInvalidWarningProblem<UpgradeNeededException>(resolution);

        problem.FilePath
            .Should().Be(CurrentFeatureDisplayPath);

        problem.FileVersion
            .Should().Be(1);

        problem.CurrentVersion
            .Should().Be(2);
    }

    [Fact]
    public async Task ResolveAsync_OutdatedOverrideWithStopSettingAndBranchFeature_ReturnsBranchFeatureWithWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        _repository.WriteFile(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "outdatedFormatOnRead": "stop" }""",
            ".shadekit", "config.json");

        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore(CreateRegistryWithCurrentFeatureVersion2());

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.FeatureId
            .Should().Be("gh-142-login-sso");

        resolution.Origin
            .Should().Be(CurrentFeatureOrigin.Branch);

        GetSingleInvalidWarningProblem<UpgradeNeededException>(resolution);
    }

    [Fact]
    public async Task ResolveAsync_OutdatedOverrideToOtherFeature_ReturnsMismatchAndFormatWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        CreateFeature("gh-7-checkout");
        WriteOverride("gh-7-checkout");
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore(CreateRegistryWithCurrentFeatureVersion2());

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.FeatureId
            .Should().Be("gh-142-login-sso");

        resolution.Origin
            .Should().Be(CurrentFeatureOrigin.Branch);

        resolution.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, CurrentFeatureDisplayPath, 1, 2),
                new CurrentFeatureMismatchWarning("gh-7-checkout", "gh-142-login-sso"));
    }

    [Fact]
    public async Task ResolveAsync_OutdatedOverrideAndInvalidConfig_ThrowsInvalidKitFileExceptionForConfig()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        _repository.WriteFile("not json", ".shadekit", "config.json");
        SetBranch("main");
        var store = CreateStore(CreateRegistryWithCurrentFeatureVersion2());

        // Act
        var act = () => CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>())
            .Which.FilePath
            .Should().Be(".shadekit/config.json");
    }

    [Fact]
    public async Task ResolveAsync_OutdatedOverrideAndTooNewConfig_ThrowsFormatTooNewExceptionForConfig()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        _repository.WriteFile("""{ "schemaVersion": 99, "minEngineVersion": "99.0.0" }""", ".shadekit",
            "config.json");

        SetBranch("main");
        var store = CreateStore(CreateRegistryWithCurrentFeatureVersion2());

        // Act
        var act = () => CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<FormatTooNewException>())
            .Which.FilePath
            .Should().Be(".shadekit/config.json");
    }

    [Fact]
    public async Task ResolveAsync_OverrideFeaturePathIsFile_ReturnsNotResolvedWithMissingWarning()
    {
        // Arrange
        _repository.WriteFile("{}", ".shadekit", "features", "gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        SetBranch("main");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution(null, null,
                [new CurrentFeatureMissingWarning("gh-142-login-sso")]),
                options => options.PreferringRuntimeMemberTypes());
    }

    [Fact]
    public async Task ResolveAsync_OverrideIdWithTrailingLineBreak_ReturnsNotResolvedWithMissingWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso\\n");
        SetBranch("main");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution(null, null,
                [new CurrentFeatureMissingWarning("gh-142-login-sso\n")]),
                options => options.PreferringRuntimeMemberTypes());
    }

    [Fact]
    public async Task ResolveAsync_OverrideWithInvalidIdAndBranchFeature_ReturnsBranchFeatureWithOnlyMissingWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("../gh-142-login-sso");
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution
            .Should().BeEquivalentTo(new CurrentFeatureResolution("gh-142-login-sso", CurrentFeatureOrigin.Branch,
                [new CurrentFeatureMissingWarning("../gh-142-login-sso")]),
                options => options.PreferringRuntimeMemberTypes());
    }

    [Fact]
    public async Task ResolveAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        A.CallTo(() => _gitRepositoryInfo.GetCurrentBranchAsync(A<string>._, A<CancellationToken>._))
            .ThrowsAsync(call => new OperationCanceledException(call.GetArgument<CancellationToken>(1)));

        var store = CreateStore();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = () => CreateResolver().ResolveAsync(store, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "featureId": null }""")]
    public async Task ResolveAsync_InvalidOverrideAndBranchFeature_ReturnsBranchFeatureWithInvalidWarning(
        string content)
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        _repository.WriteFile(content, ".shadekit", "features", "current-feature.json");
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore();
        var snapshot = _repository.TakeSnapshot();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.FeatureId
            .Should().Be("gh-142-login-sso");

        resolution.Origin
            .Should().Be(CurrentFeatureOrigin.Branch);

        GetSingleInvalidWarningProblem<InvalidKitFileException>(resolution).FilePath
            .Should().Be(CurrentFeatureDisplayPath);

        _repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task ResolveAsync_InvalidOverride_ReturnsNotResolvedWithReasonOfInvalidFile()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        _repository.WriteFile("""{ "minEngineVersion": "0.1.0", "featureId": "gh-142-login-sso" }""",
            ".shadekit", "features", "current-feature.json");
        SetBranch("main");
        var store = CreateStore();

        var expectedReason = await GetInvalidFileReasonAsync(store);

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.IsResolved
            .Should().BeFalse();

        GetSingleInvalidWarningProblem<InvalidKitFileException>(resolution).Reason
            .Should().Be(expectedReason);
    }

    [Fact]
    public async Task ResolveAsync_OverrideTooNew_ReturnsNotResolvedWithInvalidWarning()
    {
        // Arrange
        _repository.WriteFile("""{ "schemaVersion": 99, "minEngineVersion": "99.0.0", "featureId": "x" }""",
            ".shadekit", "features", "current-feature.json");

        SetBranch("main");
        var store = CreateStore();
        var snapshot = _repository.TakeSnapshot();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.IsResolved
            .Should().BeFalse();

        var problem = GetSingleInvalidWarningProblem<FormatTooNewException>(resolution);

        problem.FileVersion
            .Should().Be(99);

        problem.SupportedVersion
            .Should().Be(1);

        _repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task ResolveAsync_OverrideTooNewAndBranchFeature_ReturnsBranchFeatureWithInvalidWarning()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        _repository.WriteFile("""{ "schemaVersion": 99, "minEngineVersion": "99.0.0", "featureId": "x" }""",
            ".shadekit", "features", "current-feature.json");

        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore();

        // Act
        var resolution = await CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        resolution.FeatureId
            .Should().Be("gh-142-login-sso");

        GetSingleInvalidWarningProblem<FormatTooNewException>(resolution);
    }

    [Fact]
    public async Task ResolveAsync_GitUnavailable_ThrowsGitUnavailableException()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        WriteOverride("gh-142-login-sso");
        var exception = new GitUnavailableException(["symbolic-ref", "--short", "-q", "HEAD"], "git not found.");
        A.CallTo(() => _gitRepositoryInfo.GetCurrentBranchAsync(A<string>._, A<CancellationToken>._))
            .ThrowsAsync(exception);

        var store = CreateStore();

        // Act
        var act = () => CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GitUnavailableException>())
            .Which
            .Should().BeSameAs(exception);
    }

    [Fact]
    public async Task ResolveAsync_Store_RunsGitInParentDirectoryOfKitRootWithCancellationToken()
    {
        // Arrange
        SetBranch("main");
        var store = CreateStore();
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await CreateResolver().ResolveAsync(store, cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => _gitRepositoryInfo.GetCurrentBranchAsync(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(_repository.RootPath)),
                cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ResolveAsync_Store_ReadsOverrideWithReadAccessAndCancellationToken()
    {
        // Arrange
        var store = CreateFakeStore();
        SetBranch(null);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await CreateResolver().ResolveAsync(store, cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>("features/current-feature.json",
                FormatIds.CurrentFeature, FormatAccess.Read, cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ResolveAsync_NullStore_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CreateResolver().ResolveAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("store");
    }

    [Fact]
    public async Task ResolveAsync_KitRootWithoutParentDirectory_ThrowsArgumentException()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.RootPath).Returns(Path.GetPathRoot(Path.GetTempPath())!);

        // Act
        var act = () => CreateResolver().ResolveAsync(store, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName("store");
    }

    [Theory]
    [InlineData("feature/gh-142-login-sso", "gh-142-login-sso")]
    [InlineData("main", null)]
    [InlineData(null, null)]
    public async Task ResolveBranchFeatureIdAsync_Branch_ReturnsBranchFeatureIdOrNull(string? branchName,
        string? expectedFeatureId)
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        SetBranch(branchName);
        var store = CreateStore();

        // Act
        var featureId = await CreateResolver().ResolveBranchFeatureIdAsync(store,
            TestContext.Current.CancellationToken);

        // Assert
        featureId
            .Should().Be(expectedFeatureId);
    }

    [Fact]
    public async Task ResolveBranchFeatureIdAsync_InvalidOverride_DoesNotReadOverride()
    {
        // Arrange
        CreateFeature("gh-142-login-sso");
        _repository.WriteFile("not json", ".shadekit", "features", "current-feature.json");
        SetBranch("feature/gh-142-login-sso");
        var store = CreateStore();

        // Act
        var featureId = await CreateResolver().ResolveBranchFeatureIdAsync(store,
            TestContext.Current.CancellationToken);

        // Assert
        featureId
            .Should().Be("gh-142-login-sso");
    }

    [Fact]
    public async Task ResolveBranchFeatureIdAsync_Store_RunsGitWithCancellationTokenAndDoesNotReadOverride()
    {
        // Arrange
        var store = CreateFakeStore();
        SetBranch("feature/gh-142-login-sso");
        A.CallTo(() => store.DirectoryExists("features/gh-142-login-sso")).Returns(true);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var featureId = await CreateResolver().ResolveBranchFeatureIdAsync(store, cancellationTokenSource.Token);

        // Assert
        featureId
            .Should().Be("gh-142-login-sso");

        A.CallTo(() => _gitRepositoryInfo.GetCurrentBranchAsync(_repository.RootPath, cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("feature/..")]
    [InlineData("feature/")]
    [InlineData("feature/GH-142")]
    public async Task ResolveBranchFeatureIdAsync_InvalidCandidate_ReturnsNullWithoutCheckingAnyPath(
        string branchName)
    {
        // Arrange
        var store = CreateFakeStore();
        SetBranch(branchName);

        // Act
        var featureId = await CreateResolver().ResolveBranchFeatureIdAsync(store,
            TestContext.Current.CancellationToken);

        // Assert
        featureId
            .Should().BeNull();

        A.CallTo(() => store.DirectoryExists(A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ResolveBranchFeatureIdAsync_GitUnavailable_ThrowsGitUnavailableException()
    {
        // Arrange
        A.CallTo(() => _gitRepositoryInfo.GetCurrentBranchAsync(A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new GitUnavailableException(["symbolic-ref"], "git not found."));

        var store = CreateStore();

        // Act
        var act = () => CreateResolver().ResolveBranchFeatureIdAsync(store, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitUnavailableException>();
    }

    [Fact]
    public async Task ResolveBranchFeatureIdAsync_NullStore_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CreateResolver().ResolveBranchFeatureIdAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("store");
    }

    [Fact]
    public void Ctor_NullGitRepositoryInfo_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CurrentFeatureResolver(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitRepositoryInfo");
    }

    private static TProblem GetSingleInvalidWarningProblem<TProblem>(CurrentFeatureResolution resolution)
        where TProblem : KitFileFormatException
    {
        return resolution.Warnings
            .Should().ContainSingle()
            .Which.Should().BeOfType<CurrentFeatureInvalidWarning>()
            .Which.Problem.Should().BeOfType<TProblem>()
            .Subject;
    }

    private static async Task<string> GetInvalidFileReasonAsync(KitStore store)
    {
        try
        {
            await store.ReadJsonAsync<CurrentFeatureDocument>("features/current-feature.json",
                FormatIds.CurrentFeature, FormatAccess.Read, TestContext.Current.CancellationToken);
        }
        catch (InvalidKitFileException exception)
        {
            return exception.Reason;
        }

        throw new InvalidOperationException("The override is expected to be invalid.");
    }

    private CurrentFeatureResolver CreateResolver()
    {
        return new CurrentFeatureResolver(_gitRepositoryInfo);
    }

    private KitStore CreateStore(FormatRegistry? formatRegistry = null)
    {
        var kitStorePath = _repository.CreateDirectory(".shadekit");
        var registry = formatRegistry ?? FormatRegistry.CreateDefault();

        return new KitStore(kitStorePath, new VersionedJsonSerializer(registry), registry);
    }

    private IKitStore CreateFakeStore()
    {
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.RootPath).Returns(Path.Combine(_repository.RootPath, ".shadekit"));
        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns((VersionedReadResult<CurrentFeatureDocument>?)null);

        return store;
    }

    private static FormatRegistry CreateRegistryWithCurrentFeatureVersion2()
    {
        return new FormatRegistry(
        [
            new FormatDefinition(FormatIds.Config, SampleFormat.CreateMinEngineVersions(1)),
            new FormatDefinition(FormatIds.CurrentFeature, SampleFormat.CreateMinEngineVersions(2),
                [new NoOpMigration(FormatIds.CurrentFeature, 1)])
        ]);
    }

    private void CreateFeature(string featureId)
    {
        _repository.CreateDirectory(".shadekit", "features", featureId);
    }

    private string WriteOverride(string featureId)
    {
        return _repository.WriteFile(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "featureId": "{{featureId}}" }""",
            ".shadekit", "features", "current-feature.json");
    }

    private void SetBranch(string? branchName)
    {
        A.CallTo(() => _gitRepositoryInfo.GetCurrentBranchAsync(A<string>._, A<CancellationToken>._))
            .Returns(branchName);
    }
}
