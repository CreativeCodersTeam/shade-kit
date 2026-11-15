using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations;

public class MarketplaceChangeResolverTests
{
    private const string AgentId = "test-agent";

    private static readonly string[] ResolveCommands =
    [
        "test-agent plugin uninstall shade-kit@creativecoders-shade-kit",
        "test-agent plugin marketplace remove creativecoders-shade-kit"
    ];

    private const string Repo = "CreativeCodersTeam/shade-kit";

    private static readonly MarketplaceSource ReleaseSource = MarketplaceSource.ForGitHub(Repo, "v0.8.0");

    private readonly MarketplaceChangeResolver _resolver =
        new(AgentId, ResolveCommands, new DirectoryPathComparer(StringComparison.Ordinal));

    [Fact]
    public void Constructor_NullAgentId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new MarketplaceChangeResolver(null!, ResolveCommands,
            new DirectoryPathComparer(StringComparison.Ordinal));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agentId");
    }

    [Fact]
    public void Constructor_NullResolveCommands_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new MarketplaceChangeResolver(AgentId, null!,
            new DirectoryPathComparer(StringComparison.Ordinal));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("resolveCommands");
    }

    [Fact]
    public void Constructor_NullDirectoryPathComparer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new MarketplaceChangeResolver(AgentId, ResolveCommands, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("directoryPathComparer");
    }

    [Fact]
    public void Resolve_NullSource_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _resolver.Resolve(null, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void Resolve_NoMarketplace_ReturnsAdd()
    {
        // Act
        var change = _resolver.Resolve(null, ReleaseSource);

        // Assert
        change
            .Should().Be(MarketplaceChange.Add);
    }

    [Fact]
    public void Resolve_NoMarketplaceAndDirectorySource_ReturnsAdd()
    {
        // Act
        var change = _resolver.Resolve(null, MarketplaceSource.ForDirectory(Path.GetTempPath()));

        // Assert
        change
            .Should().Be(MarketplaceChange.Add);
    }

    [Fact]
    public void Resolve_SameRepoAndSameRef_ReturnsKeep()
    {
        // Act
        var change = _resolver.Resolve(CreateGitHubMarketplace(Repo, "v0.8.0"), ReleaseSource);

        // Assert
        change
            .Should().Be(MarketplaceChange.Keep);
    }

    [Fact]
    public void Resolve_SameRepoWithoutRefAndSourceWithoutRef_ReturnsKeep()
    {
        // Act
        var change = _resolver.Resolve(CreateGitHubMarketplace(Repo, null), MarketplaceSource.ForGitHub(Repo));

        // Assert
        change
            .Should().Be(MarketplaceChange.Keep);
    }

    [Theory]
    [InlineData("creativecodersteam/shade-kit")]
    [InlineData("CREATIVECODERSTEAM/SHADE-KIT")]
    public void Resolve_SameRepoInOtherCaseAndSameRef_ReturnsKeep(string marketplaceRepo)
    {
        // Act
        var change = _resolver.Resolve(CreateGitHubMarketplace(marketplaceRepo, "v0.8.0"), ReleaseSource);

        // Assert
        change
            .Should().Be(MarketplaceChange.Keep, "GitHub compares repositories case-insensitively");
    }

    [Theory]
    [InlineData("v0.7.0")]
    [InlineData("main")]
    [InlineData("V0.8.0")]
    public void Resolve_SameRepoAndOtherRef_ReturnsSwitch(string marketplaceRef)
    {
        // Act
        var change = _resolver.Resolve(CreateGitHubMarketplace(Repo, marketplaceRef), ReleaseSource);

        // Assert
        change
            .Should().Be(MarketplaceChange.Switch, "refs are compared case-sensitively, as git does");
    }

    [Fact]
    public void Resolve_SameRepoWithoutRefAndSourceWithRef_ReturnsSwitch()
    {
        // Act
        var change = _resolver.Resolve(CreateGitHubMarketplace(Repo, null), ReleaseSource);

        // Assert
        change
            .Should().Be(MarketplaceChange.Switch, "a marketplace without a ref follows the default branch");
    }

    [Fact]
    public void Resolve_SameRepoWithRefAndSourceWithoutRef_ReturnsSwitch()
    {
        // Act
        var change = _resolver.Resolve(CreateGitHubMarketplace(Repo, "v0.8.0"), MarketplaceSource.ForGitHub(Repo));

        // Assert
        change
            .Should().Be(MarketplaceChange.Switch);
    }

    [Fact]
    public void Resolve_OtherRepoWithRef_ThrowsConflictNamingSources()
    {
        // Act
        var act = () => _resolver.Resolve(CreateGitHubMarketplace("other/repo", "main"), ReleaseSource);

        // Assert
        var exception = act.Should().Throw<IntegrationConflictException>().Which;

        exception.AgentId
            .Should().Be(AgentId);

        exception.MarketplaceName
            .Should().Be(ShadeKitPlugin.MarketplaceName);

        exception.ExistingSource
            .Should().Be("other/repo#main");

        exception.RequestedSource
            .Should().Be("CreativeCodersTeam/shade-kit#v0.8.0");

        exception.ResolveCommands
            .Should().Equal(ResolveCommands);

        exception.Message
            .Should().Be("The plugin marketplace 'creativecoders-shade-kit' of the agent 'test-agent' has the " +
                         "source 'other/repo#main', but SHADE kit needs 'CreativeCodersTeam/shade-kit#v0.8.0'.");
    }

    [Fact]
    public void Resolve_ResolveCommandsChangedAfterConstruction_ConflictNamesCommandsOfConstruction()
    {
        // Arrange
        string[] resolveCommands = ["first", "second"];
        var resolver = new MarketplaceChangeResolver(AgentId, resolveCommands,
            new DirectoryPathComparer(StringComparison.Ordinal));
        resolveCommands[0] = "changed";

        // Act
        var act = () => resolver.Resolve(CreateGitHubMarketplace("other/repo", "main"), ReleaseSource);

        // Assert
        act.Should().Throw<IntegrationConflictException>()
            .Which.ResolveCommands.Should().Equal("first", "second");
    }

    [Fact]
    public void Resolve_OtherRepoWithoutRef_ThrowsConflictNamingRepoOnly()
    {
        // Act
        var act = () => _resolver.Resolve(CreateGitHubMarketplace("other/repo", null), ReleaseSource);

        // Assert
        act.Should().Throw<IntegrationConflictException>()
            .Which.ExistingSource.Should().Be("other/repo");
    }

    [Fact]
    public void Resolve_GitHubMarketplaceWithoutRepo_ThrowsConflictNamingKind()
    {
        // Act
        var act = () => _resolver.Resolve(
            new AgentMarketplace(ShadeKitPlugin.MarketplaceName, AgentMarketplaceSourceKind.GitHub, "github", null,
                "v0.8.0", null), ReleaseSource);

        // Assert
        act.Should().Throw<IntegrationConflictException>()
            .Which.ExistingSource.Should().Be("a source of the kind 'github'");
    }

    [Fact]
    public void Resolve_DirectoryMarketplaceAndGitHubSource_ThrowsConflictNamingPath()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");

        // Act
        var act = () => _resolver.Resolve(CreateDirectoryMarketplace(path), ReleaseSource);

        // Assert
        act.Should().Throw<IntegrationConflictException>()
            .Which.ExistingSource.Should().Be(path);
    }

    [Fact]
    public void Resolve_GitHubMarketplaceAndDirectorySource_ThrowsConflictNamingRepo()
    {
        // Arrange
        var source = MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "shade-kit"));

        // Act
        var act = () => _resolver.Resolve(CreateGitHubMarketplace(Repo, "v0.8.0"), source);

        // Assert
        var exception = act.Should().Throw<IntegrationConflictException>().Which;

        exception.ExistingSource
            .Should().Be("CreativeCodersTeam/shade-kit#v0.8.0");

        exception.RequestedSource
            .Should().Be(source.Text);
    }

    [Fact]
    public void Resolve_SameDirectory_ReturnsKeep()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");

        // Act
        var change = _resolver.Resolve(CreateDirectoryMarketplace(path + Path.DirectorySeparatorChar),
            MarketplaceSource.ForDirectory(path));

        // Assert
        change
            .Should().Be(MarketplaceChange.Keep);
    }

    [Fact]
    public void Resolve_OtherDirectory_ThrowsConflictNamingPath()
    {
        // Arrange
        var otherPath = Path.Combine(Path.GetTempPath(), "other");

        // Act
        var act = () => _resolver.Resolve(CreateDirectoryMarketplace(otherPath),
            MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "shade-kit")));

        // Assert
        var exception = act.Should().Throw<IntegrationConflictException>().Which;

        exception.ExistingSource
            .Should().Be(otherPath);

        exception.ResolveCommands
            .Should().Equal(ResolveCommands);
    }

    [Fact]
    public void Resolve_DirectoryInOtherCaseAndCaseSensitiveComparer_ThrowsConflict()
    {
        // Act
        var act = () => _resolver.Resolve(
            CreateDirectoryMarketplace(Path.Combine(Path.GetTempPath(), "SHADE-KIT")),
            MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "shade-kit")));

        // Assert
        act.Should().Throw<IntegrationConflictException>();
    }

    [Fact]
    public void Resolve_DirectoryInOtherCaseAndCaseInsensitiveComparer_ReturnsKeep()
    {
        // Arrange
        var resolver = new MarketplaceChangeResolver(AgentId, ResolveCommands,
            new DirectoryPathComparer(StringComparison.OrdinalIgnoreCase));

        // Act
        var change = resolver.Resolve(
            CreateDirectoryMarketplace(Path.Combine(Path.GetTempPath(), "SHADE-KIT")),
            MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "shade-kit")));

        // Assert
        change
            .Should().Be(MarketplaceChange.Keep);
    }

    [Theory]
    [InlineData("shade-kit")]
    [InlineData("./shade-kit")]
    public void Resolve_DirectoryMarketplaceWithRelativePath_ThrowsConflictNamingPath(string relativePath)
    {
        // Arrange
        var source = MarketplaceSource.ForDirectory(Path.Combine(Environment.CurrentDirectory, "shade-kit"));

        // Act
        var act = () => _resolver.Resolve(CreateDirectoryMarketplace(relativePath), source);

        // Assert
        act.Should().Throw<IntegrationConflictException>("only a fully qualified path names a directory")
            .Which.ExistingSource.Should().Be(relativePath);
    }

    [Fact]
    public void Resolve_DirectoryMarketplaceWithoutPath_ThrowsConflictNamingKind()
    {
        // Act
        var act = () => _resolver.Resolve(
            new AgentMarketplace(ShadeKitPlugin.MarketplaceName, AgentMarketplaceSourceKind.Directory, "local", null,
                null, null),
            MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "shade-kit")));

        // Assert
        act.Should().Throw<IntegrationConflictException>()
            .Which.ExistingSource.Should().Be("a source of the kind 'local'");
    }

    [Fact]
    public void Resolve_DirectoryMarketplaceThroughSymbolicLink_ReturnsKeep()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("shade-kit");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(target, "shade-kit-link");

        // Act
        var change = _resolver.Resolve(CreateDirectoryMarketplace(link), MarketplaceSource.ForDirectory(target));

        // Assert
        change
            .Should().Be(MarketplaceChange.Keep);
    }

    [Fact]
    public void Resolve_SymbolicLinkToOtherDirectory_ThrowsConflictNamingLink()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("shade-kit");
        var other = tempDirectory.CreateDirectory("other");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(other, "shade-kit-link");

        // Act
        var act = () => _resolver.Resolve(CreateDirectoryMarketplace(link), MarketplaceSource.ForDirectory(target));

        // Assert
        act.Should().Throw<IntegrationConflictException>()
            .Which.ExistingSource.Should().Be(link);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("C:\\kit")]
    public void Resolve_OtherKindWithRepoAndPathAndGitHubSource_ThrowsConflictNamingKind(string? path)
    {
        // Act
        var act = () => _resolver.Resolve(
            new AgentMarketplace(ShadeKitPlugin.MarketplaceName, AgentMarketplaceSourceKind.Other, "url", Repo,
                "v0.8.0", path), ReleaseSource);

        // Assert
        act.Should().Throw<IntegrationConflictException>("only a GitHub marketplace has a repository to compare")
            .Which.ExistingSource.Should().Be("a source of the kind 'url'");
    }

    [Fact]
    public void Resolve_OtherKindWithPathAndDirectorySource_ThrowsConflictNamingKind()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");

        // Act
        var act = () => _resolver.Resolve(
            new AgentMarketplace(ShadeKitPlugin.MarketplaceName, AgentMarketplaceSourceKind.Other, "git-subdir",
                null, null, path), MarketplaceSource.ForDirectory(path));

        // Assert
        act.Should().Throw<IntegrationConflictException>("only a directory marketplace has a path to compare")
            .Which.ExistingSource.Should().Be("a source of the kind 'git-subdir'");
    }

    [Fact]
    public void Resolve_UnknownKind_ThrowsConflictNamingUnknownKind()
    {
        // Act
        var act = () => _resolver.Resolve(
            new AgentMarketplace(ShadeKitPlugin.MarketplaceName, AgentMarketplaceSourceKind.Other, null, null, null,
                null), ReleaseSource);

        // Assert
        act.Should().Throw<IntegrationConflictException>()
            .Which.ExistingSource.Should().Be("a source of unknown kind");
    }

    [Fact]
    public void DescribeSource_NullMarketplace_ThrowsArgumentNullException()
    {
        // Act
        var act = () => MarketplaceChangeResolver.DescribeSource(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("marketplace");
    }

    private static AgentMarketplace CreateGitHubMarketplace(string repo, string? @ref)
    {
        return new AgentMarketplace(ShadeKitPlugin.MarketplaceName, AgentMarketplaceSourceKind.GitHub, "github", repo,
            @ref, null);
    }

    private static AgentMarketplace CreateDirectoryMarketplace(string path)
    {
        return new AgentMarketplace(ShadeKitPlugin.MarketplaceName, AgentMarketplaceSourceKind.Directory, "directory",
            null, null, path);
    }
}
