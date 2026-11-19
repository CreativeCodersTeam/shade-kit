using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Claude;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Claude;

// The plugin CLI is faked, so these tests never start claude.
public class ClaudeCodeIntegrationTests
{
    private const string PluginId = "shade-kit@creativecoders-shade-kit";

    private const string MarketplaceName = "creativecoders-shade-kit";

    private const string Repo = "CreativeCodersTeam/shade-kit";

    // Replaced by an absolute path of the current operating system.
    private const string OtherPath = "<other path>";

    private static readonly MarketplaceSource ReleaseSource = MarketplaceSource.ForGitHub(Repo, "v0.8.0");

    private static readonly ClaudeInstalledPlugin UserPlugin = new(PluginId, "0.7.0", "user", true);

    private readonly IClaudePluginCli _claudePluginCli = A.Fake<IClaudePluginCli>();

    private readonly IClaudeExecutableLocator _claudeExecutableLocator = A.Fake<IClaudeExecutableLocator>();

    private readonly ClaudeCodeIntegration _integration;

    public ClaudeCodeIntegrationTests()
    {
        _integration = new ClaudeCodeIntegration(_claudePluginCli, _claudeExecutableLocator);
    }

    [Fact]
    public void Constructor_NullClaudePluginCli_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ClaudeCodeIntegration(null!, _claudeExecutableLocator);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("claudePluginCli");
    }

    [Fact]
    public void Constructor_NullClaudeExecutableLocator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ClaudeCodeIntegration(_claudePluginCli, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("claudeExecutableLocator");
    }

    [Fact]
    public void AgentId_Always_IsClaude()
    {
        // Assert
        _integration.AgentId
            .Should().Be("claude");

        ClaudeCodeIntegration.PluginId
            .Should().Be(PluginId);

        ClaudeCodeIntegration.MarketplaceName
            .Should().Be(MarketplaceName);
    }

    [Theory]
    [InlineData("/usr/local/bin/claude", true)]
    [InlineData(null, false)]
    public void IsAvailable_ClaudeOnPathOrNot_ReturnsWhetherClaudeIsFound(string? executablePath, bool expected)
    {
        // Arrange
        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable()).Returns(executablePath);

        // Act
        var isAvailable = _integration.IsAvailable();

        // Assert
        isAvailable
            .Should().Be(expected);
    }

    [Fact]
    public async Task InstallAsync_NullSource_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _integration.InstallAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public async Task UpdateAsync_NullGetSource_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _integration.UpdateAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("getSource");
    }

    [Fact]
    public void EnsureAvailable_ClaudeOnPath_DoesNotThrow()
    {
        // Arrange
        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable()).Returns("/usr/local/bin/claude");

        // Act
        var act = () => _integration.EnsureAvailable();

        // Assert
        act.Should().NotThrow();

        A.CallTo(_claudePluginCli).MustNotHaveHappened();
    }

    [Fact]
    public void EnsureAvailable_ClaudeNotOnPath_ThrowsClaudeNotFoundNamingFirstCommand()
    {
        // Arrange
        A.CallTo(() => _claudeExecutableLocator.FindClaudeExecutable()).Returns(null);

        // Act
        var act = () => _integration.EnsureAvailable();

        // Assert
        var exception = act.Should().Throw<ClaudeNotFoundException>().Which;

        exception.CommandText
            .Should().Be("claude plugin list --json");

        exception.Reason
            .Should().Be("The Claude Code CLI claude was not found in any absolute directory of the PATH " +
                         "environment variable.");

        A.CallTo(_claudePluginCli).MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_MarketplaceAndPluginMissing_AddsMarketplaceInstallsPluginAndReturnsInstalled()
    {
        // Arrange
        SetUpMarketplaces([new ClaudeMarketplace("other", "github", "other/repo", "main", null)]);
        SetUpPluginLists([], [], [new ClaudeInstalledPlugin(PluginId, "0.8.0", "user", true)]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Installed, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync("CreativeCodersTeam/shade-kit#v0.8.0",
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _claudePluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_SameSourceAndPluginMissing_InstallsWithoutMarketplaceChange()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([], [new ClaudeInstalledPlugin(PluginId, "0.8.0", "user", false)]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Installed, null, "0.8.0",
                "v0.8.0", false));

        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_PluginInstalledOnlyWithProjectScope_InstallsForUser()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists(
            [new ClaudeInstalledPlugin(PluginId, "0.8.0", "project", true)],
            [
                new ClaudeInstalledPlugin(PluginId, "0.8.0", "project", true),
                new ClaudeInstalledPlugin(PluginId, "0.8.0", "user", true)
            ]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Installed);

        A.CallTo(() => _claudePluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_SameSourceAndPluginUpToDate_RefreshesMarketplaceAndReturnsCurrent()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Current, "0.8.0", "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _claudePluginCli.InstallPluginAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _claudePluginCli.ListPluginsAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _claudePluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task InstallAsync_KeptBranchMarketplace_RefreshesMarketplaceBeforePluginUpdate()
    {
        // Arrange
        var mainSource = MarketplaceSource.ForGitHub(Repo, "main");
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "main")]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        var result = await _integration.InstallAsync(mainSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Updated, null, null, "main",
                true));

        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _claudePluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_KeptMarketplaceAndPluginMissing_RefreshesMarketplaceBeforeInstall()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([], [new ClaudeInstalledPlugin(PluginId, "0.8.0", "user", true)]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Installed);

        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _claudePluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Theory]
    [InlineData("v0.7.0")]
    [InlineData(null)]
    public async Task InstallAsync_MarketplaceSwitchedOrAdded_DoesNotRefreshMarketplace(string? existingRef)
    {
        // Arrange
        SetUpMarketplaces(existingRef is null ? [] : [CreateGitHubMarketplace(Repo, existingRef)]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_MarketplaceRefreshFails_ThrowsCauseUnwrappedWithoutPluginChange()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "update");
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>())
            .Which.Should().BeSameAs(failure);

        A.CallTo(() => _claudePluginCli.InstallPluginAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_KeptDirectoryMarketplace_RefreshesMarketplaceBeforePluginUpdate()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "directory", null, null, path)]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        await _integration.InstallAsync(MarketplaceSource.ForDirectory(path), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _claudePluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task InstallAsync_MarketplaceRefreshCannotStartClaude_ThrowsCauseUnwrappedWithoutPluginChange()
    {
        // Arrange
        var failure = new ClaudeUnavailableException(["plugin", "marketplace", "update"], "Cannot start.");
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeUnavailableException>())
            .Which.Should().BeSameAs(failure);

        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileKeptMarketplaceIsRefreshed_PropagatesCancellationUnwrapped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().NotBeOfType<IntegrationSwitchCanceledException>(
                "a kept marketplace was not removed, so nothing needs a repair");
    }

    [Fact]
    public async Task InstallAsync_SameSourceAndPluginUpdated_ReturnsUpdated()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Updated);
    }

    [Fact]
    public async Task InstallAsync_SameRepoOtherRef_RemovesAndAddsMarketplaceThenUpdates()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Updated, "0.7.0", "0.8.0",
                "v0.8.0", true), "an up-to-date plugin after a changed ref still counts as updated");

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _claudePluginCli.AddMarketplaceAsync("CreativeCodersTeam/shade-kit#v0.8.0",
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _claudePluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task InstallAsync_SameRepoInOtherCase_TreatsRepoAsSame()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace("creativecodersteam/SHADE-KIT", "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Current);
    }

    [Fact]
    public async Task InstallAsync_RefInOtherCase_SwitchesMarketplace()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "V0.8.0")]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Updated);

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_GitHubMarketplaceWithoutRef_SwitchesToExplicitRef()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, null)]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Updated, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_SourceWithoutRefAndMarketplaceWithoutRef_KeepsMarketplace()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, null)]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(MarketplaceSource.ForGitHub(Repo),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Current, null, null, null,
                true));
    }

    [Fact]
    public async Task InstallAsync_PluginRemovedWithOldMarketplace_InstallsAgainAndReturnsUpdated()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], [], [new ClaudeInstalledPlugin(PluginId, "0.8.0", "user", true)]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Updated, "0.7.0", "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _claudePluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_PreviousRefIsBranch_ReturnsNullPreviousKitVersion()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "main")]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.PreviousKitVersion
            .Should().BeNull();
    }

    [Fact]
    public async Task InstallAsync_TargetRefIsBranch_ReturnsNullKitVersionAndBranchRef()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        var result = await _integration.InstallAsync(MarketplaceSource.ForGitHub(Repo, "main"),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Updated, "0.7.0", null, "main",
                true));
    }

    [Fact]
    public async Task InstallAsync_DisabledPlugin_UpdatesAndReportsDisabled()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin with { Enabled = false }]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Enabled
            .Should().BeFalse();

        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_SameDirectory_KeepsMarketplace()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "directory", null, null,
            path + Path.DirectorySeparatorChar)]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(MarketplaceSource.ForDirectory(path),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Current, null, null, null,
                true));
    }

    [Fact]
    public async Task InstallAsync_DirectorySourceAndMarketplaceMissing_AddsAbsolutePath()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");
        SetUpMarketplaces([]);
        SetUpPluginLists([], [], [UserPlugin]);

        // Act
        await _integration.InstallAsync(MarketplaceSource.ForDirectory(path), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(Path.TrimEndingDirectorySeparator(path),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("github", "other/shade-kit", "v0.8.0", null, "github", "other/shade-kit#v0.8.0")]
    [InlineData("github", "other/shade-kit", null, null, "github", "other/shade-kit")]
    [InlineData("directory", null, null, OtherPath, "github", OtherPath)]
    [InlineData("git", null, null, null, "github", "a source of the kind 'git'")]
    [InlineData(null, null, null, null, "github", "a source of unknown kind")]
    [InlineData("github", null, "v0.8.0", null, "github", "a source of the kind 'github'")]
    [InlineData("github", Repo, "v0.8.0", null, "directory", "CreativeCodersTeam/shade-kit#v0.8.0")]
    [InlineData("directory", null, null, OtherPath, "directory", OtherPath)]
    [InlineData("directory", null, null, null, "directory", "a source of the kind 'directory'")]
    public async Task InstallAsync_MarketplaceWithOtherSource_ThrowsConflictWithoutChanges(string? sourceKind,
        string? repo, string? @ref, string? path, string targetKind, string expectedExistingSource)
    {
        // Arrange
        var otherPath = Path.Combine(Path.GetTempPath(), "other");
        var source = targetKind == "directory"
            ? MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "shade-kit"))
            : ReleaseSource;
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, sourceKind, repo, @ref,
            path == OtherPath ? otherPath : path)]);
        SetUpPluginLists([UserPlugin]);

        // Act
        var act = () => _integration.InstallAsync(source, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationConflictException>()).Which;

        exception.AgentId
            .Should().Be("claude");

        exception.MarketplaceName
            .Should().Be(MarketplaceName);

        exception.ExistingSource
            .Should().Be(expectedExistingSource == OtherPath ? otherPath : expectedExistingSource);

        exception.RequestedSource
            .Should().Be(source.Text);

        exception.ResolveCommands
            .Should().Equal("claude plugin marketplace remove creativecoders-shade-kit");

        AssertNoChange();
    }

    [Fact]
    public async Task InstallAsync_AddFailsAfterRemove_ThrowsIntegrationIncompleteWithCause()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "add");
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationIncompleteException>()).Which;

        exception.AgentId
            .Should().Be("claude");

        exception.InnerException
            .Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_UpdateFailsAfterSwitch_ThrowsIntegrationIncomplete()
    {
        // Arrange
        var failure = CreateFailure("update");
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationIncompleteException>())
            .WithInnerException<ClaudeCommandFailedException>();
    }

    [Fact]
    public async Task InstallAsync_InstallFailsAfterSwitch_ThrowsIntegrationIncomplete()
    {
        // Arrange
        var failure = CreateFailure("install");
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], []);
        A.CallTo(() => _claudePluginCli.InstallPluginAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationIncompleteException>())
            .Which.InnerException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_PluginNotListedAfterInstallAfterSwitch_ThrowsIntegrationIncomplete()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], [], []);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationIncompleteException>())
            .WithInnerException<ClaudeCommandFailedException>();
    }

    [Fact]
    public async Task InstallAsync_OperationCanceledAfterSwitchWithoutCanceledRunToken_PropagatesUnwrapped()
    {
        // Arrange
        var canceled = new OperationCanceledException();
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Throws(canceled);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled, "only a cancellation of the run itself gets the repair hint");
    }

    [Fact]
    public async Task InstallAsync_CanceledAfterRemove_ThrowsSwitchCanceledWithRunToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(canceled);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationSwitchCanceledException>()).Which;

        exception.AgentId
            .Should().Be("claude");

        exception.InnerException
            .Should().BeSameAs(canceled);

        exception.CancellationToken
            .Should().Be(cancellationToken);

        exception
            .Should().BeAssignableTo<OperationCanceledException>("the run still ends as canceled");
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileMarketplaceIsRemoved_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>(
            "the killed claude may already have removed the marketplace and its plugin");

        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginIsUpdatedAfterSwitch_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>();
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileMissingMarketplaceIsAdded_PropagatesCancellationUnwrapped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpMarketplaces([]);
        SetUpPluginLists([]);
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(canceled);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled, "no marketplace was removed, so nothing needs a repair");
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginsAreListedAfterSwitch_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        var listCalls = 0;
        A.CallTo(() => _claudePluginCli.ListPluginsAsync(A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                if (++listCalls == 1)
                {
                    return Task.FromResult<IReadOnlyList<ClaudeInstalledPlugin>>([UserPlugin]);
                }

                cancellationTokenSource.Cancel();

                return Task.FromCanceled<IReadOnlyList<ClaudeInstalledPlugin>>(cancellationToken);
            });

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>();
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginIsInstalledAfterSwitch_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], []);
        A.CallTo(() => _claudePluginCli.InstallPluginAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>();
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginIsUpdatedWithKeptMarketplace_PropagatesCancellationUnwrapped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(canceled);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled);
    }

    [Fact]
    public async Task InstallAsync_OperationCanceledFromRemoveWithoutCanceledRunToken_PropagatesUnwrapped()
    {
        // Arrange
        var canceled = new OperationCanceledException();
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Throws(canceled);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled);
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileMarketplacesAreListed_PropagatesCancellationUnwrapped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().NotBeOfType<IntegrationSwitchCanceledException>("nothing was changed yet");
    }

    [Fact]
    public async Task InstallAsync_PluginInstalledButMarketplaceMissing_AddsMarketplaceAndUpdates()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Updated, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync("CreativeCodersTeam/shade-kit#v0.8.0",
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _claudePluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_SwitchWithPluginNotInstalled_InstallsAndReturnsInstalled()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([], [], [UserPlugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Installed, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _claudePluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task InstallAsync_PluginAppearsAfterMarketplaceAdd_UpdatesAndReturnsInstalled()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Installed, "the plugin was not installed before the run");

        A.CallTo(() => _claudePluginCli.InstallPluginAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_SourceWithoutRefAndMarketplaceWithRef_SwitchesMarketplace()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        await _integration.InstallAsync(MarketplaceSource.ForGitHub(Repo), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(Repo, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task InstallAsync_DirectoryInOtherCaseAndCaseSensitiveComparer_ThrowsConflict()
    {
        // Arrange
        var integration = new ClaudeCodeIntegration(_claudePluginCli, _claudeExecutableLocator,
            new DirectoryPathComparer(StringComparison.Ordinal));
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "directory", null, null,
            Path.Combine(Path.GetTempPath(), "SHADE-KIT"))]);
        SetUpPluginLists([UserPlugin]);

        // Act
        var act = () => integration.InstallAsync(MarketplaceSource.ForDirectory(path),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationConflictException>("Linux compares directories case-sensitively");

        AssertNoChange();
    }

    [Fact]
    public async Task InstallAsync_DirectoryInOtherCaseAndCaseInsensitiveComparer_KeepsMarketplace()
    {
        // Arrange
        var integration = new ClaudeCodeIntegration(_claudePluginCli, _claudeExecutableLocator,
            new DirectoryPathComparer(StringComparison.OrdinalIgnoreCase));
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "directory", null, null,
            Path.Combine(Path.GetTempPath(), "SHADE-KIT"))]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await integration.InstallAsync(MarketplaceSource.ForDirectory(path),
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Current, "Windows and macOS compare directories case-insensitively");

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_DirectoryInOtherCaseOnCurrentOperatingSystem_FollowsItsCaseSensitivity()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "directory", null, null,
            Path.Combine(Path.GetTempPath(), "SHADE-KIT"))]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var act = () => _integration.InstallAsync(MarketplaceSource.ForDirectory(path),
            TestContext.Current.CancellationToken);

        // Assert
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            (await act.Should().NotThrowAsync()).Subject.Outcome
                .Should().Be(IntegrationOutcome.Current);
        }
        else
        {
            await act.Should().ThrowAsync<IntegrationConflictException>();

            AssertNoChange();
        }
    }

    [Fact]
    public async Task InstallAsync_DirectoryMarketplaceThroughSymbolicLink_KeepsMarketplace()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("shade-kit");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(target, "shade-kit-link");
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "directory", null, null, link)]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(MarketplaceSource.ForDirectory(target),
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Current);

        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_SourceThroughSymbolicLinkInParentDirectory_KeepsMarketplace()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("work", "shade-kit");
        var parentLink = tempDirectory.CreateDirectorySymbolicLinkOrSkip(Path.GetDirectoryName(target)!,
            "work-link");
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "directory", null, null,
            target + Path.DirectorySeparatorChar)]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(
            MarketplaceSource.ForDirectory(Path.Combine(parentLink, "shade-kit")),
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Current);
    }

    [Fact]
    public async Task InstallAsync_SymbolicLinkToOtherDirectory_ThrowsConflict()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("shade-kit");
        var other = tempDirectory.CreateDirectory("other");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(other, "shade-kit-link");
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "directory", null, null, link)]);
        SetUpPluginLists([UserPlugin]);

        // Act
        var act = () => _integration.InstallAsync(MarketplaceSource.ForDirectory(target),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationConflictException>())
            .Which.ExistingSource.Should().Be(link);

        AssertNoChange();
    }

    [Fact]
    public void Constructor_NullDirectoryPathComparer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ClaudeCodeIntegration(_claudePluginCli, _claudeExecutableLocator, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("directoryPathComparer");
    }

    [Fact]
    public async Task InstallAsync_DirectoryMarketplaceWithRelativePath_ThrowsConflictNamingPath()
    {
        // Arrange
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "directory", null, null, "shade-kit")]);
        SetUpPluginLists([UserPlugin]);

        // Act
        var act = () => _integration.InstallAsync(
            MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "shade-kit")),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationConflictException>())
            .Which.ExistingSource.Should().Be("shade-kit");
    }

    [Fact]
    public async Task InstallAsync_MarketplaceNotFirstInList_UsesMarketplaceByName()
    {
        // Arrange
        SetUpMarketplaces(
        [
            new ClaudeMarketplace("other", "github", "other/repo", "main", null),
            CreateGitHubMarketplace(Repo, "v0.8.0")
        ]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Current);
    }

    [Fact]
    public async Task InstallAsync_RemoveFails_ThrowsCauseUnwrapped()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "remove");
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_AddFailsForMissingMarketplace_ThrowsCauseUnwrapped()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "add");
        SetUpMarketplaces([]);
        SetUpPluginLists([]);
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_UpdateFailsWithKeptMarketplace_ThrowsCauseUnwrapped()
    {
        // Arrange
        var failure = CreateFailure("update");
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_PluginNotListedAfterInstall_ThrowsClaudeCommandFailed()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([], []);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<ClaudeCommandFailedException>()).Which;

        exception.CommandText
            .Should().Be("claude plugin list --json");

        exception.Reason
            .Should().Be("claude does not list the plugin 'shade-kit@creativecoders-shade-kit' with the scope " +
                         "'user' after it was installed.");
    }

    [Fact]
    public async Task InstallAsync_ClaudeNotFound_PropagatesClaudeNotFoundException()
    {
        // Arrange
        A.CallTo(() => _claudePluginCli.ListPluginsAsync(A<CancellationToken>._))
            .Throws(new ClaudeNotFoundException(["plugin", "list", "--json"], "Not on the PATH."));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ClaudeNotFoundException>();
    }

    [Fact]
    public async Task InstallAsync_Canceled_PassesTokenToEveryCommand()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        await _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(cancellationToken)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _claudePluginCli.ListPluginsAsync(cancellationToken)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(MarketplaceName, cancellationToken))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, cancellationToken))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(PluginId, cancellationToken)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_KeptMarketplace_PassesTokenToMarketplaceRefresh()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        await _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(MarketplaceName, cancellationToken))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdateAsync_PluginNotInstalled_ReturnsNullWithoutChanges()
    {
        // Arrange
        SetUpPluginLists([new ClaudeInstalledPlugin("other@market", "1.0.0", "user", true)]);

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeNull();

        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustNotHaveHappened();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_PluginNotInstalled_DoesNotResolveSource()
    {
        // Arrange
        var getSource = A.Fake<Func<CancellationToken, Task<MarketplaceSource>>>();
        SetUpPluginLists([]);

        // Act
        var result = await _integration.UpdateAsync(getSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeNull();

        A.CallTo(getSource).MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateAsync_PluginInstalled_ResolvesSourceOnceWithTokenBeforeMarketplaceIsRead()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var getSource = A.Fake<Func<CancellationToken, Task<MarketplaceSource>>>();
        A.CallTo(() => getSource(A<CancellationToken>._)).Returns(ReleaseSource);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        await _integration.UpdateAsync(getSource, cancellationToken);

        // Assert
        A.CallTo(() => _claudePluginCli.ListPluginsAsync(cancellationToken))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => getSource(cancellationToken)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(cancellationToken))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task UpdateAsync_KeptMarketplace_ListsPluginsAndMarketplacesOnceEach()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "main")]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.UpdateAsync(
            _ => Task.FromResult(MarketplaceSource.ForGitHub(Repo, "main")), TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Current, null, null, "main",
                true));

        A.CallTo(() => _claudePluginCli.ListPluginsAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _claudePluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task UpdateAsync_GetSourceFails_PropagatesFailureWithoutChanges()
    {
        // Arrange
        var failure = new InvalidMarketplaceSourceException("-x", "It starts with '-'.");
        SetUpPluginLists([UserPlugin]);

        // Act
        var act = () => _integration.UpdateAsync(_ => Task.FromException<MarketplaceSource>(failure),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidMarketplaceSourceException>())
            .Which.Should().BeSameAs(failure);

        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustNotHaveHappened();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_GetSourceCanceled_PropagatesCancellationUnwrappedWithoutChanges()
    {
        // Arrange
        var canceled = new OperationCanceledException(new CancellationToken(true));
        SetUpPluginLists([UserPlugin]);

        // Act
        var act = () => _integration.UpdateAsync(_ => Task.FromException<MarketplaceSource>(canceled),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled);

        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustNotHaveHappened();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_PluginInstalledWithOtherRef_ListsPluginsTwiceAndMarketplacesOnceWithoutRefresh()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _claudePluginCli.ListPluginsAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateAsync_CanceledDuringMarketplaceSwitch_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.UpdateAsync(GetReleaseSourceAsync, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>();
    }

    [Fact]
    public async Task UpdateAsync_GetSourceReturnsNull_ThrowsArgumentNullExceptionWithoutChanges()
    {
        // Arrange
        SetUpPluginLists([UserPlugin]);

        // Act
        var act = () => _integration.UpdateAsync(_ => Task.FromResult<MarketplaceSource>(null!),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_PluginInstalledOnlyWithProjectScope_ReturnsNull()
    {
        // Arrange
        SetUpPluginLists([new ClaudeInstalledPlugin(PluginId, "0.8.0", "project", true)]);

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeNull();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_PluginInstalledWithOtherRef_SwitchesMarketplaceAndUpdates()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("claude", PluginId, IntegrationOutcome.Updated, "0.7.0", "0.8.0",
                "v0.8.0", true));
    }

    [Fact]
    public async Task UpdateAsync_PluginInstalledButMarketplaceMissing_AddsMarketplaceAndUpdates()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([UserPlugin], [UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpdatedOutcome);

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result!.Outcome
            .Should().Be(IntegrationOutcome.Updated);

        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdateAsync_PluginCurrent_ReturnsCurrent()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);
        SetUpUpdate(ClaudePluginUpdateResult.UpToDateOutcome);

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result!.Outcome
            .Should().Be(IntegrationOutcome.Current);
    }

    [Fact]
    public async Task UpdateAsync_MarketplaceWithOtherRepo_ThrowsConflictWithoutChanges()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace("other/repo", "v0.8.0")]);
        SetUpPluginLists([UserPlugin]);

        // Act
        var act = () => _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationConflictException>();

        AssertNoChange();
    }

    [Fact]
    public async Task GetInstalledAsync_PluginNotInstalledForUser_ReturnsNull()
    {
        // Arrange
        SetUpPluginLists([new ClaudeInstalledPlugin(PluginId, "0.8.0", "project", true)]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().BeNull();

        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("v0.8.0", "0.8.0", true)]
    [InlineData("main", null, false)]
    [InlineData(null, null, true)]
    public async Task GetInstalledAsync_PluginInstalledWithGitHubMarketplace_ReturnsRefKitVersionAndEnabled(
        string? @ref, string? expectedKitVersion, bool enabled)
    {
        // Arrange
        SetUpPluginLists([UserPlugin with { Enabled = enabled }]);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, @ref)]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().Be(new InstalledIntegration("claude", PluginId, expectedKitVersion, @ref, enabled));
    }

    [Fact]
    public async Task GetInstalledAsync_DirectoryMarketplace_ReturnsNullRefAndKitVersion()
    {
        // Arrange
        SetUpPluginLists([UserPlugin]);
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "directory", null, null, "/src/shade-kit")]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().Be(new InstalledIntegration("claude", PluginId, null, null, true));
    }

    [Fact]
    public async Task GetInstalledAsync_MarketplaceMissing_ReturnsNullRefAndKitVersion()
    {
        // Arrange
        SetUpPluginLists([UserPlugin]);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0") with { Name = "other" }]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().Be(new InstalledIntegration("claude", PluginId, null, null, true));
    }

    [Fact]
    public async Task GetInstalledAsync_MarketplaceOfOtherKindWithRef_ReturnsNullRefAndKitVersion()
    {
        // Arrange
        SetUpPluginLists([UserPlugin]);
        SetUpMarketplaces([new ClaudeMarketplace(MarketplaceName, "git", null, "v0.8.0", null)]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().Be(new InstalledIntegration("claude", PluginId, null, null, true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("User")]
    public async Task GetInstalledAsync_PluginWithOtherScope_ReturnsNull(string? scope)
    {
        // Arrange
        SetUpPluginLists([UserPlugin with { Scope = scope }]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().BeNull();
    }

    [Fact]
    public async Task GetInstalledAsync_MarketplaceListFails_PropagatesFailure()
    {
        // Arrange
        SetUpPluginLists([UserPlugin]);
        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .Throws(CreateFailure("marketplace", "list"));

        // Act
        var act = () => _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ClaudeCommandFailedException>();
    }

    [Fact]
    public async Task GetInstalledAsync_ListFails_PropagatesFailure()
    {
        // Arrange
        A.CallTo(() => _claudePluginCli.ListPluginsAsync(A<CancellationToken>._)).Throws(CreateFailure("list"));

        // Act
        var act = () => _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ClaudeCommandFailedException>();
    }

    private static Task<MarketplaceSource> GetReleaseSourceAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ReleaseSource);
    }

    private static ClaudeMarketplace CreateGitHubMarketplace(string repo, string? @ref)
    {
        return new ClaudeMarketplace(MarketplaceName, "github", repo, @ref, null);
    }

    private static ClaudeCommandFailedException CreateFailure(params string[] command)
    {
        return new ClaudeCommandFailedException(["plugin", .. command, "--json"], 1, "claude failed.");
    }

    private void SetUpMarketplaces(IReadOnlyList<ClaudeMarketplace> marketplaces)
    {
        A.CallTo(() => _claudePluginCli.ListMarketplacesAsync(A<CancellationToken>._)).Returns(marketplaces);
    }

    // Each call of ListPluginsAsync returns the next list.
    private void SetUpPluginLists(IReadOnlyList<ClaudeInstalledPlugin> first,
        IReadOnlyList<ClaudeInstalledPlugin>? second = null, IReadOnlyList<ClaudeInstalledPlugin>? third = null)
    {
        IReadOnlyList<ClaudeInstalledPlugin>[] pluginLists =
            [.. new[] { first, second, third }.OfType<IReadOnlyList<ClaudeInstalledPlugin>>()];

        A.CallTo(() => _claudePluginCli.ListPluginsAsync(A<CancellationToken>._))
            .ReturnsNextFromSequence(pluginLists);
    }

    private void SetUpUpdate(string updateOutcome)
    {
        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._))
            .Returns(new ClaudePluginUpdateResult(updateOutcome, "0.7.0", "0.8.0"));
    }

    private void AssertNoChange()
    {
        A.CallTo(() => _claudePluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _claudePluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _claudePluginCli.UpdateMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _claudePluginCli.InstallPluginAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _claudePluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
