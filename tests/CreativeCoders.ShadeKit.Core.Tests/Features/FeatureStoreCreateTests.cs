using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Core.Tests.Store;
using CreativeCoders.ShadeKit.Core.Trackers;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public sealed class FeatureStoreCreateTests : IDisposable
{
    private const string IssueUrl = "https://github.com/acme/shop/issues/142";

    private const string IssueFeatureId = "gh-142-login-sso";

    private const string WebUrl = "https://example.com/requests/login-sso";

    // The request template of earlier versions; feature new ignores it.
    private const string ObsoleteRequestTemplate = "# Request: {{feature-title}}\n\n{{original input, unchanged}}\n";

    private readonly FeatureStoreTestContext _context = new FeatureStoreTestContext();

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC1")]
    public async Task CreateAsync_IssueUrlWithTitle_CreatesIssueBasedFeatureFolderAndFeatureFile()
    {
        // Arrange
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(IssueFeatureId);

        result.Paths
            .Should().Be(new FeaturePaths(".shadekit/features/gh-142-login-sso/",
                ".shadekit/features/gh-142-login-sso/feature.json", ".shadekit/features/gh-142-login-sso/request.md"));

        result.SuggestedBranch
            .Should().Be("feature/gh-142-login-sso");

        result.Warnings
            .Should().BeEmpty();

        var feature = await _context.ReadFeatureFileAsync(IssueFeatureId);

        feature.Id
            .Should().Be(IssueFeatureId);

        feature.Title
            .Should().Be("Login SSO");

        feature.Profile
            .Should().Be(WorkflowProfiles.Feature);

        feature.Source.Kind
            .Should().Be(FeatureSourceKind.Url);

        feature.Source.Value
            .Should().Be(IssueUrl);

        feature.CreatedAt
            .Should().Be(DateTimeOffset.Parse(FeatureStoreTestContext.LocalCreatedAt,
                System.Globalization.CultureInfo.InvariantCulture));

        feature.CreatedAt.Offset
            .Should().Be(TimeSpan.FromHours(2));

        feature.Status
            .Should().Be(FeatureStatus.Active);

        _context.KitFileExists("features", IssueFeatureId, "request.md")
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC2")]
    public async Task CreateAsync_PlainText_CreatesTimeBasedFeature()
    {
        // Arrange
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest("Login SSO\nUsers sign in with the company account."),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(FeatureStoreTestContext.TimeBasedIdPrefix + "login-sso");

        result.Feature.Source.Kind
            .Should().Be(FeatureSourceKind.Text);

        _context.KitDirectoryExists("features", FeatureStoreTestContext.TimeBasedIdPrefix + "login-sso")
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC3")]
    public async Task CreateAsync_FeatureIdExists_ThrowsFeatureAlreadyExistsExceptionAndChangesNothing()
    {
        // Arrange
        _context.WriteFeature(IssueFeatureId, title: "Original");
        _context.WriteOverride(IssueFeatureId);
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<FeatureAlreadyExistsException>())
            .Which.FeatureId
            .Should().Be(IssueFeatureId);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC4")]
    public async Task CreateAsync_BranchMatchesNoFeature_NewFeatureIsCurrentFromOverride()
    {
        // Arrange
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        _context.SetBranch("feature/unrelated-work");
        var featureStore = _context.CreateFeatureStore();
        var store = _context.CreateStore();

        // Act
        await featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        var current = await featureStore.GetCurrentAsync(store, TestContext.Current.CancellationToken);

        // Assert
        current.Feature?.Id
            .Should().Be(IssueFeatureId);

        current.Origin
            .Should().Be(CurrentFeatureOrigin.Override);

        current.Paths
            .Should().Be(FeaturePaths.For(IssueFeatureId));

        current.Warnings
            .Should().BeEmpty();

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(IssueFeatureId);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC9")]
    public async Task CreateAsync_GitIdentityConfigured_RecordsGitUserAsCreatedBy()
    {
        // Arrange
        A.CallTo(() => _context.GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .Returns(new GitIdentity("Max Mustermann", "max@example.org"));
        var featureStore = _context.CreateFeatureStore();

        // Act
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        var feature = await _context.ReadFeatureFileAsync(IssueFeatureId);

        feature.CreatedBy.Name
            .Should().Be("Max Mustermann");

        feature.CreatedBy.Email
            .Should().Be("max@example.org");

        A.CallTo(() => _context.GitRepositoryInfo.GetIdentityAsync(_context.RepositoryRootPath,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC11")]
    public async Task CreateAsync_NewFeature_CurrentFeatureHasStatusActive()
    {
        // Arrange
        var featureStore = _context.CreateFeatureStore();
        var store = _context.CreateStore();

        // Act
        await featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        var current = await featureStore.GetCurrentAsync(store, TestContext.Current.CancellationToken);

        // Assert
        current.Feature?.Status
            .Should().Be(FeatureStatus.Active);
    }

    [Fact]
    public async Task CreateAsync_NoConfig_UsesDefaultProfileAndGitHubTracker()
    {
        // Arrange
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Profile
            .Should().Be(WorkflowProfiles.Feature);

        result.Feature.Id
            .Should().Be(IssueFeatureId);
    }

    [Fact]
    public async Task CreateAsync_NoExplicitProfile_UsesDefaultProfileOfConfig()
    {
        // Arrange
        _context.WriteConfig(""", "defaultProfile": "bugfix" """);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest("Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Profile
            .Should().Be(WorkflowProfiles.Bugfix);

        (await _context.ReadFeatureFileAsync(result.Feature.Id)).Profile
            .Should().Be(WorkflowProfiles.Bugfix);
    }

    [Fact]
    public async Task CreateAsync_ExplicitProfile_OverridesDefaultProfileOfConfig()
    {
        // Arrange
        _context.WriteConfig(""", "defaultProfile": "bugfix" """);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest("Login SSO", profile: WorkflowProfiles.Quick), TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Profile
            .Should().Be(WorkflowProfiles.Quick);
    }

    [Fact]
    public async Task CreateAsync_TrackerFromConfig_CreatesIssueBasedIdWithConfiguredPrefix()
    {
        // Arrange
        _context.WriteConfig(""", "trackers": { "git.example.com": "ex" } """);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest("https://git.example.com/acme/shop/-/issues/7", "Checkout"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be("ex-7-checkout");
    }

    [Fact]
    public async Task CreateAsync_UnknownExplicitProfile_ThrowsUnknownProfileExceptionAndChangesNothing()
    {
        // Arrange
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest("Login SSO", profile: "hotfix"), TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UnknownProfileException>())
            .Which.Profile
            .Should().Be("hotfix");

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_SourceIsDevZero_ThrowsInvalidFeatureSourceExceptionAndChangesNothing()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "/dev/zero is a Unix device.");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest("/dev/zero"),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.Reason
            .Should().StartWith("The file is larger than 1,048,576 bytes (1 MiB)");

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_SourceFileLargerThanMaximumSize_ThrowsInvalidFeatureSourceExceptionAndChangesNothing()
    {
        // Arrange
        var filePath = _context.Repository.WriteFile(
            "# Login SSO\n" + new string('a', FeatureIdFactory.MaxSourceFileSize), "request.md");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(filePath),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.Reason
            .Should().StartWith("The file is larger than 1,048,576 bytes (1 MiB)");

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task CreateAsync_SourceFileCannotBeRead_ThrowsInvalidFeatureSourceExceptionAndChangesNothing()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        // The file is outside the repository, because the snapshot cannot read an unreadable file.
        using var outside = new TempKitDirectory();
        var filePath = outside.WriteFile("# Login SSO\n", "request.md");
        var originalMode = File.GetUnixFileMode(filePath);
        File.SetUnixFileMode(filePath, UnixFileMode.None);

        try
        {
            Assert.SkipWhen(CanRead(filePath),
                $"'{filePath}' is readable without permissions, for example because the tests run as root.");
            var snapshot = _context.Repository.TakeSnapshot();
            var featureStore = _context.CreateFeatureStore();

            // Act
            var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(filePath),
                TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<InvalidFeatureSourceException>();

            _context.Repository.TakeSnapshot()
                .Should().BeEquivalentTo(snapshot);
        }
        finally
        {
            File.SetUnixFileMode(filePath, originalMode);
        }
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x23, 0x00, 0x20, 0x00, 0x4C, 0x00 })]
    [InlineData(new byte[] { 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x23 })]
    [InlineData(new byte[] { 0x23, 0x20, 0x4C, 0x00 })]
    [InlineData(new byte[] { 0x23, 0x20, 0x4C, 0xE4, 0x6E, 0x67, 0x65 })]
    public async Task CreateAsync_SourceFileIsNotUtf8_ThrowsInvalidFeatureSourceExceptionAndChangesNothing(
        byte[] content)
    {
        // Arrange
        _context.Repository.WriteFileBytes(content, "docs", "request.md");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest("docs/request.md", "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidFeatureSourceException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [InlineData("Login\nSSO")]
    [InlineData("Login\rSSO")]
    [InlineData("Login\tSSO")]
    public async Task CreateAsync_TitleWithControlCharacter_ThrowsInvalidFeatureTitleExceptionAndChangesNothing(
        string title)
    {
        // Arrange
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, title),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidFeatureTitleException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_UnknownDefaultProfileInConfig_ThrowsUnknownProfileException()
    {
        // Arrange
        _context.WriteConfig(""", "defaultProfile": "hotfix" """);
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest("Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UnknownProfileException>())
            .Which.Profile
            .Should().Be("hotfix");

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_InvalidConfig_ThrowsInvalidKitFileException()
    {
        // Arrange
        _context.WriteKitFile("{", "config.json");
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest("Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidKitFileException>();

        _context.KitDirectoryExists("features")
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("https://github.com/acme/shop/issues/142")]
    [InlineData("Login SSO")]
    public async Task CreateAsync_InvalidTrackerPrefixInConfig_ThrowsInvalidKitFileExceptionForConfigAndChangesNothing(
        string source)
    {
        // Arrange
        _context.WriteConfig(""", "trackers": { "github.com": "GH" } """);
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(source, "Login"),
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidKitFileException>()).Which;

        exception.FilePath
            .Should().Be(".shadekit/config.json");

        exception.Reason
            .Should().Contain("The id prefix 'GH' of host 'github.com'");

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": 99, "minEngineVersion": "99.0.0", "featureId": "x" }""")]
    public async Task CreateAsync_BrokenOverride_CreatesFeatureAndReplacesOverride(string overrideContent)
    {
        // Arrange
        _context.WriteOverrideContent(overrideContent);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(IssueFeatureId);

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(IssueFeatureId);
    }

    [Fact]
    public async Task CreateAsync_GitIdentityMissing_ThrowsAndWritesNothing()
    {
        // Arrange
        A.CallTo(() => _context.GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .Throws(new GitIdentityNotConfiguredException(_context.RepositoryRootPath, ["user.email"]));
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitIdentityNotConfiguredException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_GitUnavailable_ThrowsAndWritesNothing()
    {
        // Arrange
        A.CallTo(() => _context.GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .Throws(new GitUnavailableException(["config", "user.name"], "git was not found."));
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitUnavailableException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [InlineData(IssueUrl, null)]
    [InlineData(IssueUrl, "Login SSO")]
    [InlineData(WebUrl, "Login SSO")]
    public async Task CreateAsync_GitIdentityMissing_ThrowsBeforeSourceContentIsRead(string source, string? title)
    {
        // Arrange
        A.CallTo(() => _context.GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .Throws(new GitIdentityNotConfiguredException(_context.RepositoryRootPath, ["user.name"]));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(source, title),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitIdentityNotConfiguredException>();

        A.CallTo(_context.TrackerIssueReader)
            .MustNotHaveHappened();

        A.CallTo(_context.UrlContentReader)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_GitIdentityMissingAndSourceUnusable_ThrowsGitIdentityNotConfiguredException()
    {
        // Arrange
        A.CallTo(() => _context.GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .Throws(new GitIdentityNotConfiguredException(_context.RepositoryRootPath, ["user.email"]));
        A.CallTo(() => _context.TrackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(new TrackerIssueReadFailedException(new Uri(IssueUrl), "gh exited with the exit code 1."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitIdentityNotConfiguredException>();
    }

    [Theory]
    [InlineData(IssueUrl, null)]
    [InlineData(IssueUrl, "Login SSO")]
    [InlineData(WebUrl, "Login SSO")]
    public async Task CreateAsync_OverrideCannotBeRead_ThrowsBeforeSourceContentIsRead(string source, string? title)
    {
        // Arrange
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        var exception = new IOException("The override cannot be read.");
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>(KitStorePaths.CurrentFeatureFilePath,
                A<string>._, A<FormatAccess>._, A<CancellationToken>._))
            .ThrowsAsync(exception);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(source, title),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .Which.Should().BeSameAs(exception);

        A.CallTo(_context.TrackerIssueReader)
            .MustNotHaveHappened();

        A.CallTo(_context.UrlContentReader)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_RequestCannotBeWritten_RemovesFeatureFolderAndWritesNoOverride()
    {
        // Arrange
        var resolver = A.Fake<ILineEndingResolver>();
        A.CallTo(() => resolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .ReturnsLazily((RepositoryFile file, CancellationToken _) =>
                file.GitPath.EndsWith("/request.md", StringComparison.Ordinal)
                    ? throw new InvalidOperationException("The request cannot be written.")
                    : new LineEndingResolution(LineEnding.Lf, []));
        _context.LineEndingResolver = resolver;
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The request cannot be written.");

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_CancelledWhileStaging_DeletesOnlyStagingFolderAndRethrows()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var resolver = A.Fake<ILineEndingResolver>();
        A.CallTo(() => resolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .ReturnsLazily((RepositoryFile file, CancellationToken _) =>
            {
                if (file.GitPath.EndsWith("/request.md", StringComparison.Ordinal))
                {
                    // feature.json exists in the staging folder at this point; the feature folder does not exist yet.
                    GetStagingDirectories()
                        .Should().ContainSingle()
                        .Which.Should().Match(x => File.Exists(Path.Combine(x, "feature.json")));

                    _context.KitDirectoryExists("features", IssueFeatureId)
                        .Should().BeFalse();

                    cancellationTokenSource.Cancel();
                }

                return new LineEndingResolution(LineEnding.Lf, []);
            });
        _context.LineEndingResolver = resolver;

        // shade-kit init creates the features folder; only the staging folder of the new feature is removed again.
        _context.Repository.CreateDirectory(".shadekit", "features");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_TextSource_WritesTextUnchangedAsRequestAndAsSourceValue()
    {
        // Arrange
        const string text = "Login SSO\nUsers sign in with the company account.";
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(text),
            TestContext.Current.CancellationToken);

        // Assert
        _context.ReadRequest(result.Feature.Id)
            .Should().Be(text);

        var featureJson = _context.Repository.ReadFileText(".shadekit", "features", result.Feature.Id,
            "feature.json");
        using var document = JsonDocument.Parse(featureJson);
        var source = document.RootElement.GetProperty("source");

        source.GetProperty("kind").GetString()
            .Should().Be("text");

        source.GetProperty("value").GetString()
            .Should().Be(text);
    }

    [Theory]
    [InlineData(LineEnding.Lf, "\n")]
    [InlineData(LineEnding.CrLf, "\r\n")]
    public async Task CreateAsync_FileSource_WritesFileContentAsRequestWithResolvedLineEnding(LineEnding lineEnding,
        string newLine)
    {
        // Arrange
        _context.LineEnding = lineEnding;
        _context.Repository.WriteFile("# Login SSO\r\nUsers sign in with the company account.\r\n", "docs",
            "login.md");
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest("docs/login.md"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Source.Value
            .Should().Be("docs/login.md");

        // The file content keeps its text, but not its line endings: the whole file is written with one.
        _context.ReadRequest(result.Feature.Id)
            .Should().Be($"# Login SSO{newLine}Users sign in with the company account.{newLine}");
    }

    [Fact]
    public async Task CreateAsync_FileSourceWithBom_WritesRequestWithoutBom()
    {
        // Arrange
        _context.Repository.WriteFileBytes([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("# Login SSO ä\n")], "docs",
            "login.md");
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest("docs/login.md"),
            TestContext.Current.CancellationToken);

        // Assert
        _context.Repository.ReadFileBytes(".shadekit", "features", result.Feature.Id, "request.md")
            .Should().Equal(Encoding.UTF8.GetBytes("# Login SSO ä\n"));
    }

    [Fact]
    public async Task CreateAsync_EmptyFileSource_WritesEmptyRequest()
    {
        // Arrange
        _context.Repository.WriteFile(string.Empty, "docs", "login.md");
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest("docs/login.md"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(FeatureStoreTestContext.TimeBasedIdPrefix + "login");

        _context.Repository.ReadFileBytes(".shadekit", "features", result.Feature.Id, "request.md")
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_TrackerUrlWithoutTitle_WritesIssueAsRequestAndUsesIssueTitle()
    {
        // Arrange
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be("gh-142-login-with-sso");

        result.Feature.Title
            .Should().Be("Login with SSO");

        result.Feature.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.Url, Value = IssueUrl });

        _context.ReadRequest(result.Feature.Id)
            .Should().Be("# Login with SSO\n\nUsers sign in with the company account.\n");

        A.CallTo(() => _context.TrackerIssueReader.ReadIssueAsync(new Uri(IssueUrl), _context.RepositoryRootPath,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateAsync_WebUrl_WritesUrlContentAsRequest()
    {
        // Arrange
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(WebUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(FeatureStoreTestContext.TimeBasedIdPrefix + "login-sso");

        result.Feature.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.Url, Value = WebUrl });

        _context.ReadRequest(result.Feature.Id)
            .Should().Be(FeatureStoreTestContext.DefaultUrlContent);

        A.CallTo(_context.TrackerIssueReader)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_IssueCannotBeRead_ThrowsInvalidFeatureSourceExceptionAndChangesNothing()
    {
        // Arrange
        A.CallTo(() => _context.TrackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(new TrackerIssueReadFailedException(new Uri(IssueUrl), "gh exited with the exit code 1."));
        _context.Repository.CreateDirectory(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName);
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.InnerException.Should().BeOfType<TrackerIssueReadFailedException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_GhNotFound_ThrowsInvalidFeatureSourceExceptionAndChangesNothing()
    {
        // Arrange
        A.CallTo(() => _context.TrackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(new GhNotFoundException(["issue", "view", IssueUrl], "gh was not found."));
        _context.Repository.CreateDirectory(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName);
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.InnerException.Should().BeOfType<GhNotFoundException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_IssueReadCanceled_RethrowsOperationCanceledExceptionAndChangesNothing()
    {
        // Arrange
        A.CallTo(() => _context.TrackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(new OperationCanceledException());
        _context.Repository.CreateDirectory(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName);
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowExactlyAsync<OperationCanceledException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_UrlSources_PassesRunTokenToReaders()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var featureStore = _context.CreateFeatureStore();

        // Act
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl),
            cancellationTokenSource.Token);
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(WebUrl, "Login SSO"),
            cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => _context.TrackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._,
                cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _context.UrlContentReader.ReadAsync(A<Uri>._, cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateAsync_UrlContentCannotBeRead_ThrowsInvalidFeatureSourceExceptionAndChangesNothing()
    {
        // Arrange
        A.CallTo(() => _context.UrlContentReader.ReadAsync(A<Uri>._, A<CancellationToken>._))
            .Throws(new UrlContentReadFailedException(new Uri(WebUrl), "The request timed out."));
        _context.Repository.CreateDirectory(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName);
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(WebUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.InnerException.Should().BeOfType<UrlContentReadFailedException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_PlaceholdersInRequestContent_KeepsThemUnchanged()
    {
        // Arrange
        const string input = "Login {{feature-id}}\n{{original input, unchanged}} {{feature-title}} " +
                             "{{created at and created by}} {{source kind and source reference}}";
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(input, "{{feature-id}} Login"), TestContext.Current.CancellationToken);

        // Assert
        _context.ReadRequest(result.Feature.Id)
            .Should().Be(input);
    }

    [Theory]
    [InlineData("Login SSO")]
    [InlineData(IssueUrl)]
    public async Task CreateAsync_RepositoryRequestTemplateExists_IgnoresAndKeepsIt(string source)
    {
        // Arrange
        _context.WriteRequestTemplate(ObsoleteRequestTemplate);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(source, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        _context.ReadRequest(result.Feature.Id)
            .Should().NotContain("{{")
            .And.NotContain("<!--")
            .And.NotContain("Request:");

        _context.Repository.ReadFileText(".shadekit", "templates", "request.md")
            .Should().Be(ObsoleteRequestTemplate);
    }

    [Fact]
    public async Task CreateAsync_RepositoryRequestTemplateIsNotUtf8_IgnoresIt()
    {
        // Arrange
        _context.Repository.WriteFileBytes([0xFF, 0xFE, 0x23, 0x00, 0x20, 0x00], ".shadekit", "templates",
            "request.md");
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest("Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        _context.ReadRequest(result.Feature.Id)
            .Should().Be("Login SSO");
    }

    [Fact]
    public async Task CreateAsync_OverrideSelectsOtherFeature_ReplacesOverrideWithNewFeature()
    {
        // Arrange
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        var featureStore = _context.CreateFeatureStore();

        // Act
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(IssueFeatureId);
    }

    [Fact]
    public async Task CreateAsync_OverrideWriteFails_RemovesFeatureFolderAndRethrows()
    {
        // Arrange
        // A directory at the path of the override passes the format check, but the override cannot be written.
        _context.Repository.CreateDirectory(".shadekit", "features", "current-feature.json");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<Exception>())
            .Which
            .Should().Match<Exception>(x => x is IOException || x is UnauthorizedAccessException);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_CleanupFails_RethrowsOriginalException()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.RootPath).Returns(Path.Combine(_context.RepositoryRootPath, ".shadekit"));
        A.CallTo(() => store.RepositoryRootPath).Returns(_context.RepositoryRootPath);
        A.CallTo(() => store.ReadJsonAsync<KitConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(Task.FromResult<VersionedReadResult<KitConfig>?>(null));
        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(Task.FromResult<VersionedReadResult<CurrentFeatureDocument>?>(null));
        A.CallTo(() => store.GetFeatureNames()).Returns([]);
        A.CallTo(() => store.DirectoryExists(A<string>._)).Returns(false);
        var featureDirWriter = A.Fake<IKitDirectoryWriter>();
        A.CallTo(() => store.CreateDirectoryWriter(A<string>._, A<string>._)).Returns(featureDirWriter);
        A.CallTo(() => featureDirWriter.WriteJsonAsync(A<string>._, A<string>._, A<FeatureDocument>._,
                A<FormatAccess>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("The disk is full."));
        A.CallTo(() => store.DeleteDirectory(A<string>._)).Throws(new IOException("The folder is locked."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The disk is full.");

        A.CallTo(() => store.DeleteDirectory(A<string>.That.Matches(x => IsStagingPath(x))))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => store.DeleteDirectory("features/gh-142-login-sso"))
            .MustNotHaveHappened();

        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_OutdatedOverride_CreatesFeatureAndReplacesOverrideInCurrentVersion()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature);
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(IssueFeatureId);

        using var overrideJson = JsonDocument.Parse(
            _context.Repository.ReadFileText(".shadekit", "features", "current-feature.json"));

        overrideJson.RootElement.GetProperty("schemaVersion").GetInt32()
            .Should().Be(2);

        overrideJson.RootElement.GetProperty("featureId").GetString()
            .Should().Be(IssueFeatureId);
    }

    [Fact]
    public async Task CreateAsync_OutdatedOverrideAndConfigWithStop_CreatesFeatureAndReplacesOverride()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature);
        _context.WriteConfig(""", "outdatedFormatOnRead": "stop" """);
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().BeEmpty();

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(IssueFeatureId, "the override is read without a format warning, so it is in the current version");
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task CreateAsync_OverrideCannotBeRead_ThrowsBeforeWritingAnything()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        _context.WriteOverrideContent("{");
        var overridePath = Path.Combine(_context.RepositoryRootPath, ".shadekit", "features", "current-feature.json");
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        var featureStore = _context.CreateFeatureStore();
        File.SetUnixFileMode(overridePath, UnixFileMode.UserWrite);

        try
        {
            Assert.SkipWhen(CanRead(overridePath),
                $"'{overridePath}' is readable without permissions, for example because the tests run as root.");

            // Act
            var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
                TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        finally
        {
            File.SetUnixFileMode(overridePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        A.CallTo(() => store.CreateDirectory(A<string>._))
            .MustNotHaveHappened();

        A.CallTo(() => store.CreateDirectoryWriter(A<string>._, A<string>._))
            .MustNotHaveHappened();

        Directory.EnumerateFileSystemEntries(Path.Combine(_context.RepositoryRootPath, ".shadekit", "features"))
            .Should().Equal(overridePath);

        _context.Repository.ReadFileText(".shadekit", "features", "current-feature.json")
            .Should().Be("{");
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task CreateAsync_OverrideReadFails_RethrowsUnwrappedBeforeWritingAnything(Type exceptionType)
    {
        // Arrange
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        var exception = (Exception)Activator.CreateInstance(exceptionType, "The override cannot be read.")!;
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>(KitStorePaths.CurrentFeatureFilePath,
                A<string>._, A<FormatAccess>._, A<CancellationToken>._))
            .ThrowsAsync(exception);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<Exception>())
            .Which.Should().BeSameAs(exception);

        A.CallTo(() => _context.GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => store.CreateDirectory(A<string>._))
            .MustNotHaveHappened();

        A.CallTo(() => store.CreateDirectoryWriter(A<string>._, A<string>._))
            .MustNotHaveHappened();

        A.CallTo(() => store.WriteJsonAsync(A<string>._, A<string>._, A<CurrentFeatureDocument>._,
                A<FormatAccess>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        _context.KitDirectoryExists("features", IssueFeatureId)
            .Should().BeFalse();

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be("gh-7-checkout");
    }

    [Fact]
    public async Task CreateAsync_OutdatedOverride_ChecksOverrideWithWriteAccessOnly()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.CurrentFeature);
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(IssueFeatureId);

        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>(KitStorePaths.CurrentFeatureFilePath,
                FormatIds.CurrentFeature, FormatAccess.Write, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => store.ReadJsonAsync<CurrentFeatureDocument>(A<string>._, A<string>._,
                A<FormatAccess>.That.Not.IsEqualTo(FormatAccess.Write), A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => store.ReadFileAsync(KitStorePaths.CurrentFeatureFilePath, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => store.ReadJsonAsync<KitConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(IssueFeatureId);
    }

    [Fact]
    public async Task CreateAsync_BrokenOverrideAndOverrideWriteFails_RemovesFeatureFolderAndRethrows()
    {
        // Arrange
        var store = _context.CreateStoreFailingOverrideWrite(new IOException("The disk is full."));
        _context.Repository.CreateDirectory(".shadekit", "features");
        _context.WriteOverrideContent("{");
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The disk is full.");

        _context.KitDirectoryExists("features", IssueFeatureId)
            .Should().BeFalse();

        Directory.EnumerateFileSystemEntries(Path.Combine(_context.RepositoryRootPath, ".shadekit", "features"))
            .Should().BeEmpty("the broken override was deleted for the replacement and no folder is left behind");
    }

    [Fact]
    public async Task CreateAsync_OutdatedConfig_ReturnsUpgradeNeededFormatWarning()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Config);
        _context.WriteConfig(""", "defaultProfile": "bugfix" """);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/config.json", 1, 2));

        result.Feature.Profile
            .Should().Be(WorkflowProfiles.Bugfix);
    }

    [Fact]
    public async Task CreateAsync_CancelledBeforeStart_ThrowsAndWritesNothing()
    {
        // Arrange
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [InlineData("Login SSO\r\n\r\nDetails\r\n", LineEnding.Lf, "Login SSO\n\nDetails\n")]
    [InlineData("Login SSO\r\n\r\nDetails\r\n", LineEnding.CrLf, "Login SSO\r\n\r\nDetails\r\n")]
    [InlineData("Login SSO\n\nDetails\n", LineEnding.CrLf, "Login SSO\r\n\r\nDetails\r\n")]
    [InlineData("Login SSO\r\nDetails\n", LineEnding.Lf, "Login SSO\nDetails\n")]
    [InlineData("Login SSO\r\nDetails", LineEnding.CrLf, "Login SSO\r\nDetails")]
    [InlineData("Login SSO\rDetails", LineEnding.Lf, "Login SSO\nDetails")]
    public async Task CreateAsync_RequestContentLineEndings_WritesRequestWithResolvedLineEnding(string text,
        LineEnding lineEnding, string expectedRequest)
    {
        // Arrange
        _context.LineEnding = lineEnding;
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(text, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        _context.ReadRequest(result.Feature.Id)
            .Should().Be(expectedRequest);
    }

    [Fact]
    public async Task CreateAsync_NewFeature_ResolvesLineEndingOfEachFileForItsFinalPathNotTheStagingPath()
    {
        // Arrange
        List<string> requestedPaths = [];
        _context.LineEndingResolver = FixedLineEnding.CreateRecordingResolver(
            $".shadekit/features/{IssueFeatureId}/", requestedPaths);
        var featureStore = _context.CreateFeatureStore();

        // Act
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        requestedPaths
            .Should().Equal(
                $".shadekit/features/{IssueFeatureId}/feature.json",
                $".shadekit/features/{IssueFeatureId}/request.md",
                ".shadekit/features/current-feature.json");
    }

    [Fact]
    public async Task CreateAsync_NewFeature_PreparesLineEndingsOfAllFinalPathsOnceBeforeFirstWrite()
    {
        // Arrange
        var resolver = FixedLineEnding.CreateResolver(LineEnding.Lf);
        _context.LineEndingResolver = resolver;
        var featureStore = _context.CreateFeatureStore();
        string[] expectedGitPaths =
        [
            $".shadekit/features/{IssueFeatureId}/feature.json",
            $".shadekit/features/{IssueFeatureId}/request.md",
            ".shadekit/features/current-feature.json"
        ];

        // Act
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => resolver.PrepareAsync(
                A<IReadOnlyCollection<RepositoryFile>>.That.Matches(files =>
                    files.Select(file => file.GitPath).SequenceEqual(expectedGitPaths)),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => resolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
                .MustHaveHappened(3, Times.Exactly));
    }

    [Fact]
    public async Task CreateAsync_RequestTemplatePathIsDirectory_IgnoresItAndCreatesFeature()
    {
        // Arrange
        _context.Repository.CreateDirectory(".shadekit", "templates", "request.md");
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(IssueFeatureId);

        _context.ReadRequest(IssueFeatureId)
            .Should().Be(TrackerIssueMarkdown.Format(FeatureStoreTestContext.DefaultIssue));

        _context.KitDirectoryExists("templates", "request.md")
            .Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_EachWriteReturnsOtherWarning_ReturnsWarningsOfAllWritesInWriteOrder()
    {
        // Arrange
        var resolver = A.Fake<ILineEndingResolver>();
        A.CallTo(() => resolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .ReturnsLazily((RepositoryFile file, CancellationToken _) => new LineEndingResolution(
                LineEnding.Lf,
                [new EditorConfigUnreadableWarning(file.GitPath, "Access denied.")]));
        _context.LineEndingResolver = resolver;
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new EditorConfigUnreadableWarning($".shadekit/features/{IssueFeatureId}/feature.json",
                    "Access denied."),
                new EditorConfigUnreadableWarning($".shadekit/features/{IssueFeatureId}/request.md",
                    "Access denied."),
                new EditorConfigUnreadableWarning(".shadekit/features/current-feature.json", "Access denied."));
    }

    [Fact]
    public async Task CreateAsync_FinalFeaturePathPrescribesOtherLineEnding_WritesFeatureFilesWithLineEndingOfFinalPath()
    {
        // Arrange
        _context.LineEndingResolver = FixedLineEnding.CreateRecordingResolver(
            $".shadekit/features/{IssueFeatureId}/", []);
        var featureStore = _context.CreateFeatureStore();

        // Act
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        var featureJson = _context.Repository.ReadFileText(".shadekit", "features", IssueFeatureId, "feature.json");

        featureJson
            .Should().Be(featureJson.ReplaceLineEndings("\r\n"), "feature.json gets the line ending of its final path")
            .And.EndWith("}\r\n");

        var request = _context.ReadRequest(IssueFeatureId);

        request
            .Should().Be(request.ReplaceLineEndings("\r\n"), "request.md gets the line ending of its final path")
            .And.Contain("\r\n");

        _context.Repository.ReadFileText(".shadekit", "features", "current-feature.json")
            .Should().NotContain("\r");
    }

    [Fact]
    public async Task CreateAsync_StoreUsedBeforeWithWarnings_ReturnsNoWarningOfEarlierOperation()
    {
        // Arrange
        var earlierWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        _context.WriteFeature("other-feature");
        _context.SetBranch(null);
        var store = TestKitStores.Create(Path.Combine(_context.RepositoryRootPath, KitStorePaths.RootPath),
            FixedLineEnding.CreateWriterWithWarningsOnFirstWrite(LineEnding.Lf, earlierWarning), _context.Registry);
        var featureStore = _context.CreateFeatureStore();
        var earlierResult = await featureStore.SetCurrentAsync(store, "other-feature",
            TestContext.Current.CancellationToken);

        // Act
        var result = await featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        earlierResult.Warnings
            .Should().Equal(earlierWarning);

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_CrLfLineEnding_WritesJsonFilesWithCrLfThatCanBeReadBack()
    {
        // Arrange
        _context.LineEnding = LineEnding.CrLf;
        var featureStore = _context.CreateFeatureStore();

        // Act
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        var featureJson = _context.Repository.ReadFileText(".shadekit", "features", IssueFeatureId, "feature.json");

        featureJson
            .Should().Be(featureJson.ReplaceLineEndings("\r\n"), "every line ends with CRLF")
            .And.EndWith("}\r\n");

        var overrideJson = _context.Repository.ReadFileText(".shadekit", "features", "current-feature.json");

        overrideJson
            .Should().Be(overrideJson.ReplaceLineEndings("\r\n"), "every line ends with CRLF")
            .And.EndWith("}\r\n");

        (await _context.ReadFeatureFileAsync(IssueFeatureId)).Id
            .Should().Be(IssueFeatureId);

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(IssueFeatureId);
    }

    [Fact]
    public async Task CreateAsync_StoreWritesWithWarnings_ReturnsFormatWarningsFirstAndEachStoreWarningOnce()
    {
        // Arrange
        var editorConfigWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        _context.WriteWarnings = [editorConfigWarning];
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Config);
        _context.WriteConfig(""", "defaultProfile": "bugfix" """);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/config.json", 1, 2),
                editorConfigWarning);
    }

    [Fact]
    public async Task CreateAsync_NewFeature_MovesCompleteStagingFolderIntoPlaceAndLeavesNoStagingFolder()
    {
        // Arrange
        var realStore = _context.CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        string? movedSource = null;
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .Invokes((string source, string destination) =>
            {
                movedSource = source;

                // The feature folder appears only with the move, and the staging folder is complete before it.
                _context.KitDirectoryExists("features", IssueFeatureId)
                    .Should().BeFalse();

                realStore.GetFiles(source).Select(Path.GetFileName)
                    .Should().BeEquivalentTo("feature.json", "request.md");

                destination
                    .Should().Be($"features/{IssueFeatureId}");
            })
            .CallsWrappedMethod();
        var featureStore = _context.CreateFeatureStore();

        // Act
        await featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        movedSource
            .Should().NotBeNull()
            .And.Match(x => IsStagingPath(x));

        _context.Repository.TakeSnapshot().Keys
            .Should().BeEquivalentTo(
                ".git",
                ".shadekit",
                Path.Combine(".shadekit", "features"),
                Path.Combine(".shadekit", "features", "current-feature.json"),
                Path.Combine(".shadekit", "features", IssueFeatureId),
                Path.Combine(".shadekit", "features", IssueFeatureId, "feature.json"),
                Path.Combine(".shadekit", "features", IssueFeatureId, "request.md"));
    }

    [Fact]
    [Trait("Requirement", "FP03-AC3")]
    public async Task CreateAsync_FeatureFolderCreatedBeforeMove_ThrowsAlreadyExistsAndKeepsOtherFeature()
    {
        // Arrange
        var realStore = _context.CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        Dictionary<string, string>? snapshotOfOtherRun = null;
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .Invokes(() =>
            {
                // A concurrent run creates the same feature between the existence check and the move.
                _context.WriteFeature(IssueFeatureId, title: "Other run");
                _context.WriteOverride(IssueFeatureId);
                snapshotOfOtherRun = _context.Repository.TakeSnapshot()
                    .Where(x => !x.Key.Contains(".tmp", StringComparison.Ordinal))
                    .ToDictionary();
            })
            .CallsWrappedMethod();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<FeatureAlreadyExistsException>()).Which;

        exception.FeatureId
            .Should().Be(IssueFeatureId);

        exception.InnerException
            .Should().BeAssignableTo<IOException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshotOfOtherRun);

        (await _context.ReadFeatureFileAsync(IssueFeatureId)).Title
            .Should().Be("Other run");
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(DirectoryNotFoundException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task CreateAsync_MoveFailsAndFeatureFolderMissing_ThrowsFeatureIoFailedExceptionAndChangesNothing(
        Type exceptionType)
    {
        // Arrange
        var moveException = (Exception)Activator.CreateInstance(exceptionType, "The folder is locked.")!;
        var realStore = _context.CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._)).Throws(moveException);
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<FeatureIoFailedException>()).Which;

        exception.FeatureId
            .Should().Be(IssueFeatureId);

        exception.FolderPath
            .Should().Be($".shadekit/features/{IssueFeatureId}/");

        exception.Operation
            .Should().Be(FeatureIoFailedException.CreateOperation);

        exception.InnerException
            .Should().BeSameAs(moveException);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot, "the staging folder is removed and the override is not written");

        A.CallTo(() => store.WriteJsonAsync(A<string>._, FormatIds.CurrentFeature, A<CurrentFeatureDocument>._,
                A<FormatAccess>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_MoveDeniedAndOtherRunCreatedFolder_ThrowsFeatureAlreadyExistsException()
    {
        // Arrange
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .Invokes(() => _context.WriteFeature(IssueFeatureId, title: "Other run"))
            .Throws(new UnauthorizedAccessException("Access denied."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<FeatureAlreadyExistsException>())
            .Which.InnerException
            .Should().BeOfType<UnauthorizedAccessException>();

        (await _context.ReadFeatureFileAsync(IssueFeatureId)).Title
            .Should().Be("Other run");
    }

    [Fact]
    public async Task CreateAsync_MoveFailsAndStagingFolderCannotBeDeleted_ThrowsFeatureIoFailedException()
    {
        // Arrange
        var moveException = new IOException("The folder is locked.");
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._)).Throws(moveException);
        A.CallTo(() => store.DeleteDirectory(A<string>._)).Throws(new IOException("Still locked."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<FeatureIoFailedException>())
            .Which.InnerException
            .Should().BeSameAs(moveException);

        _context.KitDirectoryExists("features", IssueFeatureId)
            .Should().BeFalse();

        (await featureStore.ListAsync(_context.CreateStore(), TestContext.Current.CancellationToken)).Features
            .Should().BeEmpty("the leftover staging folder is hidden");
    }

    [Fact]
    public async Task CreateAsync_MoveFailsWithOtherException_RethrowsItUnwrappedAndDeletesStagingFolder()
    {
        // Arrange
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .Throws(new InvalidOperationException("Unexpected."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unexpected.");

        GetStagingDirectories()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_MoveDeniedAndFileAtFeatureFolderPath_RethrowsUnwrapped()
    {
        // Arrange
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .Invokes(() => _context.WriteKitFile("not a folder", "features", IssueFeatureId))
            .Throws(new UnauthorizedAccessException("Access denied."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Access denied.");
    }

    [Fact]
    public async Task CreateAsync_RetryAfterFeatureIoFailure_CreatesFeature()
    {
        // Arrange
        var failMove = true;
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .WhenArgumentsMatch(_ => failMove)
            .Throws(new IOException("The folder is locked."));
        var featureStore = _context.CreateFeatureStore();
        var firstAttempt = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);
        await firstAttempt.Should().ThrowAsync<FeatureIoFailedException>();
        failMove = false;

        // Act
        var result = await featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(IssueFeatureId);

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be(IssueFeatureId);

        GetStagingDirectories()
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task CreateAsync_StagingFolderCannotBeCreated_ThrowsFeatureIoFailedExceptionAndChangesNothing(
        Type exceptionType)
    {
        // Arrange
        var createException = (Exception)Activator.CreateInstance(exceptionType, "Access denied.")!;
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.CreateDirectory(A<string>.That.Matches(x => IsStagingPath(x)))).Throws(createException);
        _context.Repository.CreateDirectory(".shadekit", "features");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<FeatureIoFailedException>()).Which;

        exception.Operation
            .Should().Be(FeatureIoFailedException.CreateOperation);

        exception.InnerException
            .Should().BeSameAs(createException);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        A.CallTo(() => store.MoveDirectory(A<string>._, A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_FileWriteFailsInStagingFolder_RethrowsUnwrappedIOException()
    {
        // Arrange
        var realStore = _context.CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        A.CallTo(() => store.CreateDirectoryWriter(A<string>._, A<string>._))
            .ReturnsLazily((string writePath, string targetPath) =>
            {
                var featureDirWriter = A.Fake<IKitDirectoryWriter>(options =>
                    options.Wrapping(realStore.CreateDirectoryWriter(writePath, targetPath)));
                A.CallTo(() => featureDirWriter.WriteFileAsync(A<string>._, A<TextFileContent>._,
                        A<CancellationToken>._))
                    .ThrowsAsync(new IOException("The disk is full."));

                return featureDirWriter;
            });
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowExactlyAsync<IOException>()
            .WithMessage("The disk is full.");

        GetStagingDirectories()
            .Should().BeEmpty();
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task CreateAsync_FeaturesFolderReadOnly_ThrowsFeatureIoFailedExceptionAndChangesNothing()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a folder read-only.");
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        Exception? thrown;

        using (UnixFileModeScope.MakeReadOnly(Path.Combine(_context.RepositoryRootPath, ".shadekit", "features")))
        {
            // Act
            thrown = await Record.ExceptionAsync(() => featureStore.CreateAsync(_context.CreateStore(),
                _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken));
        }

        // Assert
        var exception = thrown
            .Should().BeOfType<FeatureIoFailedException>().Subject;

        exception.FeatureId
            .Should().Be(IssueFeatureId);

        exception.Operation
            .Should().Be(FeatureIoFailedException.CreateOperation);

        exception.InnerException
            .Should().Match(x => x is IOException || x is UnauthorizedAccessException);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        (await _context.ReadOverrideFeatureIdAsync())
            .Should().Be("gh-7-checkout");
    }

    [Fact]
    public async Task CreateAsync_OverrideWriteFailsAndFeatureCannotBeMovedBack_KeepsCompleteFeatureAndRethrows()
    {
        // Arrange
        var realStore = _context.CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        A.CallTo(() => store.WriteJsonAsync(A<string>._, FormatIds.CurrentFeature, A<CurrentFeatureDocument>._,
                A<FormatAccess>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("The disk is full."));
        A.CallTo(() => store.MoveDirectory($"features/{IssueFeatureId}", A<string>._))
            .Throws(new IOException("The folder is locked."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The disk is full.");

        _context.KitFileExists("features", IssueFeatureId, "feature.json")
            .Should().BeTrue();

        _context.KitFileExists("features", IssueFeatureId, "request.md")
            .Should().BeTrue();

        GetStagingDirectories()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_CancelledAfterMoveIntoPlace_RemovesFeatureFolderKeepsOverrideAndRethrows()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var store = A.Fake<IKitStore>(options => options.Wrapping(_context.CreateStore()));
        A.CallTo(() => store.MoveDirectory(A<string>._, $"features/{IssueFeatureId}"))
            .Invokes(() => cancellationTokenSource.Cancel())
            .CallsWrappedMethod();
        _context.WriteFeature("gh-7-checkout");
        _context.WriteOverride("gh-7-checkout");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(() => store.MoveDirectory(A<string>._, $"features/{IssueFeatureId}"))
            .MustHaveHappenedOnceExactly();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_OverrideWriteFailsAndHiddenFolderCannotBeDeleted_RethrowsAndFeatureIsGone()
    {
        // Arrange
        var realStore = _context.CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        A.CallTo(() => store.WriteJsonAsync(A<string>._, FormatIds.CurrentFeature, A<CurrentFeatureDocument>._,
                A<FormatAccess>._, A<CancellationToken>._))
            .ThrowsAsync(new IOException("The disk is full."));
        A.CallTo(() => store.DeleteDirectory(A<string>._)).Throws(new IOException("The folder is locked."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The disk is full.");

        _context.KitDirectoryExists("features", IssueFeatureId)
            .Should().BeFalse();

        GetStagingDirectories()
            .Should().ContainSingle();

        (await featureStore.ListAsync(realStore, TestContext.Current.CancellationToken))
            .Should().BeEquivalentTo(new FeatureListResult([], []));
    }

    [Fact]
    public async Task CreateAsync_FileAtFeatureFolderPath_ThrowsIOExceptionAndKeepsFile()
    {
        // Arrange
        _context.WriteKitFile("not a folder", "features", IssueFeatureId);
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(IssueUrl, "Login SSO"),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .Which.Should().NotBeOfType<DirectoryNotFoundException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_LeftoverHiddenFoldersOfSameId_CreatesFeatureAndKeepsLeftovers()
    {
        // Arrange
        _context.WriteKitFile("old", "features", $".{IssueFeatureId}.00000000.tmp", "feature.json");
        _context.WriteKitFile("old", "features", $".{IssueFeatureId}.00000000.dropping", "feature.json");
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(IssueUrl, "Login SSO"), TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(IssueFeatureId);

        _context.KitFileExists("features", $".{IssueFeatureId}.00000000.tmp", "feature.json")
            .Should().BeTrue();

        _context.KitFileExists("features", $".{IssueFeatureId}.00000000.dropping", "feature.json")
            .Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(), null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static bool CanRead(string filePath)
    {
        try
        {
            _ = File.ReadAllBytes(filePath);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsStagingPath(string kitRelativePath)
    {
        return kitRelativePath.StartsWith($"features/.{IssueFeatureId}.", StringComparison.Ordinal)
               && kitRelativePath.EndsWith(".tmp", StringComparison.Ordinal);
    }

    private IEnumerable<string> GetStagingDirectories()
    {
        var featuresPath = Path.Combine(_context.RepositoryRootPath, ".shadekit", "features");

        return Directory.Exists(featuresPath)
            ? Directory.EnumerateDirectories(featuresPath, $".{IssueFeatureId}.*.tmp")
            : [];
    }
}
