using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Codex;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Codex;

// The plugin CLI is faked, so these tests never start codex.
public class CodexCliIntegrationTests
{
    private const string PluginId = "shade-kit@creativecoders-shade-kit";

    private const string MarketplaceName = "creativecoders-shade-kit";

    private const string Repo = "CreativeCodersTeam/shade-kit";

    private const string GitHubUrl = "https://github.com/CreativeCodersTeam/shade-kit.git";

    private const string Root = "/home/me/.codex/.tmp/marketplaces/creativecoders-shade-kit";

    private const string RemoveCommand = "codex plugin marketplace remove creativecoders-shade-kit";

    private const string AcInstall = "FP08-AC3";

    private static readonly MarketplaceSource ReleaseSource = MarketplaceSource.ForGitHub(Repo, "v0.8.0");

    private static readonly MarketplaceSource DirectorySource =
        MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "shade-kit-src"));

    private static readonly CodexInstalledPlugin Plugin = new(PluginId, "shade-kit", MarketplaceName, "local", true,
        true);

    private static readonly CodexMarketplace GitHubMarketplace = new(MarketplaceName, Root, "git", GitHubUrl);

    private readonly ICodexPluginCli _codexPluginCli = A.Fake<ICodexPluginCli>();

    private readonly ICodexExecutableLocator _codexExecutableLocator = A.Fake<ICodexExecutableLocator>();

    private readonly CodexCliIntegration _integration;

    public CodexCliIntegrationTests()
    {
        _integration = new CodexCliIntegration(_codexPluginCli, _codexExecutableLocator);

        SetUpMarketplaceAdds(Added(false));
    }

    [Fact]
    public void Constructor_NullCodexPluginCli_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CodexCliIntegration(null!, _codexExecutableLocator);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("codexPluginCli");
    }

    [Fact]
    public void Constructor_NullCodexExecutableLocator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CodexCliIntegration(_codexPluginCli, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("codexExecutableLocator");
    }

    [Fact]
    public void Constructor_NullDirectoryPathComparer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CodexCliIntegration(_codexPluginCli, _codexExecutableLocator, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("directoryPathComparer");
    }

    [Fact]
    public void AgentId_Always_IsCodex()
    {
        // Assert
        _integration.AgentId
            .Should().Be("codex");

        CodexCliIntegration.CodexAgentId
            .Should().Be("codex");
    }

    [Theory]
    [InlineData("/usr/local/bin/codex", true)]
    [InlineData(@"C:\npm\codex.cmd", true)]
    [InlineData(null, false)]
    public void IsAvailable_CodexOnPathOrNot_ReturnsWhetherCodexIsFound(string? executablePath, bool expected)
    {
        // Arrange
        A.CallTo(() => _codexExecutableLocator.FindCodexExecutable()).Returns(executablePath);

        // Act
        var isAvailable = _integration.IsAvailable();

        // Assert
        isAvailable
            .Should().Be(expected);
    }

    [Fact]
    public void EnsureAvailable_CodexOnPath_DoesNotThrow()
    {
        // Arrange
        A.CallTo(() => _codexExecutableLocator.FindCodexExecutable()).Returns("/usr/local/bin/codex");

        // Act
        var act = () => _integration.EnsureAvailable();

        // Assert
        act.Should().NotThrow();

        A.CallTo(_codexPluginCli).MustNotHaveHappened();
    }

    [Fact]
    public void EnsureAvailable_CodexNotOnPath_ThrowsCodexNotFoundNamingFirstCommand()
    {
        // Arrange
        A.CallTo(() => _codexExecutableLocator.FindCodexExecutable()).Returns(null);

        // Act
        var act = () => _integration.EnsureAvailable();

        // Assert
        var exception = act.Should().Throw<CodexNotFoundException>().Which;

        exception.CommandText
            .Should().Be("codex plugin list --json");

        exception.Reason
            .Should().Be("The OpenAI Codex CLI codex was not found in any absolute directory of the PATH " +
                         "environment variable.");

        A.CallTo(_codexPluginCli).MustNotHaveHappened();
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
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_MarketplaceAndPluginMissing_AddsMarketplaceThenPluginAndReturnsInstalled()
    {
        // Arrange
        SetUpMarketplaces([new CodexMarketplace("other", "/r", "git", "https://github.com/other/repo.git")]);
        SetUpPluginLists([], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Installed, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _codexPluginCli.ListPluginsAsync(A<CancellationToken>._))
                .MustHaveHappened());

        A.CallTo(() => _codexPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        AssertNotCalled(upgrade: true, removeMarketplace: true, removePlugin: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_PluginListedButMarketplaceMissing_AddsMarketplaceAndPluginAndReturnsUpdated()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Updated, null, "0.8.0",
                "v0.8.0", true), "the plugin was installed before the run");

        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        AssertNotCalled(upgrade: true, removeMarketplace: true, removePlugin: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_KeptGitMarketplaceWithInstalledPlugin_ChecksSourceUpgradesAddsPluginAndReturnsUpdated()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Added(true));

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Updated, "0.8.0", "0.8.0",
                "v0.8.0", true), "codex reports no up-to-date state, so a kept plugin is never current");

        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.UpgradeMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _codexPluginCli.ListPluginsAsync(A<CancellationToken>._))
                .MustHaveHappened());

        A.CallTo(() => _codexPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        AssertNotCalled(removeMarketplace: true, removePlugin: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_KeptGitMarketplaceWithoutPlugin_UpgradesAddsPluginAndReturnsInstalled()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([], [Plugin]);
        SetUpMarketplaceAdds(Added(true));

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Installed, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _codexPluginCli.UpgradeMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        AssertNotCalled(removeMarketplace: true, removePlugin: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_KeptDirectoryMarketplace_AddsPluginWithoutMarketplaceAddOrUpgrade()
    {
        // Arrange
        SetUpMarketplaces([new CodexMarketplace(MarketplaceName, DirectorySource.Text, "local", DirectorySource.Text)]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(DirectorySource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Updated, null, null, null, true));

        A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        AssertNotCalled(addMarketplace: true, upgrade: true, removeMarketplace: true, removePlugin: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_DirectorySourceAndMarketplaceMissing_AddsDirectoryAndPlugin()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(DirectorySource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Installed, null, null, null,
                true));

        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(DirectorySource, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        AssertNotCalled(upgrade: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_GitMarketplaceWithOtherSourceAndInstalledPlugin_RemovesPluginAndMarketplaceAndAddsBoth()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Refused(), Added(false));

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Updated, null, "0.8.0",
                "v0.8.0", true), "codex does not report the ref, so the kit version before the switch is unknown");

        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, A<CancellationToken>._))
            .MustHaveHappened()
            .Then(A.CallTo(() => _codexPluginCli.RemovePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, A<CancellationToken>._))
                .MustHaveHappened())
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        AssertNotCalled(upgrade: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_GitMarketplaceWithOtherSourceWithoutPlugin_RemovesMarketplaceWithoutPluginRemove()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([], [Plugin]);
        SetUpMarketplaceAdds(Refused(), Added(false));

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Installed, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, A<CancellationToken>._))
                .MustHaveHappened())
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        AssertNotCalled(removePlugin: true, upgrade: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_GitMarketplaceAddedByCheck_AddsPluginWithoutUpgradeOrRemoval()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Added(false));

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Updated, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        AssertNotCalled(upgrade: true, removeMarketplace: true, removePlugin: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_GitHubMarketplaceAndSourceWithoutRef_ChecksSourceInsteadOfKeepingUnchecked()
    {
        // Arrange
        var source = MarketplaceSource.ForGitHub(Repo);
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Refused(), Added(false));

        // Act
        var result = await _integration.InstallAsync(source, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Updated, null, null, null, true),
                "a marketplace without a reported ref may still have one, so codex is asked");

        A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_KeptGitMarketplaceWithBranchRef_ReturnsNullKitVersionsAndBranchRef()
    {
        // Arrange
        var source = MarketplaceSource.ForGitHub(Repo, "main");
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Added(true));

        // Act
        var result = await _integration.InstallAsync(source, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Updated, null, null, "main",
                true));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    [InlineData(-1)]
    public async Task InstallAsync_CheckAddFailsWithAnyNonZeroExitCode_SwitchesMarketplace(int exitCode)
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(
            () => Task.FromException<CodexMarketplaceAddResult>(new CodexCommandFailedException(
                ["plugin", "marketplace", "add", Repo, "--ref", "v0.8.0", "--json"], exitCode, "Failed.")),
            Added(false));

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Updated);

        A.CallTo(() => _codexPluginCli.RemovePluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_GitHubMarketplaceUrlInOtherCase_ChecksSourceAndSwitchesWhenRefused()
    {
        // Arrange
        SetUpMarketplaces([new CodexMarketplace(MarketplaceName, Root, "git",
            "https://github.com/creativecodersteam/shade-kit.git")]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Refused(), Added(false));

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Updated, "GitHub compares repositories case-insensitively");

        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, A<CancellationToken>._))
            .MustHaveHappened()
            .Then(A.CallTo(() => _codexPluginCli.RemovePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, A<CancellationToken>._))
                .MustHaveHappened())
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginsAreRelistedAfterSwitch_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpMarketplaceAdds(Refused(), Added(false));
        var listCalls = 0;
        A.CallTo(() => _codexPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                if (++listCalls == 1)
                {
                    return Task.FromResult<IReadOnlyList<CodexInstalledPlugin>>([Plugin]);
                }

                cancellationTokenSource.Cancel();
                return Task.FromException<IReadOnlyList<CodexInstalledPlugin>>(
                    new OperationCanceledException(cancellationToken));
            });

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task InstallAsync_CheckAddRefusedBeforeStartOrUnreadable_PropagatesWithoutSwitch(int? exitCode)
    {
        // Arrange
        var failure = new CodexCommandFailedException(["plugin", "marketplace", "add", Repo], exitCode, "Broken.");
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(() => Task.FromException<CodexMarketplaceAddResult>(failure));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexCommandFailedException>())
            .Which.Should().BeSameAs(failure, "only a non-zero exit code means that codex refused the source");

        AssertNotCalled(upgrade: true, removeMarketplace: true, removePlugin: true, addPlugin: true);
    }

    [Fact]
    public async Task InstallAsync_CheckAddCodexUnavailable_PropagatesWithoutSwitch()
    {
        // Arrange
        var failure = new CodexUnavailableException(["plugin", "marketplace", "add", Repo], "Cannot start.");
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(() => Task.FromException<CodexMarketplaceAddResult>(failure));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexUnavailableException>())
            .Which.Should().BeSameAs(failure);

        AssertNotCalled(upgrade: true, removeMarketplace: true, removePlugin: true, addPlugin: true);
    }

    [Theory]
    [InlineData("git", "https://github.com/other/repo.git", "other/repo")]
    [InlineData("git", "git@github.com:CreativeCodersTeam/shade-kit.git", "a source of the kind 'git'")]
    [InlineData("local", "/src/shade-kit", "/src/shade-kit")]
    [InlineData(null, null, "a source of unknown kind")]
    public async Task InstallAsync_MarketplaceWithOtherSource_ThrowsConflictWithoutChanges(string? sourceType,
        string? source, string expectedExistingSource)
    {
        // Arrange
        SetUpMarketplaces([new CodexMarketplace(MarketplaceName, Root, sourceType, source)]);
        SetUpPluginLists([Plugin]);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationConflictException>()).Which;

        exception.AgentId
            .Should().Be("codex");

        exception.ExistingSource
            .Should().Be(expectedExistingSource);

        exception.RequestedSource
            .Should().Be("CreativeCodersTeam/shade-kit#v0.8.0");

        exception.ResolveCommands
            .Should().Equal(RemoveCommand);

        AssertNoChange();
    }

    [Fact]
    public async Task InstallAsync_DirectorySourceAndGitHubMarketplace_ThrowsConflictWithoutChanges()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);

        // Act
        var act = () => _integration.InstallAsync(DirectorySource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationConflictException>())
            .Which.ExistingSource.Should().Be(Repo);

        AssertNoChange();
    }

    [Fact]
    public async Task InstallAsync_MarketplaceNotFirstInList_UsesMarketplaceByName()
    {
        // Arrange
        SetUpMarketplaces(
        [
            new CodexMarketplace("other", "/r", "local", "/src/other"),
            GitHubMarketplace
        ]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Added(true));

        // Act
        await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _codexPluginCli.UpgradeMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_PluginListedWithInstalledFalse_DoesNotCountAsInstalled()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([Plugin with { Installed = false }], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Installed);
    }

    [Fact]
    public async Task InstallAsync_PluginListedOnlyWithInstalledFalseAfterAdd_ThrowsCodexCommandFailed()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([], [Plugin with { Installed = false }]);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<CodexCommandFailedException>();
    }

    [Theory]
    [InlineData("shade-kit", "other")]
    [InlineData("Shade-Kit", MarketplaceName)]
    [InlineData("shade-kit", "CreativeCoders-Shade-Kit")]
    [InlineData("other-plugin", MarketplaceName)]
    public async Task InstallAsync_PluginOfOtherNameOrMarketplace_DoesNotCountAsInstalled(string name,
        string marketplace)
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([Plugin with { Name = name, MarketplaceName = marketplace }], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Installed);
    }

    [Fact]
    public async Task InstallAsync_PluginDisabledAfterAdd_ReportsDisabled()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin with { Enabled = false }], [Plugin with { Enabled = false }]);
        SetUpMarketplaceAdds(Added(true));

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Enabled
            .Should().BeFalse();
    }

    [Fact]
    public async Task InstallAsync_EnabledStateAfterAdd_ComesFromRereadList()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin with { Enabled = false }], [Plugin]);
        SetUpMarketplaceAdds(Added(true));

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Enabled
            .Should().BeTrue("codex plugin add enables the plugin, and the result reports the listed state");
    }

    [Fact]
    public async Task InstallAsync_PluginNotListedAfterAdd_ThrowsCodexCommandFailedNamingListCommand()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([], []);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CodexCommandFailedException>()).Which;

        exception.CommandText
            .Should().Be("codex plugin list --json");

        exception.ExitCode
            .Should().Be(0);

        exception.Reason
            .Should().Be("codex does not list the plugin 'shade-kit' of the marketplace 'creativecoders-shade-kit' " +
                         "as installed after it was added.");
    }

    [Fact]
    public async Task InstallAsync_PluginNotListedAfterAddAfterSwitch_ThrowsIntegrationIncomplete()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], []);
        SetUpMarketplaceAdds(Refused(), Added(false));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationIncompleteException>())
            .WithInnerException<CodexCommandFailedException>();
    }

    [Fact]
    public async Task InstallAsync_UpgradeFailsWithKeptMarketplace_ThrowsCauseUnwrappedWithoutPluginAdd()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "upgrade", MarketplaceName, "--json");
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Added(true));
        A.CallTo(() => _codexPluginCli.UpgradeMarketplaceAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexCommandFailedException>())
            .Which.Should().BeSameAs(failure, "a kept marketplace is not removed, so nothing needs a repair");

        AssertNotCalled(addPlugin: true, removeMarketplace: true, removePlugin: true);
    }

    [Fact]
    public async Task InstallAsync_PluginAddFailsWithKeptMarketplace_ThrowsCauseUnwrapped()
    {
        // Arrange
        var failure = CreateFailure("add", PluginId, "--json");
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Added(true));
        A.CallTo(() => _codexPluginCli.AddPluginAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexCommandFailedException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_MarketplaceAddFailsForMissingMarketplace_ThrowsCauseUnwrapped()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "add", Repo, "--ref", "v0.8.0", "--json");
        SetUpMarketplaces([]);
        SetUpPluginLists([]);
        SetUpMarketplaceAdds(() => Task.FromException<CodexMarketplaceAddResult>(failure));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexCommandFailedException>())
            .Which.Should().BeSameAs(failure, "a missing marketplace is not switched");

        AssertNotCalled(removeMarketplace: true, removePlugin: true, addPlugin: true);
    }

    [Fact]
    public async Task InstallAsync_PluginRemoveFailsInSwitch_ThrowsCauseUnwrappedWithoutRemovingMarketplace()
    {
        // Arrange
        var failure = CreateFailure("remove", PluginId, "--json");
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Refused());
        A.CallTo(() => _codexPluginCli.RemovePluginAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexCommandFailedException>())
            .Which.Should().BeSameAs(failure, "a failed remove did not remove the plugin");

        AssertNotCalled(removeMarketplace: true, addPlugin: true);
    }

    [Fact]
    public async Task InstallAsync_MarketplaceRemoveFailsAsFirstRemoval_ThrowsCauseUnwrapped()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "remove", MarketplaceName, "--json");
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([]);
        SetUpMarketplaceAdds(Refused());
        A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CodexCommandFailedException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_MarketplaceRemoveFailsAfterPluginRemove_ThrowsIntegrationIncompleteWithCause()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "remove", MarketplaceName, "--json");
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Refused());
        A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationIncompleteException>()).Which;

        exception.AgentId
            .Should().Be("codex");

        exception.InnerException
            .Should().BeSameAs(failure);

        AssertNotCalled(addPlugin: true);
    }

    [Fact]
    public async Task InstallAsync_MarketplaceAddFailsAfterRemove_ThrowsIntegrationIncompleteWithCause()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "add", Repo, "--ref", "v0.8.0", "--json");
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([]);
        SetUpMarketplaceAdds(Refused(), () => Task.FromException<CodexMarketplaceAddResult>(failure));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationIncompleteException>())
            .Which.InnerException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_PluginAddFailsAfterSwitch_ThrowsIntegrationIncomplete()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Refused(), Added(false));
        A.CallTo(() => _codexPluginCli.AddPluginAsync(A<string>._, A<CancellationToken>._))
            .Throws(CreateFailure("add", PluginId, "--json"));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationIncompleteException>();
    }

    [Fact]
    public async Task InstallAsync_CodexNotFoundAfterPluginRemove_ThrowsIntegrationIncomplete()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Refused());
        A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Throws(new CodexNotFoundException(["plugin", "marketplace", "remove", MarketplaceName, "--json"],
                "Gone."));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationIncompleteException>())
            .WithInnerException<CodexNotFoundException>();
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileSourceIsChecked_PropagatesCancellationUnwrappedWithoutRemoval()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Canceled(cancellationTokenSource, canceled));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled, "the check changes nothing, so nothing needs a repair");

        AssertNotCalled(removeMarketplace: true, removePlugin: true, upgrade: true);
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginIsRemoved_ThrowsSwitchCanceledWithoutMarketplaceRemoval()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Refused());
        A.CallTo(() => _codexPluginCli.RemovePluginAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>(
            "the killed codex may already have removed the plugin");

        AssertNotCalled(removeMarketplace: true);
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileMarketplaceIsRemovedWithoutPlugin_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([]);
        SetUpMarketplaceAdds(Refused());
        A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>();
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileMarketplaceIsAddedAfterRemove_ThrowsSwitchCanceledWithRunToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Refused(), Canceled(cancellationTokenSource, canceled));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationSwitchCanceledException>()).Which;

        exception.AgentId
            .Should().Be("codex");

        exception.InnerException
            .Should().BeSameAs(canceled);

        exception.CancellationToken
            .Should().Be(cancellationToken);
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginIsAddedAfterSwitch_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Refused(), Added(false));
        A.CallTo(() => _codexPluginCli.AddPluginAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>();
    }

    [Fact]
    public async Task InstallAsync_OperationCanceledInSwitchWithoutCanceledRunToken_PropagatesUnwrapped()
    {
        // Arrange
        var canceled = new OperationCanceledException();
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Refused(), () => Task.FromException<CodexMarketplaceAddResult>(canceled));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled, "only a cancellation of the run itself gets the repair hint");
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileKeptMarketplaceIsUpgraded_PropagatesCancellationUnwrapped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Added(true));
        A.CallTo(() => _codexPluginCli.UpgradeMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(canceled);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled, "a kept marketplace is not removed, so nothing needs a repair");
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginIsAddedWithKeptMarketplace_PropagatesCancellationUnwrapped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Added(true));
        A.CallTo(() => _codexPluginCli.AddPluginAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(canceled);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled);
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
        SetUpMarketplaceAdds(Canceled(cancellationTokenSource, canceled));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled, "no marketplace was removed, so nothing needs a repair");
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileMarketplacesAreListed_PropagatesCancellationUnwrapped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _codexPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowExactlyAsync<OperationCanceledException>();

        AssertNoChange();
    }

    [Fact]
    public async Task InstallAsync_CodexNotFound_PropagatesCodexNotFoundException()
    {
        // Arrange
        A.CallTo(() => _codexPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .Throws(new CodexNotFoundException(["plugin", "list", "--json"], "Not found."));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<CodexNotFoundException>();

        AssertNoChange();
    }

    [Fact]
    public async Task InstallAsync_SwitchWithInstalledPlugin_PassesTokenToEveryCommand()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Refused(), Added(false));

        // Act
        await _integration.InstallAsync(ReleaseSource, token);

        // Assert
        A.CallTo(() => _codexPluginCli.ListPluginsAsync(token)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => _codexPluginCli.ListMarketplacesAsync(token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, token)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => _codexPluginCli.RemovePluginAsync(PluginId, token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(MarketplaceName, token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_KeptGitMarketplace_PassesTokenToCheckUpgradeAndPluginAdd()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Added(true));

        // Act
        await _integration.InstallAsync(ReleaseSource, token);

        // Assert
        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(ReleaseSource, token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _codexPluginCli.UpgradeMarketplaceAsync(MarketplaceName, token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdateAsync_PluginNotInstalled_ReturnsNullWithoutResolvingSourceOrChanges()
    {
        // Arrange
        SetUpPluginLists([Plugin with { MarketplaceName = "other" }, Plugin with { Installed = false }]);
        var getSourceCalls = 0;

        // Act
        var result = await _integration.UpdateAsync(_ =>
        {
            getSourceCalls++;
            return Task.FromResult(ReleaseSource);
        }, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeNull();

        getSourceCalls
            .Should().Be(0);

        A.CallTo(() => _codexPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustNotHaveHappened();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_PluginInstalled_ResolvesSourceOnceWithTokenBeforeMarketplaceIsRead()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Added(true));
        var getSource = A.Fake<Func<CancellationToken, Task<MarketplaceSource>>>();
        A.CallTo(() => getSource(A<CancellationToken>._)).Returns(ReleaseSource);

        // Act
        await _integration.UpdateAsync(getSource, token);

        // Assert
        A.CallTo(() => getSource(token))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.ListMarketplacesAsync(token))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task UpdateAsync_KeptGitMarketplace_UpgradesAddsPluginAndReturnsUpdated()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Added(true));

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Updated, "0.8.0", "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _codexPluginCli.UpgradeMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task UpdateAsync_PluginInstalledWithOtherRef_SwitchesMarketplaceAndAddsPlugin()
    {
        // Arrange
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin], [Plugin]);
        SetUpMarketplaceAdds(Refused(), Added(false));

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("codex", PluginId, IntegrationOutcome.Updated, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _codexPluginCli.RemovePluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _codexPluginCli.AddPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task UpdateAsync_CanceledDuringMarketplaceSwitch_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([GitHubMarketplace]);
        SetUpPluginLists([Plugin]);
        SetUpMarketplaceAdds(Refused());
        A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.UpdateAsync(GetReleaseSourceAsync, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>();
    }

    [Fact]
    public async Task UpdateAsync_GetSourceFails_PropagatesFailureWithoutChanges()
    {
        // Arrange
        var failure = new InvalidOperationException("No source.");
        SetUpPluginLists([Plugin]);

        // Act
        var act = () => _integration.UpdateAsync(_ => Task.FromException<MarketplaceSource>(failure),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(failure);

        A.CallTo(() => _codexPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustNotHaveHappened();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_GetSourceCanceled_PropagatesCancellationUnwrappedWithoutChanges()
    {
        // Arrange
        var canceled = new OperationCanceledException();
        SetUpPluginLists([Plugin]);

        // Act
        var act = () => _integration.UpdateAsync(_ => Task.FromException<MarketplaceSource>(canceled),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled);

        A.CallTo(() => _codexPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustNotHaveHappened();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_GetSourceReturnsNull_ThrowsArgumentNullExceptionWithoutChanges()
    {
        // Arrange
        SetUpPluginLists([Plugin]);

        // Act
        var act = () => _integration.UpdateAsync(_ => Task.FromResult<MarketplaceSource>(null!),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_MarketplaceWithOtherRepo_ThrowsConflictWithoutChanges()
    {
        // Arrange
        SetUpMarketplaces([new CodexMarketplace(MarketplaceName, Root, "git", "https://github.com/other/repo.git")]);
        SetUpPluginLists([Plugin]);

        // Act
        var act = () => _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationConflictException>())
            .Which.ResolveCommands.Should().Equal(RemoveCommand);

        AssertNoChange();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetInstalledAsync_PluginInstalled_ReturnsEnabledWithoutRefKitVersionOrMarketplaceList(
        bool enabled)
    {
        // Arrange
        SetUpPluginLists([Plugin with { Enabled = enabled }]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().Be(new InstalledIntegration("codex", PluginId, null, null, enabled),
                "codex does not report the ref of a marketplace");

        A.CallTo(() => _codexPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetInstalledAsync_NonMatchingEntriesBeforeMatchingOne_ReturnsMatchingEntry()
    {
        // Arrange
        SetUpPluginLists(
        [
            Plugin with { MarketplaceName = "other", Enabled = true },
            Plugin with { Installed = false, Enabled = true },
            Plugin with { Enabled = false }
        ]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().Be(new InstalledIntegration("codex", PluginId, null, null, false));
    }

    [Fact]
    public async Task GetInstalledAsync_PluginNotInstalled_ReturnsNull()
    {
        // Arrange
        SetUpPluginLists([Plugin with { MarketplaceName = "other" }, Plugin with { Installed = false }]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().BeNull();
    }

    [Fact]
    public async Task GetInstalledAsync_Token_PassesTokenToPluginList()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;
        SetUpPluginLists([Plugin]);

        // Act
        await _integration.GetInstalledAsync(token);

        // Assert
        A.CallTo(() => _codexPluginCli.ListPluginsAsync(token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetInstalledAsync_ListFails_PropagatesFailure()
    {
        // Arrange
        A.CallTo(() => _codexPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .Throws(CreateFailure("list", "--json"));

        // Act
        var act = () => _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<CodexCommandFailedException>();
    }

    private static Task<MarketplaceSource> GetReleaseSourceAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ReleaseSource);
    }

    private static CodexCommandFailedException CreateFailure(params string[] command)
    {
        return new CodexCommandFailedException(["plugin", .. command], 1, "codex exited with the exit code 1.");
    }

    private static Func<Task<CodexMarketplaceAddResult>> Added(bool alreadyAdded)
    {
        return () => Task.FromResult(new CodexMarketplaceAddResult(MarketplaceName, Root, alreadyAdded));
    }

    // codex refuses a marketplace of the same name with another source with a non-zero exit code.
    private static Func<Task<CodexMarketplaceAddResult>> Refused()
    {
        return () => Task.FromException<CodexMarketplaceAddResult>(new CodexCommandFailedException(
            ["plugin", "marketplace", "add", Repo, "--ref", "v0.8.0", "--json"], 1,
            "codex exited with the exit code 1: Error: marketplace 'creativecoders-shade-kit' is already added " +
            "from a different source; remove it before adding this source.",
            "Error: marketplace 'creativecoders-shade-kit' is already added from a different source; remove it " +
            "before adding this source"));
    }

    private static Func<Task<CodexMarketplaceAddResult>> Canceled(CancellationTokenSource cancellationTokenSource,
        OperationCanceledException canceled)
    {
        return () =>
        {
            cancellationTokenSource.Cancel();
            return Task.FromException<CodexMarketplaceAddResult>(canceled);
        };
    }

    private void SetUpMarketplaces(IReadOnlyList<CodexMarketplace> marketplaces)
    {
        A.CallTo(() => _codexPluginCli.ListMarketplacesAsync(A<CancellationToken>._)).Returns(marketplaces);
    }

    // Each call of AddMarketplaceAsync gets the next answer; the last answer repeats.
    private void SetUpMarketplaceAdds(params Func<Task<CodexMarketplaceAddResult>>[] answers)
    {
        var calls = 0;

        A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .ReturnsLazily(() => answers[Math.Min(calls++, answers.Length - 1)]());
    }

    // Each call of ListPluginsAsync returns the next list.
    private void SetUpPluginLists(IReadOnlyList<CodexInstalledPlugin> first,
        IReadOnlyList<CodexInstalledPlugin>? second = null)
    {
        IReadOnlyList<CodexInstalledPlugin>[] pluginLists =
            [.. new[] { first, second }.OfType<IReadOnlyList<CodexInstalledPlugin>>()];

        A.CallTo(() => _codexPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .ReturnsNextFromSequence(pluginLists);
    }

    private void AssertNotCalled(bool addMarketplace = false, bool upgrade = false, bool removeMarketplace = false,
        bool addPlugin = false, bool removePlugin = false)
    {
        if (addMarketplace)
        {
            A.CallTo(() => _codexPluginCli.AddMarketplaceAsync(A<MarketplaceSource>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        if (upgrade)
        {
            A.CallTo(() => _codexPluginCli.UpgradeMarketplaceAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        if (removeMarketplace)
        {
            A.CallTo(() => _codexPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        if (addPlugin)
        {
            A.CallTo(() => _codexPluginCli.AddPluginAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        if (removePlugin)
        {
            A.CallTo(() => _codexPluginCli.RemovePluginAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }
    }

    private void AssertNoChange()
    {
        AssertNotCalled(addMarketplace: true, upgrade: true, removeMarketplace: true, addPlugin: true,
            removePlugin: true);
    }
}
