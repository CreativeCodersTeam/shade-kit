using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Copilot;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Copilot;

// The plugin CLI is faked, so these tests never start copilot.
public class CopilotCliIntegrationTests
{
    private const string PluginId = "shade-kit@creativecoders-shade-kit";

    private const string MarketplaceName = "creativecoders-shade-kit";

    private const string Repo = "CreativeCodersTeam/shade-kit";

    private static readonly string[] ResolveCommands =
    [
        "copilot plugin uninstall shade-kit@creativecoders-shade-kit",
        "copilot plugin marketplace remove creativecoders-shade-kit"
    ];

    private const string AcInstall = "FP08-AC2";

    // Replaced by an absolute path of the current operating system.
    private const string OtherPath = "<other path>";

    private static readonly MarketplaceSource ReleaseSource = MarketplaceSource.ForGitHub(Repo, "v0.8.0");

    private static readonly CopilotInstalledPlugin Plugin = new("shade-kit", MarketplaceName, "0.7.0", true);

    private readonly ICopilotPluginCli _copilotPluginCli = A.Fake<ICopilotPluginCli>();

    private readonly ICopilotExecutableLocator _copilotExecutableLocator = A.Fake<ICopilotExecutableLocator>();

    private readonly CopilotCliIntegration _integration;

    public CopilotCliIntegrationTests()
    {
        _integration = new CopilotCliIntegration(_copilotPluginCli, _copilotExecutableLocator);
    }

    [Fact]
    public void Constructor_NullCopilotPluginCli_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CopilotCliIntegration(null!, _copilotExecutableLocator);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("copilotPluginCli");
    }

    [Fact]
    public void Constructor_NullCopilotExecutableLocator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CopilotCliIntegration(_copilotPluginCli, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("copilotExecutableLocator");
    }

    [Fact]
    public void Constructor_NullDirectoryPathComparer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CopilotCliIntegration(_copilotPluginCli, _copilotExecutableLocator, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("directoryPathComparer");
    }

    [Fact]
    public void AgentId_Always_IsCopilot()
    {
        // Assert
        _integration.AgentId
            .Should().Be("copilot");

        CopilotCliIntegration.CopilotAgentId
            .Should().Be("copilot");
    }

    [Theory]
    [InlineData("/usr/local/bin/copilot", true)]
    [InlineData(@"C:\npm\copilot.cmd", true)]
    [InlineData(null, false)]
    public void IsAvailable_CopilotOnPathOrNot_ReturnsWhetherCopilotIsFound(string? executablePath, bool expected)
    {
        // Arrange
        A.CallTo(() => _copilotExecutableLocator.FindCopilotExecutable()).Returns(executablePath);

        // Act
        var isAvailable = _integration.IsAvailable();

        // Assert
        isAvailable
            .Should().Be(expected);
    }

    [Fact]
    public void EnsureAvailable_CopilotOnPath_DoesNotThrow()
    {
        // Arrange
        A.CallTo(() => _copilotExecutableLocator.FindCopilotExecutable()).Returns("/usr/local/bin/copilot");

        // Act
        var act = () => _integration.EnsureAvailable();

        // Assert
        act.Should().NotThrow();

        A.CallTo(_copilotPluginCli).MustNotHaveHappened();
    }

    [Fact]
    public void EnsureAvailable_CopilotNotOnPath_ThrowsCopilotNotFoundNamingFirstCommand()
    {
        // Arrange
        A.CallTo(() => _copilotExecutableLocator.FindCopilotExecutable()).Returns(null);

        // Act
        var act = () => _integration.EnsureAvailable();

        // Assert
        var exception = act.Should().Throw<CopilotNotFoundException>().Which;

        exception.CommandText
            .Should().Be("copilot plugin list --json");

        exception.Reason
            .Should().Be("The GitHub Copilot CLI copilot was not found in any absolute directory of the PATH " +
                         "environment variable.");

        A.CallTo(_copilotPluginCli).MustNotHaveHappened();
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
    public async Task InstallAsync_MarketplaceAndPluginMissing_AddsMarketplaceRereadsInstallsAndReturnsInstalled()
    {
        // Arrange
        SetUpMarketplaces([new CopilotMarketplace("other", "github", "other/repo", "main", null)]);
        SetUpPluginLists([], [], [Plugin with { Version = "0.8.0" }]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Installed, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync("CreativeCodersTeam/shade-kit#v0.8.0",
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _copilotPluginCli.ListPluginsAsync(A<CancellationToken>._))
                .MustHaveHappened())
            .Then(A.CallTo(() => _copilotPluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        A.CallTo(() => _copilotPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .MustHaveHappened(3, Times.Exactly);

        AssertNotCalled(update: true, updateMarketplace: true, remove: true, uninstall: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_PluginListedButMarketplaceMissing_AddsMarketplaceUpdatesAndReturnsUpdated()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([Plugin], [Plugin], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Updated, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(ReleaseSource.Text, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _copilotPluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        AssertNotCalled(install: true, updateMarketplace: true, remove: true, uninstall: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_PluginAppearsAfterMarketplaceAdd_UpdatesAndReturnsInstalled()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([], [Plugin], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Installed, "the plugin was not installed before the run");

        A.CallTo(() => _copilotPluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        AssertNotCalled(install: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_KeptMarketplaceAndPluginInstalled_RefreshesMarketplaceThenUpdatesAndReturnsUpdated()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Updated, "0.8.0", "0.8.0",
                "v0.8.0", true), "copilot reports no up-to-date state, so a kept plugin is never current");

        A.CallTo(() => _copilotPluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _copilotPluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _copilotPluginCli.ListPluginsAsync(A<CancellationToken>._))
                .MustHaveHappened());

        A.CallTo(() => _copilotPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        A.CallTo(() => _copilotPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        AssertNotCalled(add: true, install: true, remove: true, uninstall: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_KeptMarketplaceAndPluginMissing_RefreshesMarketplaceThenInstalls()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([], [Plugin with { Enabled = false }]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Installed, null, "0.8.0",
                "v0.8.0", false));

        A.CallTo(() => _copilotPluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _copilotPluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        AssertNotCalled(add: true, update: true, remove: true, uninstall: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_SameRepoOtherRefWithInstalledPlugin_UninstallsRemovesAddsAndInstalls()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin], [Plugin with { Version = "0.8.0" }]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Updated, "0.7.0", "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _copilotPluginCli.UninstallPluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync("CreativeCodersTeam/shade-kit#v0.8.0",
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _copilotPluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        A.CallTo(() => _copilotPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        AssertNotCalled(update: true, updateMarketplace: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_SameRepoOtherRefWithoutInstalledPlugin_RemovesAddsAndInstallsWithoutUninstall()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Installed, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(ReleaseSource.Text, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _copilotPluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());

        AssertNotCalled(uninstall: true, update: true, updateMarketplace: true);
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_OrphanPluginGoneAfterMarketplaceAdd_InstallsAndReturnsUpdated()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([Plugin], [], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Updated, null, "0.8.0",
                "v0.8.0", true), "the plugin was installed before the run");

        A.CallTo(() => _copilotPluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        AssertNotCalled(update: true, uninstall: true, remove: true);
    }

    [Fact]
    public async Task InstallAsync_RereadAfterInstallFailsAfterSwitch_ThrowsIntegrationIncompleteWithCause()
    {
        // Arrange
        var failure = new CopilotCommandFailedException(["plugin", "list", "--json"], 0,
            "The output of copilot is not valid JSON.");
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        var listCalls = 0;
        A.CallTo(() => _copilotPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .ReturnsLazily(() => ++listCalls == 1
                ? Task.FromResult<IReadOnlyList<CopilotInstalledPlugin>>([Plugin])
                : Task.FromException<IReadOnlyList<CopilotInstalledPlugin>>(failure));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationIncompleteException>())
            .Which.InnerException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_RereadAfterMarketplaceAddFails_ThrowsCauseUnwrapped()
    {
        // Arrange
        var failure = new CopilotNotFoundException(["plugin", "list", "--json"], "Gone.");
        SetUpMarketplaces([]);
        var listCalls = 0;
        A.CallTo(() => _copilotPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .ReturnsLazily(() => ++listCalls == 1
                ? Task.FromResult<IReadOnlyList<CopilotInstalledPlugin>>([])
                : Task.FromException<IReadOnlyList<CopilotInstalledPlugin>>(failure));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CopilotNotFoundException>())
            .Which.Should().BeSameAs(failure, "nothing was removed, so nothing needs a repair");
    }

    [Theory]
    [InlineData("Shade-Kit", MarketplaceName)]
    [InlineData("shade-kit", "CreativeCoders-Shade-Kit")]
    public async Task InstallAsync_PluginNameOrMarketplaceInOtherCase_DoesNotCountAsInstalled(string name,
        string marketplace)
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin with { Name = name, Marketplace = marketplace }], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Installed);
    }

    [Fact]
    public async Task InstallAsync_GitHubMarketplaceWithoutRef_SwitchesToExplicitRef()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, null)]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.PreviousKitVersion
            .Should().BeNull();

        A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_PreviousRefIsBranch_ReturnsNullPreviousKitVersion()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "main")]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.PreviousKitVersion
            .Should().BeNull();

        result.Outcome
            .Should().Be(IntegrationOutcome.Updated);
    }

    [Fact]
    public async Task InstallAsync_TargetRefIsBranch_ReturnsNullKitVersionAndBranchRef()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([], [], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(MarketplaceSource.ForGitHub(Repo, "main"),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Installed, null, null, "main",
                true));

        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync("CreativeCodersTeam/shade-kit#main",
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_DisabledPlugin_UpdatesAndReportsDisabled()
    {
        // Arrange
        var disabledPlugin = Plugin with { Enabled = false };
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([disabledPlugin], [disabledPlugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Enabled
            .Should().BeFalse();

        result.Outcome
            .Should().Be(IntegrationOutcome.Updated);
    }

    [Fact]
    public async Task InstallAsync_EnabledStateAfterChange_ComesFromRereadList()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin], [Plugin with { Enabled = false }]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Enabled
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("other-marketplace")]
    [InlineData(null)]
    [Trait("Requirement", AcInstall)]
    public async Task InstallAsync_PluginOnlyFromOtherMarketplaceOrDirectInstall_InstallsAndReturnsInstalled(
        string? marketplace)
    {
        // Arrange
        var foreignPlugin = Plugin with { Marketplace = marketplace };
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([foreignPlugin], [foreignPlugin, Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Installed);

        result.PreviousKitVersion
            .Should().BeNull();

        A.CallTo(() => _copilotPluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        AssertNotCalled(update: true, uninstall: true);
    }

    [Fact]
    public async Task InstallAsync_PluginWithOtherNameFromShadeKitMarketplace_DoesNotCountAsInstalled()
    {
        // Arrange
        var otherPlugin = Plugin with { Name = "shade-kit-extras" };
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([otherPlugin], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(IntegrationOutcome.Installed);

        AssertNotCalled(uninstall: true);
    }

    [Fact]
    public async Task InstallAsync_SameDirectory_KeepsMarketplaceAndUpdates()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "shade-kit"));
        SetUpMarketplaces([new CopilotMarketplace(MarketplaceName, "directory", null, null, directory)]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        var result = await _integration.InstallAsync(MarketplaceSource.ForDirectory(directory),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Updated, null, null, null,
                true));

        A.CallTo(() => _copilotPluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        AssertNotCalled(add: true, remove: true, uninstall: true);
    }

    [Fact]
    public async Task InstallAsync_SameDirectoryReportedAsLocalKind_KeepsMarketplace()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "shade-kit"));
        SetUpMarketplaces([new CopilotMarketplace(MarketplaceName, "local", null, null, directory)]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        await _integration.InstallAsync(MarketplaceSource.ForDirectory(directory),
            TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _copilotPluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        AssertNotCalled(add: true, remove: true);
    }

    [Fact]
    public async Task InstallAsync_DirectorySourceAndMarketplaceMissing_AddsAbsolutePath()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "shade kit"));
        SetUpMarketplaces([]);
        SetUpPluginLists([], [], [Plugin]);

        // Act
        await _integration.InstallAsync(MarketplaceSource.ForDirectory(directory),
            TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(directory, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("github", "other/repo", "v0.8.0", null, "github", "other/repo#v0.8.0")]
    [InlineData("directory", null, null, OtherPath, "github", OtherPath)]
    [InlineData("git", null, null, null, "github", "a source of the kind 'git'")]
    [InlineData(null, null, null, null, "github", "a source of unknown kind")]
    [InlineData("github", Repo, "v0.8.0", null, "directory", "CreativeCodersTeam/shade-kit#v0.8.0")]
    [InlineData("directory", null, null, OtherPath, "directory", OtherPath)]
    [InlineData("url", null, null, null, "directory", "a source of the kind 'url'")]
    public async Task InstallAsync_MarketplaceWithOtherSource_ThrowsConflictWithoutChanges(string? sourceKind,
        string? repo, string? @ref, string? path, string targetKind, string expectedExistingSource)
    {
        // Arrange
        var otherPath = Path.Combine(Path.GetTempPath(), "other");
        var source = targetKind == "directory"
            ? MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "shade-kit"))
            : ReleaseSource;
        SetUpMarketplaces([new CopilotMarketplace(MarketplaceName, sourceKind, repo, @ref,
            path == OtherPath ? otherPath : path)]);
        SetUpPluginLists([Plugin]);

        // Act
        var act = () => _integration.InstallAsync(source, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationConflictException>()).Which;

        exception.AgentId
            .Should().Be("copilot");

        exception.MarketplaceName
            .Should().Be(MarketplaceName);

        exception.ExistingSource
            .Should().Be(expectedExistingSource == OtherPath ? otherPath : expectedExistingSource);

        exception.RequestedSource
            .Should().Be(source.Text);

        exception.ResolveCommands
            .Should().Equal(ResolveCommands);

        AssertNoChange();
    }

    [Fact]
    public async Task InstallAsync_MarketplaceNotFirstInList_UsesMarketplaceByName()
    {
        // Arrange
        SetUpMarketplaces([
            new CopilotMarketplace("other", "directory", null, null, "/x"),
            CreateGitHubMarketplace(Repo, "v0.8.0")
        ]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        await _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _copilotPluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        AssertNotCalled(add: true, remove: true);
    }

    [Fact]
    public async Task InstallAsync_PluginNotListedAfterInstall_ThrowsCopilotCommandFailedNamingListCommand()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([], [], []);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<CopilotCommandFailedException>()).Which;

        exception.CommandText
            .Should().Be("copilot plugin list --json");

        exception.ExitCode
            .Should().Be(0);

        exception.Reason
            .Should().Be("copilot does not list the plugin 'shade-kit' of the marketplace " +
                         "'creativecoders-shade-kit' after it was installed.");
    }

    [Fact]
    public async Task InstallAsync_PluginNotListedAfterUpdate_ThrowsCopilotCommandFailed()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin], []);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CopilotCommandFailedException>())
            .Which.Reason.Should().EndWith("after it was updated.");
    }

    [Fact]
    public async Task InstallAsync_MarketplaceRefreshFails_ThrowsCauseUnwrappedWithoutPluginChange()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "update", MarketplaceName);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.UpdateMarketplaceAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CopilotCommandFailedException>())
            .Which.Should().BeSameAs(failure);

        AssertNotCalled(install: true, update: true);
    }

    [Fact]
    public async Task InstallAsync_UpdateFailsWithKeptMarketplace_ThrowsCauseUnwrapped()
    {
        // Arrange
        var failure = CreateFailure("update", PluginId);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CopilotCommandFailedException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_AddFailsForMissingMarketplace_ThrowsCauseUnwrapped()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "add", ReleaseSource.Text);
        SetUpMarketplaces([]);
        SetUpPluginLists([]);
        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CopilotCommandFailedException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_UninstallFailsInSwitch_ThrowsCauseUnwrappedWithoutRemovingMarketplace()
    {
        // Arrange
        var failure = CreateFailure("uninstall", PluginId);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.UninstallPluginAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CopilotCommandFailedException>())
            .Which.Should().BeSameAs(failure, "nothing was removed, so nothing needs a repair");

        AssertNotCalled(remove: true, add: true, install: true);
    }

    [Fact]
    public async Task InstallAsync_MarketplaceRemoveFailsAsFirstRemoval_ThrowsCauseUnwrapped()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "remove", MarketplaceName);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([]);
        A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<CopilotCommandFailedException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_MarketplaceRemoveFailsAfterUninstall_ThrowsIntegrationIncompleteWithCause()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "remove", MarketplaceName);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationIncompleteException>()).Which;

        exception.AgentId
            .Should().Be("copilot");

        exception.InnerException
            .Should().BeSameAs(failure, "the plugin was already uninstalled");
    }

    [Fact]
    public async Task InstallAsync_AddFailsAfterRemove_ThrowsIntegrationIncompleteWithCause()
    {
        // Arrange
        var failure = CreateFailure("marketplace", "add", ReleaseSource.Text);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([]);
        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationIncompleteException>())
            .Which.InnerException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_InstallFailsAfterSwitch_ThrowsIntegrationIncomplete()
    {
        // Arrange
        var failure = CreateFailure("install", PluginId);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.InstallPluginAsync(A<string>._, A<CancellationToken>._)).Throws(failure);

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
        SetUpPluginLists([Plugin], []);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationIncompleteException>())
            .WithInnerException<CopilotCommandFailedException>();
    }

    [Fact]
    public async Task InstallAsync_CopilotNotFoundAfterUninstall_ThrowsIntegrationIncomplete()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Throws(new CopilotNotFoundException(["plugin", "marketplace", "remove", MarketplaceName], "Gone."));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationIncompleteException>())
            .WithInnerException<CopilotNotFoundException>();
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginIsUninstalled_ThrowsSwitchCanceledWithoutRemoval()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.UninstallPluginAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>(
            "the killed copilot may already have uninstalled the plugin");

        AssertNotCalled(remove: true, add: true);
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileMarketplaceIsRemovedWithoutInstalledPlugin_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([]);
        A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationSwitchCanceledException>();

        AssertNotCalled(add: true);
    }

    [Fact]
    public async Task InstallAsync_CanceledAfterRemove_ThrowsSwitchCanceledWithRunToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(canceled);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationSwitchCanceledException>()).Which;

        exception.AgentId
            .Should().Be("copilot");

        exception.InnerException
            .Should().BeSameAs(canceled);

        exception.CancellationToken
            .Should().Be(cancellationToken);
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginIsInstalledAfterSwitch_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.InstallPluginAsync(A<string>._, A<CancellationToken>._))
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
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Throws(canceled);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled, "only a cancellation of the run itself gets the repair hint");
    }

    [Fact]
    public async Task InstallAsync_CanceledWhileKeptMarketplaceIsRefreshed_PropagatesCancellationUnwrapped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.UpdateMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(canceled);

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled, "a kept marketplace is not removed, so nothing needs a repair");
    }

    [Fact]
    public async Task InstallAsync_CanceledWhilePluginIsUpdatedWithKeptMarketplace_PropagatesCancellationUnwrapped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._))
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
        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(canceled);

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
        A.CallTo(() => _copilotPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Throws(new OperationCanceledException(cancellationToken));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, cancellationToken);

        // Assert
        await act.Should().ThrowExactlyAsync<OperationCanceledException>();

        AssertNoChange();
    }

    [Fact]
    public async Task InstallAsync_CopilotNotFound_PropagatesCopilotNotFoundException()
    {
        // Arrange
        A.CallTo(() => _copilotPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .Throws(new CopilotNotFoundException(["plugin", "list", "--json"], "Not found."));

        // Act
        var act = () => _integration.InstallAsync(ReleaseSource, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<CopilotNotFoundException>();

        AssertNoChange();
    }

    [Fact]
    public async Task InstallAsync_SwitchWithInstalledPlugin_PassesTokenToEveryCommand()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        await _integration.InstallAsync(ReleaseSource, token);

        // Assert
        A.CallTo(() => _copilotPluginCli.ListPluginsAsync(token)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => _copilotPluginCli.ListMarketplacesAsync(token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _copilotPluginCli.UninstallPluginAsync(PluginId, token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(MarketplaceName, token))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(ReleaseSource.Text, token))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _copilotPluginCli.InstallPluginAsync(PluginId, token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_KeptMarketplace_PassesTokenToRefreshAndUpdate()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        await _integration.InstallAsync(ReleaseSource, token);

        // Assert
        A.CallTo(() => _copilotPluginCli.UpdateMarketplaceAsync(MarketplaceName, token))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _copilotPluginCli.UpdatePluginAsync(PluginId, token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdateAsync_PluginNotInstalled_ReturnsNullWithoutResolvingSourceOrChanges()
    {
        // Arrange
        SetUpPluginLists([]);
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

        A.CallTo(() => _copilotPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustNotHaveHappened();

        AssertNoChange();
    }

    [Theory]
    [InlineData("other-marketplace")]
    [InlineData(null)]
    public async Task UpdateAsync_PluginOnlyFromOtherMarketplaceOrDirectInstall_ReturnsNullWithoutChanges(
        string? marketplace)
    {
        // Arrange
        SetUpPluginLists([Plugin with { Marketplace = marketplace }]);

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeNull();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_PluginInstalled_ResolvesSourceOnceWithTokenBeforeMarketplaceIsRead()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin], [Plugin]);
        var getSource = A.Fake<Func<CancellationToken, Task<MarketplaceSource>>>();
        A.CallTo(() => getSource(A<CancellationToken>._)).Returns(ReleaseSource);

        // Act
        await _integration.UpdateAsync(getSource, token);

        // Assert
        A.CallTo(() => getSource(token))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _copilotPluginCli.ListMarketplacesAsync(token)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task UpdateAsync_KeptMarketplace_RefreshesUpdatesAndReturnsUpdated()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Updated, "0.8.0", "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _copilotPluginCli.UpdateMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _copilotPluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    [Trait("Requirement", AcInstall)]
    public async Task UpdateAsync_PluginInstalledWithOtherRef_SwitchesMarketplaceAndReinstalls()
    {
        // Arrange
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin], [Plugin]);

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Updated, "0.7.0", "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _copilotPluginCli.UninstallPluginAsync(PluginId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(MarketplaceName, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(ReleaseSource.Text, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _copilotPluginCli.InstallPluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task UpdateAsync_CanceledDuringMarketplaceSwitch_ThrowsSwitchCanceled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.7.0")]);
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
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

        A.CallTo(() => _copilotPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .MustNotHaveHappened();

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_GetSourceCanceled_PropagatesCancellationUnwrappedWithoutChanges()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        SetUpPluginLists([Plugin]);

        // Act
        var act = () => _integration.UpdateAsync(_ =>
        {
            cancellationTokenSource.Cancel();

            return Task.FromException<MarketplaceSource>(canceled);
        }, cancellationToken);

        // Assert
        (await act.Should().ThrowExactlyAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled);

        AssertNoChange();
    }

    [Fact]
    public async Task UpdateAsync_PluginInstalledButMarketplaceMissing_AddsMarketplaceAndUpdates()
    {
        // Arrange
        SetUpMarketplaces([]);
        SetUpPluginLists([Plugin], [Plugin], [Plugin]);

        // Act
        var result = await _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new IntegrationResult("copilot", PluginId, IntegrationOutcome.Updated, null, "0.8.0",
                "v0.8.0", true));

        A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(ReleaseSource.Text, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _copilotPluginCli.UpdatePluginAsync(PluginId, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
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
        SetUpMarketplaces([CreateGitHubMarketplace("other/repo", "v0.8.0")]);
        SetUpPluginLists([Plugin]);

        // Act
        var act = () => _integration.UpdateAsync(GetReleaseSourceAsync, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationConflictException>())
            .Which.ResolveCommands.Should().Equal(ResolveCommands);

        AssertNoChange();
    }

    [Fact]
    public async Task GetInstalledAsync_PluginNotInstalled_ReturnsNullWithoutListingMarketplaces()
    {
        // Arrange
        SetUpPluginLists([Plugin with { Marketplace = "other" }]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().BeNull();

        A.CallTo(() => _copilotPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
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
        SetUpPluginLists([Plugin with { Enabled = enabled }]);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, @ref)]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().Be(new InstalledIntegration("copilot", PluginId, expectedKitVersion, @ref, enabled));
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("git")]
    public async Task GetInstalledAsync_MarketplaceOfOtherKindThanGitHub_ReturnsNullRefAndKitVersion(
        string sourceKind)
    {
        // Arrange
        SetUpPluginLists([Plugin]);
        SetUpMarketplaces([new CopilotMarketplace(MarketplaceName, sourceKind, null, "v0.8.0", "/src/shade-kit")]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().Be(new InstalledIntegration("copilot", PluginId, null, null, true));
    }

    [Fact]
    public async Task GetInstalledAsync_MarketplaceMissing_ReturnsNullRefAndKitVersion()
    {
        // Arrange
        SetUpPluginLists([Plugin]);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0") with { Name = "other" }]);

        // Act
        var installed = await _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        installed
            .Should().Be(new InstalledIntegration("copilot", PluginId, null, null, true));
    }

    [Fact]
    public async Task GetInstalledAsync_Token_PassesTokenToBothListCommands()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;
        SetUpPluginLists([Plugin]);
        SetUpMarketplaces([CreateGitHubMarketplace(Repo, "v0.8.0")]);

        // Act
        await _integration.GetInstalledAsync(token);

        // Assert
        A.CallTo(() => _copilotPluginCli.ListPluginsAsync(token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _copilotPluginCli.ListMarketplacesAsync(token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetInstalledAsync_MarketplaceListFails_PropagatesFailure()
    {
        // Arrange
        SetUpPluginLists([Plugin]);
        A.CallTo(() => _copilotPluginCli.ListMarketplacesAsync(A<CancellationToken>._))
            .Throws(CreateFailure("marketplace", "list", "--json"));

        // Act
        var act = () => _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<CopilotCommandFailedException>();
    }

    [Fact]
    public async Task GetInstalledAsync_ListFails_PropagatesFailure()
    {
        // Arrange
        A.CallTo(() => _copilotPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .Throws(CreateFailure("list", "--json"));

        // Act
        var act = () => _integration.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<CopilotCommandFailedException>();
    }

    private static Task<MarketplaceSource> GetReleaseSourceAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ReleaseSource);
    }

    private static CopilotMarketplace CreateGitHubMarketplace(string repo, string? @ref)
    {
        return new CopilotMarketplace(MarketplaceName, "github", repo, @ref, null);
    }

    private static CopilotCommandFailedException CreateFailure(params string[] command)
    {
        return new CopilotCommandFailedException(["plugin", .. command], 1, "copilot exited with the exit code 1.");
    }

    private void SetUpMarketplaces(IReadOnlyList<CopilotMarketplace> marketplaces)
    {
        A.CallTo(() => _copilotPluginCli.ListMarketplacesAsync(A<CancellationToken>._)).Returns(marketplaces);
    }

    // Each call of ListPluginsAsync returns the next list.
    private void SetUpPluginLists(IReadOnlyList<CopilotInstalledPlugin> first,
        IReadOnlyList<CopilotInstalledPlugin>? second = null, IReadOnlyList<CopilotInstalledPlugin>? third = null)
    {
        IReadOnlyList<CopilotInstalledPlugin>[] pluginLists =
            [.. new[] { first, second, third }.OfType<IReadOnlyList<CopilotInstalledPlugin>>()];

        A.CallTo(() => _copilotPluginCli.ListPluginsAsync(A<CancellationToken>._))
            .ReturnsNextFromSequence(pluginLists);
    }

    private void AssertNotCalled(bool add = false, bool remove = false, bool updateMarketplace = false,
        bool install = false, bool update = false, bool uninstall = false)
    {
        if (add)
        {
            A.CallTo(() => _copilotPluginCli.AddMarketplaceAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        if (remove)
        {
            A.CallTo(() => _copilotPluginCli.RemoveMarketplaceAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        if (updateMarketplace)
        {
            A.CallTo(() => _copilotPluginCli.UpdateMarketplaceAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        if (install)
        {
            A.CallTo(() => _copilotPluginCli.InstallPluginAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        if (update)
        {
            A.CallTo(() => _copilotPluginCli.UpdatePluginAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        if (uninstall)
        {
            A.CallTo(() => _copilotPluginCli.UninstallPluginAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }
    }

    private void AssertNoChange()
    {
        AssertNotCalled(add: true, remove: true, updateMarketplace: true, install: true, update: true,
            uninstall: true);
    }
}
