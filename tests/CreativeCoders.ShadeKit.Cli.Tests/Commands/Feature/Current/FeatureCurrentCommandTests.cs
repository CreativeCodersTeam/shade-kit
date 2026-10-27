using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.Current;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature.Current;

public class FeatureCurrentCommandTests
{
    private const string Id = FeatureCommandTestHelper.FeatureId;

    [Theory]
    [InlineData(CurrentFeatureOrigin.Branch, "branch")]
    [InlineData(CurrentFeatureOrigin.Override, "override")]
    public async Task ExecuteAsync_PlainModeResolved_WritesFeatureAndOrigin(CurrentFeatureOrigin origin,
        string originName)
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateFeatureStore(CreateResolvedResult(origin)));

        // Act
        var result = await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines
            .Should().Equal($"{Id} (feature, active)", "Current phase: new (in-progress)",
                $"Resolved from: {originName}");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC6")]
    public async Task ExecuteAsync_PlainModeNotResolved_WritesHintAndReturnsSuccess()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateFeatureStore(CreateUnresolvedResult()));

        // Act
        var result = await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines
            .Should().Equal("No current feature.", "Run 'shade-kit feature set <id>' to select one.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeProfileWithMarkupCharacters_WritesProfileVerbatim()
    {
        // Arrange
        var console = new TestConsole();
        var resolved = CreateResolvedResult(CurrentFeatureOrigin.Branch) with
        {
            Feature = FeatureCommandTestHelper.CreateDocument(profile: "[red]x[/]")
        };
        var command = CreateCommand(console, CreateFeatureStore(resolved));

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines[0]
            .Should().Be($"{Id} ([red]x[/], active)");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC5")]
    public async Task ExecuteAsync_PlainModeWithMismatchWarning_WritesWarningBeforeFeature()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateFeatureStore(CreateResolvedResult(CurrentFeatureOrigin.Branch,
            [new CurrentFeatureMismatchWarning("other-feature", Id)])));

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines.Count
            .Should().Be(4);

        console.Lines[0]
            .Should().StartWith("Warning: ").And.Contain("other-feature").And.Contain(Id);

        console.Lines[1]
            .Should().Be($"{Id} (feature, active)");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC11")]
    public async Task ExecuteAsync_JsonModeResolved_WritesEnvelopeWithFeatureDataShape()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console,
            CreateFeatureStore(CreateResolvedResult(CurrentFeatureOrigin.Override, status: FeatureStatus.Done)),
            jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var data = document.RootElement.GetProperty("data");
        var feature = data.GetProperty("feature");

        FeatureCommandTestHelper.GetPropertyNames(data)
            .Should().Equal("resolved", "resolvedFrom", "feature");

        data.GetProperty("resolved").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("resolvedFrom").GetString()
            .Should().Be("override");

        FeatureCommandTestHelper.GetPropertyNames(feature)
            .Should().Equal("id", "title", "profile", "status", "currentPhase", "phaseState", "paths");

        feature.GetProperty("id").GetString()
            .Should().Be(Id);

        feature.GetProperty("title").GetString()
            .Should().Be("Login SSO");

        feature.GetProperty("profile").GetString()
            .Should().Be("feature");

        feature.GetProperty("status").GetString()
            .Should().Be("done");

        feature.GetProperty("currentPhase").GetString()
            .Should().Be("finish");

        feature.GetProperty("phaseState").GetString()
            .Should().Be("approved");

        feature.GetProperty("paths").GetProperty("featureDir").GetString()
            .Should().Be($".shadekit/features/{Id}/");

        result.ExitCode
            .Should().Be(0);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC6")]
    public async Task ExecuteAsync_JsonModeNotResolved_WritesNotResolvedDataWithNullsAndReturnsSuccess()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateFeatureStore(CreateUnresolvedResult()), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var root = document.RootElement;
        var data = root.GetProperty("data");

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("resolved").GetBoolean()
            .Should().BeFalse();

        data.GetProperty("resolvedFrom").ValueKind
            .Should().Be(JsonValueKind.Null);

        data.GetProperty("feature").ValueKind
            .Should().Be(JsonValueKind.Null);

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithWarnings_WritesWarningsInResultOrder()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var unresolved = CreateUnresolvedResult() with
        {
            Warnings = [FeatureCommandTestHelper.UpgradeWarning, new CurrentFeatureMissingWarning("gone")]
        };
        var command = CreateCommand(new TestConsole(), CreateFeatureStore(unresolved), jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded, ShadeKitCliWarningCodes.CurrentFeatureMissing);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeNotResolvedWithWarnings_WritesWarningsInResultOrderBeforeHint()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var unresolved = CreateUnresolvedResult() with
        {
            Warnings = [FeatureCommandTestHelper.UpgradeWarning, new CurrentFeatureMissingWarning("gone")]
        };
        var command = CreateCommand(console, CreateFeatureStore(unresolved));

        // Act
        var result = await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines.Count
            .Should().Be(4);

        console.Lines[0]
            .Should().StartWith("Warning: ").And.Contain("shade-kit upgrade");

        console.Lines[1]
            .Should().StartWith("Warning: ").And.Contain("'gone'");

        console.Lines.Skip(2)
            .Should().Equal("No current feature.", "Run 'shade-kit feature set <id>' to select one.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_RunToken_IsPassedToFeatureStore()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var featureStore = CreateFeatureStore(CreateUnresolvedResult());
        var command = CreateCommand(new TestConsole(), featureStore,
            cancellation: new TestCliCancellation(cancellationSource.Token));

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        A.CallTo(() => featureStore.GetCurrentAsync(A<IKitStore>._, cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PassesStoreOfFactoryToFeatureStore()
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        var featureStore = CreateFeatureStore(CreateUnresolvedResult());
        var context = TestShadeKitCommandContext.Create(new TestConsole(),
            kitStoreFactory: FeatureCommandTestHelper.CreateKitStoreFactory(kitStore));
        var command = new FeatureCurrentCommand(context, featureStore);

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        A.CallTo(() => featureStore.GetCurrentAsync(kitStore, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_GitUnavailable_ThrowsAbortExceptionWithGitUnavailableExitCode()
    {
        // Arrange
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.GetCurrentAsync(A<IKitStore>._, A<CancellationToken>._))
            .ThrowsAsync(new GitUnavailableException(["symbolic-ref", "--short", "-q", "HEAD"], "git not found"));

        var command = CreateCommand(new TestConsole(), featureStore);

        // Act
        var act = () => command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.GitUnavailable);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeKitRootNotFound_WritesErrorEnvelope()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var context = TestShadeKitCommandContext.Create(new TestConsole(),
            kitStoreFactory: FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")),
            jsonOutput: jsonOutput);
        var command = new FeatureCurrentCommand(context, A.Fake<IFeatureStore>());

        // Act
        var act = () => command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.PrintMessage
            .Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.KitRootNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), CreateFeatureStore(CreateUnresolvedResult()));

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Theory]
    [InlineData(0, "context")]
    [InlineData(1, "featureStore")]
    public void Ctor_NullArgument_ThrowsArgumentNullException(int nullIndex, string parameterName)
    {
        // Act
        var act = () => new FeatureCurrentCommand(
            nullIndex == 0 ? null! : TestShadeKitCommandContext.Create(new TestConsole()),
            nullIndex == 1 ? null! : A.Fake<IFeatureStore>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void FeatureCurrentCommand_CliCommandAttribute_IsRegisteredAsFeatureCurrentCommand()
    {
        // Act
        var attribute = typeof(FeatureCurrentCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("feature", "current");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeAwaitingApproval_WritesCurrentPhaseAndKebabCasePhaseState()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var feature = FeatureCommandTestHelper.CreateDocument();
        feature.CurrentPhase = WorkflowPhaseNames.Refine;
        feature.Phases = new OrderedDictionary<string, PhaseState>
        {
            [WorkflowPhaseNames.New] = PhaseState.Approved,
            [WorkflowPhaseNames.Refine] = PhaseState.AwaitingApproval
        };
        feature.PendingTransition = WorkflowPhaseNames.Map;
        var resolved = new CurrentFeatureResult(Id, feature, CurrentFeatureOrigin.Branch,
            FeatureCommandTestHelper.ReadPhaseState(feature), []);
        var command = CreateCommand(new TestConsole(), CreateFeatureStore(resolved), jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var data = document.RootElement.GetProperty("data").GetProperty("feature");

        data.GetProperty("currentPhase").GetString()
            .Should().Be("refine");

        data.GetProperty("phaseState").GetString()
            .Should().Be("awaiting-approval");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeAwaitingApproval_WritesCurrentPhaseWithState()
    {
        // Arrange
        var console = new TestConsole();
        var feature = FeatureCommandTestHelper.CreateDocument(profile: WorkflowProfileNames.Quick);
        feature.CurrentPhase = WorkflowPhaseNames.Quick;
        feature.Phases = new OrderedDictionary<string, PhaseState>
        {
            [WorkflowPhaseNames.New] = PhaseState.Approved,
            [WorkflowPhaseNames.Quick] = PhaseState.AwaitingApproval
        };
        feature.PendingTransition = WorkflowPhaseNames.Finish;
        var resolved = new CurrentFeatureResult(Id, feature, CurrentFeatureOrigin.Override,
            FeatureCommandTestHelper.ReadPhaseState(feature), []);
        var command = CreateCommand(console, CreateFeatureStore(resolved));

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines
            .Should().Equal($"{Id} (quick, active)", "Current phase: quick (awaiting-approval)",
                "Resolved from: override");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeInvalidPhaseState_WritesFailureEnvelopeWithExitCode46()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.GetCurrentAsync(A<IKitStore>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidPhaseStateException($".shadekit/features/{Id}/feature.json", "currentPhase",
                "names the phase 'review', which is not a phase of the workflow profile 'feature'."));
        var command = CreateCommand(new TestConsole(), featureStore, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode
            .Should().Be(ShadeKitCliExitCodes.PhaseStateInvalid);

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.PhaseStateInvalid);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeUnknownProfileInFeatureFile_WritesFailureEnvelopeWithExitCode40()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.GetCurrentAsync(A<IKitStore>._, A<CancellationToken>._))
            .ThrowsAsync(new UnknownProfileException("legacy", ["feature", "bugfix", "quick"],
                $".shadekit/features/{Id}/feature.json"));
        var command = CreateCommand(new TestConsole(), featureStore, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode
            .Should().Be(ShadeKitCliExitCodes.UnknownProfile);

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.UnknownProfile);
    }

    [Fact]
    public void FeatureCurrentCommandOptions_Type_DerivesFromJsonCommandOptions()
    {
        // Assert
        typeof(FeatureCurrentCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    private static CurrentFeatureResult CreateResolvedResult(CurrentFeatureOrigin origin,
        IReadOnlyList<KitWarning>? warnings = null, FeatureStatus status = FeatureStatus.Active)
    {
        var document = FeatureCommandTestHelper.CreateDocument(status: status);

        return new CurrentFeatureResult(Id, document, origin, FeatureCommandTestHelper.ReadPhaseState(document),
            warnings ?? []);
    }

    private static CurrentFeatureResult CreateUnresolvedResult()
    {
        return new CurrentFeatureResult(null, null, null, null, []);
    }

    private static IFeatureStore CreateFeatureStore(CurrentFeatureResult result)
    {
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.GetCurrentAsync(A<IKitStore>._, A<CancellationToken>._)).Returns(result);

        return featureStore;
    }

    private static FeatureCurrentCommand CreateCommand(IAnsiConsole console, IFeatureStore featureStore,
        TextWriter? jsonOutput = null, ICliCancellation? cancellation = null)
    {
        var context = TestShadeKitCommandContext.Create(console,
            kitStoreFactory: FeatureCommandTestHelper.CreateKitStoreFactory(), jsonOutput: jsonOutput,
            cancellation: cancellation);

        return new FeatureCurrentCommand(context, featureStore);
    }
}
