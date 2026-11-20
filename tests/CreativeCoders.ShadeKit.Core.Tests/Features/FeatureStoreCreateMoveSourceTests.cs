using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public sealed class FeatureStoreCreateMoveSourceTests : IDisposable
{
    private const string NotesFileName = "20261020-1500-login-sso.md";

    private const string NotesPath = ".shadekit/discussions/" + NotesFileName;

    private const string NotesContent = "# Login SSO\n\nFeature: `none` · Date: 2026-10-20\n\n## Conclusions\n\nUse Entra ID.\n";

    private const string FeatureId = FeatureStoreTestContext.TimeBasedIdPrefix + "login-sso";

    private const string MovedNotesPath = ".shadekit/features/" + FeatureId + "/discussions/" + NotesFileName;

    private readonly FeatureStoreTestContext _context = new FeatureStoreTestContext();

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_MoveSourceWithDiscussionNotes_WritesNotesIntoFeatureDiscussionsAndDeletesOriginal()
    {
        // Arrange
        _context.WriteKitFile(NotesContent, "discussions", NotesFileName);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(NotesPath, moveSource: true), TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Id
            .Should().Be(FeatureId);

        _context.Repository.ReadFileText(".shadekit", "features", FeatureId, "discussions", NotesFileName)
            .Should().Be(NotesContent);

        _context.ReadRequest(FeatureId)
            .Should().Be(NotesContent);

        _context.KitFileExists("discussions", NotesFileName)
            .Should().BeFalse();

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_MoveSourceWithDiscussionNotes_SetsSourceValueToMovedPath()
    {
        // Arrange
        _context.WriteKitFile(NotesContent, "discussions", NotesFileName);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(NotesPath, moveSource: true), TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Source.Kind
            .Should().Be(FeatureSourceKind.File);

        result.Feature.Source.Value
            .Should().Be(MovedNotesPath);

        result.MovedSourceFrom
            .Should().Be(NotesPath);

        var featureJson = _context.Repository.ReadFileText(".shadekit", "features", FeatureId, "feature.json");
        using var document = JsonDocument.Parse(featureJson);

        document.RootElement.GetProperty("source").GetProperty("value").GetString()
            .Should().Be(MovedNotesPath);
    }

    [Theory]
    [InlineData(LineEnding.Lf, "\n")]
    [InlineData(LineEnding.CrLf, "\r\n")]
    public async Task CreateAsync_MoveSourceWithDiscussionNotes_WritesMovedNotesWithResolvedLineEnding(
        LineEnding lineEnding, string newLine)
    {
        // Arrange
        _context.LineEnding = lineEnding;
        _context.WriteKitFile("# Login SSO\r\n\r\nUse Entra ID.\r\n", "discussions", NotesFileName);
        var featureStore = _context.CreateFeatureStore();

        // Act
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(NotesPath, moveSource: true),
            TestContext.Current.CancellationToken);

        // Assert
        _context.Repository.ReadFileText(".shadekit", "features", FeatureId, "discussions", NotesFileName)
            .Should().Be($"# Login SSO{newLine}{newLine}Use Entra ID.{newLine}");
    }

    [Fact]
    public async Task CreateAsync_MoveSourceWithNormalizablePathToDiscussionNotes_MovesNotes()
    {
        // Arrange
        _context.WriteKitFile(NotesContent, "discussions", NotesFileName);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest($".shadekit/features/../discussions/{NotesFileName}", moveSource: true),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Source.Value
            .Should().Be(MovedNotesPath);

        _context.KitFileExists("discussions", NotesFileName)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("Login SSO\nUsers sign in with the company account.", null)]
    [InlineData("https://example.com/requests/login-sso", "Login SSO")]
    [InlineData("https://github.com/acme/shop/issues/142", null)]
    [InlineData("docs/login.md", null)]
    [InlineData(".shadekit/discussions/archive/login.md", null)]
    [InlineData(".shadekit/features/20261019-0900-checkout/discussions/login.md", null)]
    [InlineData(".shadekit/login.md", null)]
    public async Task CreateAsync_MoveSourceWithUnmovableSource_ThrowsInvalidFeatureSourceExceptionAndChangesNothing(
        string source, string? title)
    {
        // Arrange
        _context.Repository.WriteFile(NotesContent, "docs", "login.md");
        _context.WriteKitFile(NotesContent, "discussions", "archive", "login.md");
        _context.WriteKitFile(NotesContent, "features", "20261019-0900-checkout", "discussions", "login.md");
        _context.WriteKitFile(NotesContent, "login.md");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(source, title, moveSource: true), TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.Problem
            .Should().Be(FeatureSourceProblem.SourceNotMovable);

        exception.SourceText
            .Should().Be(source);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [InlineData("https://github.com/acme/shop/issues/142", null)]
    [InlineData("https://example.com/requests/login-sso", "Login SSO")]
    public async Task CreateAsync_MoveSourceWithUrl_ThrowsInvalidFeatureSourceExceptionWithoutReadingUrl(string source,
        string? title)
    {
        // Arrange
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(source, title, moveSource: true), TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which.Problem
            .Should().Be(FeatureSourceProblem.SourceNotMovable);

        A.CallTo(_context.TrackerIssueReader).MustNotHaveHappened();
        A.CallTo(_context.UrlContentReader).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_MoveSourceWithTextAndGitIdentityMissing_ThrowsGitIdentityNotConfiguredException()
    {
        // Arrange
        A.CallTo(() => _context.GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .Throws(new GitIdentityNotConfiguredException(_context.RepositoryRootPath, ["user.name"]));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest("Login SSO", moveSource: true), TestContext.Current.CancellationToken);

        // Assert
        // The git identity is checked first, as for every other source problem (package 03, order of work).
        await act.Should().ThrowAsync<GitIdentityNotConfiguredException>();
    }

    [Fact]
    public async Task CreateAsync_MoveSourceWithFileOutsideRepository_ThrowsInvalidFeatureSourceException()
    {
        // Arrange
        using var outside = new TempKitDirectory();
        var filePath = outside.WriteFile(NotesContent, ".shadekit", "discussions", NotesFileName);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(filePath, moveSource: true), TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which.Problem
            .Should().Be(FeatureSourceProblem.SourceNotMovable);

        File.Exists(filePath)
            .Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_MoveSourceAndOverrideWriteFails_KeepsOriginalNotesAndRemovesFeature()
    {
        // Arrange
        _context.WriteKitFile(NotesContent, "discussions", NotesFileName);
        var store = _context.CreateStoreFailingOverrideWrite(new IOException("The disk is full."));
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(store, _context.CreateRequest(NotesPath, moveSource: true),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>()
            .WithMessage("The disk is full.");

        _context.Repository.ReadFileText(".shadekit", "discussions", NotesFileName)
            .Should().Be(NotesContent);

        _context.KitDirectoryExists("features", FeatureId)
            .Should().BeFalse();
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task CreateAsync_MoveSourceAndOriginalCannotBeDeleted_ReturnsSourceNotRemovedWarningAndKeepsFeature()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file undeletable.");
        _context.WriteKitFile(NotesContent, "discussions", NotesFileName);
        var discussionsPath = Path.Combine(_context.RepositoryRootPath, ".shadekit", "discussions");
        var originalMode = File.GetUnixFileMode(discussionsPath);

        // Without write permission on its folder, the file cannot be deleted.
        File.SetUnixFileMode(discussionsPath, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            Assert.SkipWhen(CanCreateFile(discussionsPath),
                $"'{discussionsPath}' is writable without permissions, for example because the tests run as root.");
            var featureStore = _context.CreateFeatureStore();

            // Act
            var result = await featureStore.CreateAsync(_context.CreateStore(),
                _context.CreateRequest(NotesPath, moveSource: true), TestContext.Current.CancellationToken);

            // Assert
            var warning = result.Warnings
                .Should().ContainSingle()
                .Which.Should().BeOfType<FeatureSourceNotRemovedWarning>().Subject;

            warning.FilePath
                .Should().Be(NotesPath);

            warning.Reason
                .Should().NotBeNullOrWhiteSpace();

            result.Feature.Source.Value
                .Should().Be(MovedNotesPath);

            _context.Repository.ReadFileText(".shadekit", "features", FeatureId, "discussions", NotesFileName)
                .Should().Be(NotesContent);

            File.Exists(Path.Combine(discussionsPath, NotesFileName))
                .Should().BeTrue();

            (await _context.ReadOverrideFeatureIdAsync())
                .Should().Be(FeatureId);
        }
        finally
        {
            File.SetUnixFileMode(discussionsPath, originalMode);
        }
    }

    [Fact]
    public async Task CreateAsync_MoveSourceAndStagingWriteFails_KeepsOriginalNotesAndChangesNothing()
    {
        // Arrange
        var resolver = A.Fake<ILineEndingResolver>();
        A.CallTo(() => resolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .ReturnsLazily((RepositoryFile file, CancellationToken _) =>
                file.GitPath.EndsWith("/discussions/" + NotesFileName, StringComparison.Ordinal)
                    ? throw new InvalidOperationException("The notes cannot be written.")
                    : new LineEndingResolution(LineEnding.Lf, []));
        _context.LineEndingResolver = resolver;
        _context.WriteKitFile(NotesContent, "discussions", NotesFileName);

        // shade-kit init creates the features folder; only the staging folder of the new feature is removed again.
        _context.Repository.CreateDirectory(".shadekit", "features");
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(NotesPath, moveSource: true), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The notes cannot be written.");

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.Repository.ReadFileText(".shadekit", "discussions", NotesFileName)
            .Should().Be(NotesContent);
    }

    [Fact]
    public async Task CreateAsync_MoveSourceAndFeatureExists_ThrowsFeatureAlreadyExistsExceptionAndKeepsNotes()
    {
        // Arrange
        _context.WriteKitFile(NotesContent, "discussions", NotesFileName);
        _context.WriteFeature(FeatureId);
        var snapshot = _context.Repository.TakeSnapshot();
        var featureStore = _context.CreateFeatureStore();

        // Act
        var act = () => featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(NotesPath, moveSource: true), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<FeatureAlreadyExistsException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CreateAsync_MoveSourceWithAbsolutePathInsideRepository_MovesNotes()
    {
        // Arrange
        var absolutePath = Path.Combine(_context.RepositoryRootPath, ".shadekit", "discussions", NotesFileName);
        _context.WriteKitFile(NotesContent, "discussions", NotesFileName);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(),
            _context.CreateRequest(absolutePath, moveSource: true), TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Source.Value
            .Should().Be(MovedNotesPath);

        result.MovedSourceFrom
            .Should().Be(NotesPath);

        File.Exists(absolutePath)
            .Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_MoveSourceRelativeToDiscussionsWorkingDirectory_MovesNotes()
    {
        // Arrange
        _context.WriteKitFile(NotesContent, "discussions", NotesFileName);
        var workingDirectory = Path.Combine(_context.RepositoryRootPath, ".shadekit", "discussions");
        var request = new NewFeatureRequest(NotesFileName, null, null, workingDirectory, MoveSource: true);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), request,
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Source.Value
            .Should().Be(MovedNotesPath);

        _context.KitFileExists("discussions", NotesFileName)
            .Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_MoveSource_DeterminesLineEndingOfMovedNotesForFeaturePath()
    {
        // Arrange
        // Only the final path in the feature folder gets CRLF, so the copy shows which path the writer resolved.
        var resolver = A.Fake<ILineEndingResolver>();
        A.CallTo(() => resolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .ReturnsLazily((RepositoryFile file, CancellationToken _) => new LineEndingResolution(
                file.GitPath == MovedNotesPath ? LineEnding.CrLf : LineEnding.Lf, []));
        _context.LineEndingResolver = resolver;
        _context.WriteKitFile("# Login SSO\n\nUse Entra ID.\n", "discussions", NotesFileName);
        var featureStore = _context.CreateFeatureStore();

        // Act
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(NotesPath, moveSource: true),
            TestContext.Current.CancellationToken);

        // Assert
        _context.Repository.ReadFileText(".shadekit", "features", FeatureId, "discussions", NotesFileName)
            .Should().Be("# Login SSO\r\n\r\nUse Entra ID.\r\n");

        _context.ReadRequest(FeatureId)
            .Should().Be("# Login SSO\n\nUse Entra ID.\n");
    }

    [Fact]
    public async Task CreateAsync_MoveSourceWithBomAndNoFinalLineEnding_WritesCopyWithoutBomAndWithoutFinalLineEnding()
    {
        // Arrange
        _context.Repository.WriteFileBytes([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("# Login SSO\n\nUse Entra ID.")],
            ".shadekit", "discussions", NotesFileName);
        var featureStore = _context.CreateFeatureStore();

        // Act
        await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(NotesPath, moveSource: true),
            TestContext.Current.CancellationToken);

        // Assert
        _context.Repository.ReadFileBytes(".shadekit", "features", FeatureId, "discussions", NotesFileName)
            .Should().Equal(Encoding.UTF8.GetBytes("# Login SSO\n\nUse Entra ID."));
    }

    [Fact]
    public async Task CreateAsync_DiscussionNotesWithoutMoveSource_LeavesNotesInPlace()
    {
        // Arrange
        _context.WriteKitFile(NotesContent, "discussions", NotesFileName);
        var featureStore = _context.CreateFeatureStore();

        // Act
        var result = await featureStore.CreateAsync(_context.CreateStore(), _context.CreateRequest(NotesPath),
            TestContext.Current.CancellationToken);

        // Assert
        result.Feature.Source.Value
            .Should().Be(NotesPath);

        result.MovedSourceFrom
            .Should().BeNull();

        _context.KitFileExists("discussions", NotesFileName)
            .Should().BeTrue();

        _context.KitDirectoryExists("features", FeatureId, "discussions")
            .Should().BeFalse();
    }

    private static bool CanCreateFile(string directoryPath)
    {
        var probePath = Path.Combine(directoryPath, ".probe");

        try
        {
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
