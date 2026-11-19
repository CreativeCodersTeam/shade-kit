using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations;

// A minimal integration runs the base class, so these tests start no agent CLI.
public class AgentIntegrationBaseTests
{
    private const string TestAgentId = "test-agent";

    private static readonly MarketplaceSource Source = MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit",
        "v0.8.0");

    private static readonly TestPlugin Plugin = new("shade-kit");

    private static readonly IntegrationResult Result =
        new(TestAgentId, ShadeKitPlugin.Id, IntegrationOutcome.Updated, "0.7.0", "0.8.0", "v0.8.0", true);

    private readonly TestAgentIntegration _integration = new();

    [Theory]
    [InlineData("/usr/local/bin/agent", true)]
    [InlineData(null, false)]
    public void IsAvailable_ExecutableFoundOrNot_ReturnsWhetherItIsFound(string? executablePath, bool expected)
    {
        // Arrange
        _integration.ExecutablePath = executablePath;

        // Act
        var isAvailable = _integration.IsAvailable();

        // Assert
        isAvailable.Should().Be(expected);
    }

    [Fact]
    public void EnsureAvailable_ExecutableNotFound_ThrowsNotFoundExceptionOfAgent()
    {
        // Arrange
        _integration.ExecutablePath = null;

        // Act
        var act = () => _integration.EnsureAvailable();

        // Assert
        act.Should().Throw<ExternalToolException>()
            .Which.Should().BeSameAs(_integration.NotFoundException);
    }

    [Fact]
    public void EnsureAvailable_ExecutableFound_DoesNotThrow()
    {
        // Arrange
        _integration.ExecutablePath = "/usr/local/bin/agent";

        // Act
        var act = () => _integration.EnsureAvailable();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task InstallAsync_NullSource_ThrowsArgumentNullExceptionWithoutReadingPlugin()
    {
        // Act
        var act = () => _integration.InstallAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("source");

        _integration.FindPluginCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InstallAsync_PluginInstalledOrNot_AppliesSourceWithPluginBefore(bool isInstalled)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        _integration.InstalledPlugin = isInstalled ? Plugin : null;

        // Act
        var result = await _integration.InstallAsync(Source, cancellationToken);

        // Assert
        result.Should().BeSameAs(Result);

        _integration.Applied.Should().ContainSingle()
            .Which.Should().Be((Source, isInstalled ? Plugin : null, cancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_NullGetSource_ThrowsArgumentNullExceptionWithoutReadingPlugin()
    {
        // Act
        var act = () => _integration.UpdateAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("getSource");

        _integration.FindPluginCalls.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_PluginNotInstalled_ReturnsNullWithoutResolvingSource()
    {
        // Arrange
        _integration.InstalledPlugin = null;
        var getSourceCalls = 0;

        // Act
        var result = await _integration.UpdateAsync(_ =>
        {
            getSourceCalls++;

            return Task.FromResult(Source);
        }, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();

        getSourceCalls.Should().Be(0, "a run in which nothing is installed resolves no source");

        _integration.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_PluginInstalled_ResolvesSourceOnceAfterReadingPluginAndApplies()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        _integration.InstalledPlugin = Plugin;
        var getSourceTokens = new List<CancellationToken>();
        var findPluginCallsAtResolve = -1;

        // Act
        var result = await _integration.UpdateAsync(token =>
        {
            getSourceTokens.Add(token);
            findPluginCallsAtResolve = _integration.FindPluginCalls;

            return Task.FromResult(Source);
        }, cancellationToken);

        // Assert
        result.Should().BeSameAs(Result);

        getSourceTokens.Should().Equal(cancellationToken);

        findPluginCallsAtResolve.Should().Be(1, "the source is resolved lazily, after the plugin was read");

        _integration.Applied.Should().ContainSingle()
            .Which.Should().Be((Source, Plugin, cancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_GetSourceReturnsNull_ThrowsArgumentNullExceptionWithoutApplying()
    {
        // Arrange
        _integration.InstalledPlugin = Plugin;

        // Act
        var act = () => _integration.UpdateAsync(_ => Task.FromResult<MarketplaceSource>(null!),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();

        _integration.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_GetSourceThrows_PropagatesExceptionUnchanged()
    {
        // Arrange
        _integration.InstalledPlugin = Plugin;
        var failure = new InvalidOperationException("no source");

        // Act
        var act = () => _integration.UpdateAsync(_ => Task.FromException<MarketplaceSource>(failure),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(failure);

        _integration.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task RunStepsAsync_NullSteps_ThrowsArgumentNullException()
    {
        // Arrange
        _integration.Steps = null;

        // Act
        var act = () => _integration.InstallAsync(Source, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("steps");
    }

    [Fact]
    public async Task RunStepsAsync_StepsSucceed_ReturnsResultOfSteps()
    {
        // Arrange
        _integration.Steps = steps =>
        {
            steps.StartSwitch();
            steps.MarkRemoved();

            return Task.FromResult(Result);
        };

        // Act
        var result = await _integration.InstallAsync(Source, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(Result);
    }

    [Fact]
    public async Task RunStepsAsync_ToolFailsBeforeRemoval_PropagatesFailureUnwrapped()
    {
        // Arrange
        var failure = CreateToolFailure();
        _integration.Steps = steps =>
        {
            steps.StartSwitch();

            return Task.FromException<IntegrationResult>(failure);
        };

        // Act
        var act = () => _integration.InstallAsync(Source, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ExternalToolException>())
            .Which.Should().BeSameAs(failure, "nothing was removed, so nothing needs a repair");
    }

    [Fact]
    public async Task RunStepsAsync_ToolFailsAfterRemoval_ThrowsIntegrationIncompleteWithAgentAndCause()
    {
        // Arrange
        var failure = CreateToolFailure();
        _integration.Steps = steps =>
        {
            steps.StartSwitch();
            steps.MarkRemoved();

            return Task.FromException<IntegrationResult>(failure);
        };

        // Act
        var act = () => _integration.InstallAsync(Source, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationIncompleteException>()).Which;

        exception.AgentId.Should().Be(TestAgentId);

        exception.InnerException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task RunStepsAsync_OtherFailureAfterRemoval_PropagatesFailureUnwrapped()
    {
        // Arrange
        var failure = new InvalidOperationException("not a tool failure");
        _integration.Steps = steps =>
        {
            steps.StartSwitch();
            steps.MarkRemoved();

            return Task.FromException<IntegrationResult>(failure);
        };

        // Act
        var act = () => _integration.InstallAsync(Source, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task RunStepsAsync_CanceledWhileSwitching_ThrowsSwitchCanceledWithAgentAndCause()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        _integration.Steps = steps =>
        {
            // The first removal has not finished: the killed CLI may have removed it.
            steps.StartSwitch();
            cancellationTokenSource.Cancel();

            return Task.FromException<IntegrationResult>(canceled);
        };

        // Act
        var act = () => _integration.InstallAsync(Source, cancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<IntegrationSwitchCanceledException>()).Which;

        exception.AgentId.Should().Be(TestAgentId);

        exception.InnerException.Should().BeSameAs(canceled);

        exception.CancellationToken.Should().Be(cancellationToken);
    }

    [Fact]
    public async Task RunStepsAsync_CanceledBeforeSwitchStarts_PropagatesCancellationUnwrapped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var canceled = new OperationCanceledException(cancellationToken);
        _integration.Steps = _ =>
        {
            // A step before the first removal of a switch, for example a check, is canceled.
            cancellationTokenSource.Cancel();

            return Task.FromException<IntegrationResult>(canceled);
        };

        // Act
        var act = () => _integration.InstallAsync(Source, cancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled, "nothing was removed yet, so nothing needs a repair");
    }

    [Fact]
    public async Task RunStepsAsync_OperationCanceledWhileSwitchingWithoutCanceledRunToken_PropagatesUnwrapped()
    {
        // Arrange
        var canceled = new OperationCanceledException();
        _integration.Steps = steps =>
        {
            steps.StartSwitch();
            steps.MarkRemoved();

            return Task.FromException<IntegrationResult>(canceled);
        };

        // Act
        var act = () => _integration.InstallAsync(Source, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceled, "only a cancellation of the run counts as a canceled switch");
    }

    private static ClaudeCommandFailedException CreateToolFailure()
    {
        return new ClaudeCommandFailedException(["plugin", "list", "--json"], 1, "the command failed.");
    }

    private sealed record TestPlugin(string Name);

    private sealed class TestAgentIntegration : AgentIntegrationBase<TestPlugin>
    {
        public string? ExecutablePath { get; set; } = "/usr/local/bin/agent";

        public ExternalToolException NotFoundException { get; } =
            new ClaudeNotFoundException(["plugin", "list", "--json"], "not found");

        public TestPlugin? InstalledPlugin { get; set; }

        public int FindPluginCalls { get; private set; }

        public List<(MarketplaceSource Source, TestPlugin? PluginBefore, CancellationToken CancellationToken)>
            Applied { get; } = [];

        public Func<IntegrationStepState, Task<IntegrationResult>>? Steps { get; set; } =
            _ => Task.FromResult(Result);

        public override string AgentId => TestAgentId;

        public override Task<InstalledIntegration?> GetInstalledAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<InstalledIntegration?>(null);
        }

        private protected override string? FindExecutable()
        {
            return ExecutablePath;
        }

        private protected override ExternalToolException CreateNotFoundException()
        {
            return NotFoundException;
        }

        private protected override Task<TestPlugin?> FindPluginAsync(CancellationToken cancellationToken)
        {
            FindPluginCalls++;

            return Task.FromResult(InstalledPlugin);
        }

        private protected override Task<IntegrationResult> ApplyAsync(MarketplaceSource source,
            TestPlugin? pluginBefore, CancellationToken cancellationToken)
        {
            Applied.Add((source, pluginBefore, cancellationToken));

            return RunStepsAsync(Steps!, cancellationToken);
        }
    }
}
