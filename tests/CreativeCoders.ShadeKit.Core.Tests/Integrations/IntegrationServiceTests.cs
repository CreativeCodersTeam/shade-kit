using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Integrations.Warnings;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations;

// The agents and the release tag checker are faked and the environment variables come from a dictionary, so these
// tests neither start an agent CLI or git nor read the real environment.
public class IntegrationServiceTests
{
    private static readonly string CurrentDirectory = Path.Combine(Path.GetTempPath(), "shade-kit-tests", "cwd");

    private static readonly MarketplaceSource ReleaseSource =
        MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit", "v0.8.0");

    private static readonly MarketplaceSource MainSource = MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit",
        "main");

    private readonly IEngineVersionProvider _engineVersionProvider = A.Fake<IEngineVersionProvider>();

    private readonly IGitRemoteTagChecker _gitRemoteTagChecker = A.Fake<IGitRemoteTagChecker>();

    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);

    // The source that each agent received in UpdateAsync, in the order of the calls.
    private readonly List<(string AgentId, MarketplaceSource Source)> _updateSources = [];

    private readonly IAgentIntegration _claude;

    private readonly IAgentIntegration _other;

    public IntegrationServiceTests()
    {
        _claude = CreateAgent("claude", isAvailable: true);
        _other = CreateAgent("other", isAvailable: false);

        A.CallTo(() => _engineVersionProvider.Version).Returns("0.8.0");
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(GitRemoteTagState.Found);
    }

    [Fact]
    public void Constructor_NullAgentIntegrations_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new IntegrationService(null!, _engineVersionProvider, _gitRemoteTagChecker);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agentIntegrations");
    }

    [Fact]
    public void Constructor_NullEngineVersionProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new IntegrationService([_claude], null!, _gitRemoteTagChecker);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("engineVersionProvider");
    }

    [Fact]
    public void Constructor_NullGitRemoteTagChecker_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new IntegrationService([_claude], _engineVersionProvider, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitRemoteTagChecker");
    }

    [Fact]
    public void Constructor_DuplicateAgentIds_ThrowsArgumentException()
    {
        // Act
        var act = () => new IntegrationService([_claude, CreateAgent("CLAUDE", isAvailable: true)],
            _engineVersionProvider, _gitRemoteTagChecker);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("agentIntegrations");
    }

    [Fact]
    public void AgentIds_RegisteredAgents_ReturnsIdsInRegistrationOrder()
    {
        // Arrange
        var service = CreateService();

        // Act
        var agentIds = service.AgentIds;

        // Assert
        agentIds
            .Should().Equal("claude", "other");
    }

    [Theory]
    [InlineData("0.8.0", "CreativeCodersTeam/shade-kit#v0.8.0")]
    [InlineData("1.12.3", "CreativeCodersTeam/shade-kit#v1.12.3")]
    [InlineData("0.8.0+build.5", "CreativeCodersTeam/shade-kit#v0.8.0")]
    public async Task ResolveSourceAsync_ReleaseEngine_ReturnsReleaseTagWithoutWarnings(string engineVersion,
        string expectedSource)
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns(engineVersion);
        var service = CreateService();

        // Act
        var (source, warnings) = await service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        source.Text
            .Should().Be(expectedSource);

        warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveSourceAsync_PrereleaseEngine_ReturnsMainWithPrereleaseRefWarning()
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns("0.8.0-beta.3");
        var service = CreateService();

        // Act
        var (source, warnings) = await service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        source
            .Should().Be(MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit", "main"));

        warnings
            .Should().Equal(new IntegrationPrereleaseRefWarning("0.8.0-beta.3", "main"));
    }

    [Fact]
    public async Task ResolveSourceAsync_InvalidEngineVersion_ThrowsInvalidOperationException()
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns("1.0");
        var service = CreateService();

        // Act
        var act = () => service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*'1.0'*");
    }

    [Theory]
    [InlineData("owner/fork#feature/x", "owner/fork", "feature/x")]
    [InlineData("owner/fork", "owner/fork", null)]
    public async Task ResolveSourceAsync_GitHubOverride_ReturnsOverrideWithoutWarnings(string value, string expectedRepo,
        string? expectedRef)
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns("0.8.0-beta.1");
        _environment[IntegrationService.MarketplaceSourceVariableName] = value;
        var service = CreateService();

        // Act
        var (source, warnings) = await service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        source
            .Should().Be(MarketplaceSource.ForGitHub(expectedRepo, expectedRef));

        warnings
            .Should().BeEmpty("the override replaces the whole source, also the prerelease branch");
    }

    [Fact]
    public async Task ResolveSourceAsync_RelativePathOverride_ReturnsPathResolvedAgainstCurrentDirectory()
    {
        // Arrange
        _environment[IntegrationService.MarketplaceSourceVariableName] = "./shade-kit";
        var service = CreateService();

        // Act
        var (source, _) = await service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        source.Path
            .Should().Be(Path.Combine(CurrentDirectory, "shade-kit"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("--help")]
    [InlineData("owner/repo#")]
    public async Task ResolveSourceAsync_InvalidOverride_ThrowsInvalidMarketplaceSourceException(string value)
    {
        // Arrange
        _environment[IntegrationService.MarketplaceSourceVariableName] = value;
        var service = CreateService();

        // Act
        var act = () => service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidMarketplaceSourceException>())
            .Which.Value.Should().Be(value);
    }

    [Fact]
    public async Task ResolveSourceAsync_OverrideAndInvalidEngineVersion_ReturnsOverride()
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns("not a version");
        _environment[IntegrationService.MarketplaceSourceVariableName] = "owner/fork#main";
        var service = CreateService();

        // Act
        var (source, _) = await service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        source
            .Should().Be(MarketplaceSource.ForGitHub("owner/fork", "main"));
    }

    [Fact]
    public async Task ResolveSourceAsync_EmptyEngineVersion_ThrowsInvalidOperationException()
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns(string.Empty);
        var service = CreateService();

        // Act
        var act = () => service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResolveSourceAsync_AbsolutePathOverride_ReturnsDirectorySource()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");
        _environment[IntegrationService.MarketplaceSourceVariableName] = path;
        var service = CreateService();

        // Act
        var (source, _) = await service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        source
            .Should().Be(MarketplaceSource.ForDirectory(path));
    }

    [Fact]
    public async Task ResolveSourceAsync_ReleaseEngine_ChecksReleaseTagInMarketplaceRepository()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var service = CreateService();

        // Act
        await service.ResolveSourceAsync(cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync("https://github.com/CreativeCodersTeam/shade-kit.git",
                "v0.8.0", cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ResolveSourceAsync_ReleaseTagMissing_ReturnsMainWithReleaseTagMissingWarning()
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns("0.8.1+build.7");
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, "v0.8.1", A<CancellationToken>._))
            .Returns(GitRemoteTagState.Missing);
        var service = CreateService();

        // Act
        var (source, warnings) = await service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        source
            .Should().Be(MainSource);

        warnings
            .Should().Equal(new IntegrationReleaseTagMissingWarning("0.8.1+build.7", "v0.8.1", "main"));
    }

    [Fact]
    public async Task ResolveSourceAsync_ReleaseTagCannotBeChecked_ReturnsReleaseTagUncheckedWithoutWarnings()
    {
        // Arrange
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(GitRemoteTagState.Unknown);
        var service = CreateService();

        // Act
        var (source, warnings) = await service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        source
            .Should().Be(ReleaseSource);

        warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveSourceAsync_TagCheckCanceled_PropagatesCancellation()
    {
        // Arrange
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());
        var service = CreateService();

        // Act
        var act = () => service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ResolveSourceAsync_PrereleaseEngine_DoesNotCheckAnyTag()
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns("0.8.0-beta.3");
        var service = CreateService();

        // Act
        await service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("owner/fork#v0.8.0")]
    [InlineData("CreativeCodersTeam/shade-kit#v0.8.0")]
    [InlineData("./shade-kit")]
    public async Task ResolveSourceAsync_OverrideOfReleaseEngine_DoesNotCheckAnyTag(string value)
    {
        // Arrange
        _environment[IntegrationService.MarketplaceSourceVariableName] = value;
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(GitRemoteTagState.Missing);
        var service = CreateService();

        // Act
        var (_, warnings) = await service.ResolveSourceAsync(TestContext.Current.CancellationToken);

        // Assert
        warnings
            .Should().BeEmpty();

        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_ReleaseTagMissing_InstallsFromMainWithReleaseTagMissingWarning()
    {
        // Arrange
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(GitRemoteTagState.Missing);
        var service = CreateService();

        // Act
        var result = await service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.Source
            .Should().Be(MainSource);

        result.Warnings
            .Should().Equal(new IntegrationReleaseTagMissingWarning("0.8.0", "v0.8.0", "main"));

        A.CallTo(() => _claude.InstallAsync(MainSource, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_ExplicitAgentAndReleaseTagMissing_InstallsFromMain()
    {
        // Arrange
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(GitRemoteTagState.Missing);
        var service = CreateService();

        // Act
        var result = await service.InstallAsync(["other"], TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new IntegrationReleaseTagMissingWarning("0.8.0", "v0.8.0", "main"));

        A.CallTo(() => _other.InstallAsync(MainSource, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_SecondAgentFails_ThrowsRunFailedWithResultOfFirstAgent()
    {
        // Arrange
        var failure = new ClaudeCommandFailedException(["plugin", "install"], 1, "failed.");
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._)).ThrowsAsync(failure);
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationRunFailedException>()).Which;

        exception.FailedAgentId
            .Should().Be("other");

        exception.CompletedIntegrations
            .Should().Equal(CreateResult("claude"));

        exception.MarketplaceSource
            .Should().Be(ReleaseSource);

        exception.InnerException
            .Should().BeSameAs(failure);
    }

    [Theory]
    [MemberData(nameof(AgentFailures))]
    public async Task InstallAsync_SecondAgentFailsWithAgentFailure_KeepsFailureAsInnerException(
        Exception failure)
    {
        // Arrange
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._)).ThrowsAsync(failure);
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationRunFailedException>())
            .Which.InnerException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_SecondAgentFailsWithReleaseTagMissing_KeepsFallbackSource()
    {
        // Arrange
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(GitRemoteTagState.Missing);
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .ThrowsAsync(new ClaudeCommandFailedException(["plugin", "install"], 1, "failed."));
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationRunFailedException>())
            .Which.MarketplaceSource.Should().Be(MainSource);
    }

    [Fact]
    public async Task InstallAsync_SecondAgentSwitchCanceled_PropagatesSameInstance()
    {
        // Arrange
        var canceled = new IntegrationSwitchCanceledException("other", new OperationCanceledException());
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._)).ThrowsAsync(canceled);
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationSwitchCanceledException>())
            .Which.Should().BeSameAs(canceled);
    }

    [Fact]
    public async Task InstallAsync_SecondDetectedAgentMissing_ChecksNoTagAndInstallsForNoAgent()
    {
        // Arrange
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.EnsureAvailable())
            .Throws(new ClaudeNotFoundException(["plugin", "list", "--json"], "Not on the PATH."));
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ClaudeNotFoundException>();

        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _claude.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_TagCheckCanceled_InstallsForNoAgent()
    {
        // Arrange
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(() => _claude.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_ThirdAgentFails_KeepsResultsOfFirstTwoAgentsInOrder()
    {
        // Arrange
        var third = CreateAgent("third", isAvailable: true);
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => third.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .ThrowsAsync(new ClaudeCommandFailedException(["plugin", "install"], 1, "failed."));
        var service = new IntegrationService([_claude, _other, third], _engineVersionProvider, _gitRemoteTagChecker,
            name => _environment.GetValueOrDefault(name), () => CurrentDirectory);

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationRunFailedException>()).Which;

        exception.FailedAgentId
            .Should().Be("third");

        exception.CompletedIntegrations.Select(x => x.AgentId)
            .Should().Equal("claude", "other");
    }

    [Fact]
    public async Task InstallAsync_SecondAgentCanceled_PropagatesCancellationUnwrapped()
    {
        // Arrange
        var canceled = new OperationCanceledException();
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._)).ThrowsAsync(canceled);
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled);
    }

    [Fact]
    public async Task InstallAsync_SecondAgentThrowsUnexpectedException_PropagatesUnwrapped()
    {
        // Arrange
        var failure = new InvalidOperationException("Defect.");
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._)).ThrowsAsync(failure);
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task InstallAsync_ExplicitAgentMissing_ThrowsItsNotFoundErrorBeforeReleaseTagCheck()
    {
        // Arrange
        var notFound = new ClaudeNotFoundException(["plugin", "list", "--json"], "Not on the PATH.");
        A.CallTo(() => _claude.EnsureAvailable()).Throws(notFound);
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(["claude"], TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeNotFoundException>())
            .Which.Should().BeSameAs(notFound);

        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _claude.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_SecondExplicitAgentMissing_InstallsForNoAgent()
    {
        // Arrange
        A.CallTo(() => _other.EnsureAvailable())
            .Throws(new ClaudeNotFoundException(["plugin", "list", "--json"], "Not on the PATH."));
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(["claude", "other"], TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ClaudeNotFoundException>();

        A.CallTo(() => _claude.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_AvailableAgents_EnsuresAvailabilityBeforeTagCheckAndInstall()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _claude.EnsureAvailable())
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._,
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _claude.InstallAsync(ReleaseSource, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task InstallAsync_NoAgentAvailable_DoesNotCheckReleaseTag()
    {
        // Arrange
        A.CallTo(() => _claude.IsAvailable()).Returns(false);
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NoAgentFoundException>();

        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_SeveralAvailableAgents_InstallsAllInRegistrationOrder()
    {
        // Arrange
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        var service = CreateService();

        // Act
        var result = await service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.Integrations.Select(x => x.AgentId)
            .Should().Equal("claude", "other");
    }

    [Fact]
    public async Task InstallAsync_ExplicitAgentIdsInOtherOrder_InstallsInRequestedOrder()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.InstallAsync(["other", "claude"], TestContext.Current.CancellationToken);

        // Assert
        result.Integrations.Select(x => x.AgentId)
            .Should().Equal("other", "claude");
    }

    [Fact]
    public async Task InstallAsync_FirstAgentFails_PropagatesFailureWithoutAskingLaterAgents()
    {
        // Arrange
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _claude.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .ThrowsAsync(new IntegrationConflictException("claude", "m", "a", "b", "c"));
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IntegrationConflictException>();

        A.CallTo(() => _other.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_UnknownAgentIdAndInvalidOverride_ThrowsArgumentException()
    {
        // Arrange
        _environment[IntegrationService.MarketplaceSourceVariableName] = "-x";
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(["nope"], TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("agentIds");
    }

    [Fact]
    public async Task InstallAsync_NoAgentsRegistered_ThrowsNoAgentFoundExceptionWithEmptyList()
    {
        // Arrange
        var service = new IntegrationService([], _engineVersionProvider, _gitRemoteTagChecker, _ => null,
            () => CurrentDirectory);

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<NoAgentFoundException>())
            .Which.SupportedAgentIds.Should().BeEmpty();
    }

    [Fact]
    public async Task InstallAsync_NoAgentIds_InstallsForEveryAvailableAgent()
    {
        // Arrange
        var expected = CreateResult("claude");
        A.CallTo(() => _claude.InstallAsync(ReleaseSource, A<CancellationToken>._)).Returns(expected);
        var service = CreateService();

        // Act
        var result = await service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new IntegrationRunResult(ReleaseSource, [expected], []));

        A.CallTo(() => _other.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_EmptyAgentIds_InstallsForEveryAvailableAgent()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.InstallAsync([], TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _claude.InstallAsync(ReleaseSource, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InstallAsync_PrereleaseEngine_ReturnsPrereleaseRefWarning()
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns("0.9.0-alpha.1");
        var service = CreateService();

        // Act
        var result = await service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.Source
            .Should().Be(MainSource);

        result.Warnings
            .Should().Equal(new IntegrationPrereleaseRefWarning("0.9.0-alpha.1", "main"));

        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_NoAgentAvailable_ThrowsNoAgentFoundException()
    {
        // Arrange
        A.CallTo(() => _claude.IsAvailable()).Returns(false);
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<NoAgentFoundException>())
            .Which.SupportedAgentIds.Should().Equal("claude", "other");
    }

    [Fact]
    public async Task InstallAsync_ExplicitAgentIdInOtherCase_InstallsOnlyThatAgentWithoutDetection()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.InstallAsync(["OTHER", "other"], TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _other.InstallAsync(ReleaseSource, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

        A.CallTo(() => _claude.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _other.IsAvailable())
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_ExplicitAgentNotFound_PropagatesAgentFailure()
    {
        // Arrange
        A.CallTo(() => _claude.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .ThrowsAsync(new ClaudeNotFoundException(["plugin", "list", "--json"], "Not on the PATH."));
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(["claude"], TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ClaudeNotFoundException>();
    }

    [Fact]
    public async Task InstallAsync_UnknownAgentId_ThrowsArgumentExceptionWithoutAgentCalls()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(["claude", "nope"], TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName("agentIds")
            .WithMessage("*'nope'*claude, other*");

        A.CallTo(() => _claude.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task InstallAsync_InvalidOverride_ThrowsBeforeAnyAgentIsAsked()
    {
        // Arrange
        _environment[IntegrationService.MarketplaceSourceVariableName] = "-x";
        A.CallTo(() => _claude.IsAvailable()).Returns(false);
        var service = CreateService();

        // Act
        var act = () => service.InstallAsync(null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidMarketplaceSourceException>();
    }

    [Fact]
    public async Task InstallAsync_CancellationToken_IsPassedToAgent()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var service = CreateService();

        // Act
        await service.InstallAsync(null, cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => _claude.InstallAsync(ReleaseSource, cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdateAsync_InstalledAgent_ReturnsItsResult()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new IntegrationRunResult(ReleaseSource, [CreateResult("claude")], []));

        _updateSources
            .Should().Equal(("claude", ReleaseSource));

        A.CallTo(() => _other.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateAsync_InstalledAgents_DoesNotReadInstalledIntegrationsSeparately()
    {
        // Arrange
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        var service = CreateService();

        // Act
        await service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _claude.GetInstalledAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _other.GetInstalledAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateAsync_NothingInstalled_ReturnsEmptyResultWithoutSourceAndWarnings()
    {
        // Arrange
        SetUpNotInstalled(_claude);
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new IntegrationRunResult(null, [], []));

        _updateSources
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("0.8.0")]
    [InlineData("0.9.0-alpha.1")]
    public async Task UpdateAsync_NothingInstalled_DoesNotCheckReleaseTag(string engineVersion)
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns(engineVersion);
        SetUpNotInstalled(_claude);
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().BeEmpty();

        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateAsync_NothingInstalledAndInvalidOverride_ReturnsEmptyResult()
    {
        // Arrange
        _environment[IntegrationService.MarketplaceSourceVariableName] = "-x";
        SetUpNotInstalled(_claude);
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Integrations
            .Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_InstalledAndReleaseTagMissing_UpdatesFromMainWithReleaseTagMissingWarning()
    {
        // Arrange
        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(GitRemoteTagState.Missing);
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new IntegrationRunResult(MainSource, [CreateResult("claude")],
                [new IntegrationReleaseTagMissingWarning("0.8.0", "v0.8.0", "main")]));

        _updateSources
            .Should().Equal(("claude", MainSource));
    }

    [Fact]
    public async Task UpdateAsync_AgentResolvesSourceButReturnsNoResult_ReturnsSourceWithEmptyList()
    {
        // Arrange
        A.CallTo(() => _claude.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ReturnsLazily(async (Func<CancellationToken, Task<MarketplaceSource>> getSource,
                CancellationToken cancellationToken) =>
            {
                await getSource(cancellationToken);

                return (IntegrationResult?)null;
            });
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Integrations
            .Should().BeEmpty();

        result.Source
            .Should().Be(ReleaseSource);
    }

    [Fact]
    public async Task UpdateAsync_SeveralInstalledAgents_UpdatesAllFromSourceResolvedOnceInRegistrationOrder()
    {
        // Arrange
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Integrations.Select(x => x.AgentId)
            .Should().Equal("claude", "other");

        _updateSources
            .Should().Equal(("claude", ReleaseSource), ("other", ReleaseSource));

        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdateAsync_FirstAgentNotInstalled_ResolvesSourceForSecondAgent()
    {
        // Arrange
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        SetUpNotInstalled(_claude);
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new IntegrationRunResult(ReleaseSource, [CreateResult("other")], []));
    }

    [Fact]
    public async Task UpdateAsync_SecondAgentUpdateFails_ThrowsRunFailedWithResultOfFirstAgent()
    {
        // Arrange
        var failure = new ClaudeCommandFailedException(["plugin", "update"], 1, "failed.");
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ThrowsAsync(failure);
        var service = CreateService();

        // Act
        var act = () => service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationRunFailedException>()).Which;

        exception.FailedAgentId
            .Should().Be("other");

        exception.CompletedIntegrations
            .Should().Equal(CreateResult("claude"));

        exception.MarketplaceSource
            .Should().Be(ReleaseSource);

        exception.InnerException
            .Should().BeSameAs(failure);
    }

    [Theory]
    [MemberData(nameof(AgentFailures))]
    public async Task UpdateAsync_SecondAgentFailsWithAgentFailure_ThrowsRunFailedKeepingFailure(Exception failure)
    {
        // Arrange
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ThrowsAsync(failure);
        var service = CreateService();

        // Act
        var act = () => service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IntegrationRunFailedException>())
            .Which.InnerException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task UpdateAsync_FirstAgentReturnsNoResultSecondFails_PropagatesFailureUnwrapped()
    {
        // Arrange
        var failure = new ClaudeCommandFailedException(["plugin", "update"], 1, "failed.");
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _claude.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ReturnsLazily(async (Func<CancellationToken, Task<MarketplaceSource>> getSource,
                CancellationToken cancellationToken) =>
            {
                await getSource(cancellationToken);

                return (IntegrationResult?)null;
            });
        A.CallTo(() => _other.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ThrowsAsync(failure);
        var service = CreateService();

        // Act
        var act = () => service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task UpdateAsync_FirstAgentNotInstalledSecondFails_PropagatesFailureUnwrapped()
    {
        // Arrange
        var failure = new ClaudeCommandFailedException(["plugin", "update"], 1, "failed.");
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        SetUpNotInstalled(_claude);
        A.CallTo(() => _other.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ThrowsAsync(failure);
        var service = CreateService();

        // Act
        var act = () => service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ClaudeCommandFailedException>())
            .Which.Should().BeSameAs(failure, "no other agent was done, so there is nothing to keep");
    }

    [Fact]
    public async Task UpdateAsync_SecondAgentCanceled_PropagatesCancellationUnwrapped()
    {
        // Arrange
        var canceled = new IntegrationSwitchCanceledException("other", new OperationCanceledException());
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ThrowsAsync(canceled);
        var service = CreateService();

        // Act
        var act = () => service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled);
    }

    [Fact]
    public async Task UpdateAsync_SecondAgentThrowsUnexpectedException_PropagatesUnwrapped()
    {
        // Arrange
        var failure = new IOException("Disk");
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _other.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ThrowsAsync(failure);
        var service = CreateService();

        // Act
        var act = () => service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .Which.Should().BeSameAs(failure, "a defect is not an agent failure");
    }

    [Fact]
    public async Task UpdateAsync_FirstAgentCannotBeStarted_PropagatesFailureWithoutAskingLaterAgents()
    {
        // Arrange
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _claude.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ThrowsAsync(new ClaudeUnavailableException(["plugin", "list", "--json"], "Cannot start."));
        var service = CreateService();

        // Act
        var act = () => service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ClaudeUnavailableException>("update reports an agent failure as an error, " +
                                                                  "unlike the read for 'version'");

        A.CallTo(() => _other.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateAsync_CancellationToken_IsPassedToAgentAndTagChecker()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var service = CreateService();

        // Act
        await service.UpdateAsync(cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => _claude.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _gitRemoteTagChecker.CheckTagAsync(A<string>._, A<string>._, cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdateAsync_AgentFails_PropagatesFailure()
    {
        // Arrange
        A.CallTo(() => _claude.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ThrowsAsync(new ClaudeCommandFailedException(["plugin", "update"], 1, "claude failed."));
        var service = CreateService();

        // Act
        var act = () => service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ClaudeCommandFailedException>();
    }

    [Fact]
    public async Task UpdateAsync_InstalledAndInvalidOverride_ThrowsFromSourceResolution()
    {
        // Arrange
        _environment[IntegrationService.MarketplaceSourceVariableName] = "";
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        var service = CreateService();

        // Act
        var act = () => service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidMarketplaceSourceException>();

        _updateSources
            .Should().BeEmpty("the first installed agent resolves the source before it changes anything");

        A.CallTo(() => _other.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateAsync_NoAgentAvailable_ThrowsNoAgentFoundException()
    {
        // Arrange
        A.CallTo(() => _claude.IsAvailable()).Returns(false);
        var service = CreateService();

        // Act
        var act = () => service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NoAgentFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_PrereleaseEngine_ReturnsPrereleaseRefWarning()
    {
        // Arrange
        A.CallTo(() => _engineVersionProvider.Version).Returns("0.9.0-alpha.1");
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new IntegrationPrereleaseRefWarning("0.9.0-alpha.1", "main"));
    }

    [Fact]
    public async Task GetInstalledAsync_InstalledAndNotInstalledAgents_ReturnsInstalledOnly()
    {
        // Arrange
        var installed = new InstalledIntegration("claude", "shade-kit@creativecoders-shade-kit", "0.8.0", "v0.8.0",
            true);
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _claude.GetInstalledAsync(A<CancellationToken>._)).Returns(installed);
        A.CallTo(() => _other.GetInstalledAsync(A<CancellationToken>._)).Returns((InstalledIntegration?)null);
        var service = CreateService();

        // Act
        var result = await service.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Integrations
            .Should().Equal(installed);

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task GetInstalledAsync_AgentNotAvailable_DoesNotAskItAndReturnsNoWarning()
    {
        // Arrange
        A.CallTo(() => _claude.IsAvailable()).Returns(false);
        var service = CreateService();

        // Act
        var result = await service.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new InstalledIntegrationsResult([], []));

        A.CallTo(() => _claude.GetInstalledAsync(A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _other.GetInstalledAsync(A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetInstalledAsync_AvailableAgentNotFoundWhenCalled_ReturnsAgentUnavailableWarning()
    {
        // Arrange
        var failure = new ClaudeNotFoundException(["plugin", "list", "--json"], "Not on the PATH.");
        A.CallTo(() => _claude.GetInstalledAsync(A<CancellationToken>._)).ThrowsAsync(failure);
        A.CallTo(() => _other.GetInstalledAsync(A<CancellationToken>._)).Returns((InstalledIntegration?)null);
        var service = CreateService();

        // Act
        var result = await service.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Integrations
            .Should().BeEmpty();

        result.Warnings
            .Should().Equal(new AgentUnavailableWarning("claude", failure.Message));
    }

    [Fact]
    public async Task GetInstalledAsync_AgentCommandFails_ReturnsWarningAndOtherAgents()
    {
        // Arrange
        var failure = new ClaudeCommandFailedException(["plugin", "list", "--json"], 1, "claude failed.");
        var installed = new InstalledIntegration("other", "kit", null, null, true);
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _claude.GetInstalledAsync(A<CancellationToken>._)).ThrowsAsync(failure);
        A.CallTo(() => _other.GetInstalledAsync(A<CancellationToken>._)).Returns(installed);
        var service = CreateService();

        // Act
        var result = await service.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Integrations
            .Should().Equal(installed);

        result.Warnings
            .Should().Equal(new AgentUnavailableWarning("claude", failure.Message));
    }

    [Fact]
    public async Task GetInstalledAsync_UnexpectedFailure_PropagatesFailure()
    {
        // Arrange
        A.CallTo(() => _claude.GetInstalledAsync(A<CancellationToken>._)).ThrowsAsync(new IOException("Disk"));
        var service = CreateService();

        // Act
        var act = () => service.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task GetInstalledAsync_AllAgentsFail_ReturnsOneWarningPerAgentInOrder()
    {
        // Arrange
        A.CallTo(() => _other.IsAvailable()).Returns(true);
        A.CallTo(() => _claude.GetInstalledAsync(A<CancellationToken>._))
            .ThrowsAsync(new ClaudeNotFoundException(["plugin", "list", "--json"], "Not on the PATH."));
        A.CallTo(() => _other.GetInstalledAsync(A<CancellationToken>._))
            .ThrowsAsync(new ClaudeUnavailableException(["plugin", "list", "--json"], "Cannot start."));
        var service = CreateService();

        // Act
        var result = await service.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Warnings.OfType<AgentUnavailableWarning>().Select(x => x.AgentId)
            .Should().Equal("claude", "other");
    }

    [Fact]
    public async Task GetInstalledAsync_Canceled_PropagatesCancellation()
    {
        // Arrange
        A.CallTo(() => _claude.GetInstalledAsync(A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());
        var service = CreateService();

        // Act
        var act = () => service.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetInstalledAsync_CancellationToken_IsPassedToAgent()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var service = CreateService();

        // Act
        await service.GetInstalledAsync(cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => _claude.GetInstalledAsync(cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetInstalledAsync_InvalidOverride_IgnoresOverride()
    {
        // Arrange
        _environment[IntegrationService.MarketplaceSourceVariableName] = "-x";
        A.CallTo(() => _claude.GetInstalledAsync(A<CancellationToken>._)).Returns((InstalledIntegration?)null);
        A.CallTo(() => _other.GetInstalledAsync(A<CancellationToken>._)).Returns((InstalledIntegration?)null);
        var service = CreateService();

        // Act
        var result = await service.GetInstalledAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().BeEmpty();
    }

    // An update of the fake behaves like an installed integration: it resolves the source with the token it got,
    // records the source, and returns its result.
    private IAgentIntegration CreateAgent(string agentId, bool isAvailable)
    {
        var agent = A.Fake<IAgentIntegration>();
        A.CallTo(() => agent.AgentId).Returns(agentId);
        A.CallTo(() => agent.IsAvailable()).Returns(isAvailable);
        A.CallTo(() => agent.InstallAsync(A<MarketplaceSource>._, A<CancellationToken>._))
            .Returns(CreateResult(agentId));
        A.CallTo(() => agent.GetInstalledAsync(A<CancellationToken>._))
            .Returns(new InstalledIntegration(agentId, "shade-kit@creativecoders-shade-kit", "0.7.0", "v0.7.0", true));
        A.CallTo(() => agent.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .ReturnsLazily(async (Func<CancellationToken, Task<MarketplaceSource>> getSource,
                CancellationToken cancellationToken) =>
            {
                _updateSources.Add((agentId, await getSource(cancellationToken)));

                return (IntegrationResult?)CreateResult(agentId);
            });

        return agent;
    }

    // An update of the fake behaves like a missing integration: it resolves no source and returns null.
    private static void SetUpNotInstalled(IAgentIntegration agent)
    {
        A.CallTo(() => agent.UpdateAsync(A<Func<CancellationToken, Task<MarketplaceSource>>>._,
                A<CancellationToken>._))
            .Returns((IntegrationResult?)null);
    }

    public static TheoryData<Exception> AgentFailures()
    {
        return
        [
            new ClaudeUnavailableException(["plugin", "list", "--json"], "Cannot start."),
            new IntegrationConflictException("other", "m", "a", "b", "c"),
            new IntegrationIncompleteException("other", new ClaudeCommandFailedException(["x"], 1, "failed."))
        ];
    }

    private static IntegrationResult CreateResult(string agentId)
    {
        return new IntegrationResult(agentId, "shade-kit@creativecoders-shade-kit", IntegrationOutcome.Installed,
            null, "0.8.0", "v0.8.0", true);
    }

    private IntegrationService CreateService()
    {
        return new IntegrationService([_claude, _other], _engineVersionProvider, _gitRemoteTagChecker,
            name => _environment.GetValueOrDefault(name), () => CurrentDirectory);
    }
}
