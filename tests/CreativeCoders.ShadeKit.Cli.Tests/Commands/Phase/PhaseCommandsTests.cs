using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Phase;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Approve;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Complete;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Next;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Reject;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Skills;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Start;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;
using CreativeCoders.SysConsole.Cli.Parsing;
using FakeItEasy;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase;

/// <summary>
/// Tests what every <c>shade-kit phase</c> command has in common: the registration, the options, the argument checks,
/// and the reporting of expected failures.
/// </summary>
public class PhaseCommandsTests
{
    private const string Id = PhaseCommandTestHelper.FeatureId;

    public static TheoryData<string> CommandNames { get; } =
    [
        PhaseStartCommand.Name, PhaseApproveCommand.Name, PhaseRejectCommand.Name, PhaseCompleteCommand.Name,
        PhaseSkillsCommand.Name, PhaseNextCommand.Name
    ];

    [Fact]
    public void CliAssembly_CliCommandGroupAttributes_ContainPhaseGroup()
    {
        // Act
        var groupAttributes = typeof(PhaseStartCommand).Assembly.GetCustomAttributes<CliCommandGroupAttribute>();

        // Assert
        groupAttributes
            .Where(x => x.Commands is ["phase"])
            .Should().ContainSingle();

        PhaseCommandGroup.Name
            .Should().Be("phase");
    }

    [Theory]
    [InlineData(typeof(PhaseStartCommand), typeof(PhaseStartCommandOptions), "start")]
    [InlineData(typeof(PhaseApproveCommand), typeof(PhaseApproveCommandOptions), "approve")]
    [InlineData(typeof(PhaseRejectCommand), typeof(PhaseRejectCommandOptions), "reject")]
    [InlineData(typeof(PhaseCompleteCommand), typeof(PhaseCompleteCommandOptions), "complete")]
    [InlineData(typeof(PhaseSkillsCommand), typeof(PhaseSkillsCommandOptions), "skills")]
    [InlineData(typeof(PhaseNextCommand), typeof(PhaseNextCommandOptions), "next")]
    public void PhaseCommand_CliCommandAttribute_IsRegisteredInPhaseGroupWithJsonOptions(Type commandType,
        Type optionsType, string name)
    {
        // Act
        var attribute = commandType.GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("phase", name);

        commandType
            .Should().Implement(typeof(ICliCommand<>).MakeGenericType(optionsType));

        optionsType
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    [Theory]
    [InlineData(typeof(PhaseStartCommandOptions))]
    [InlineData(typeof(PhaseApproveCommandOptions))]
    [InlineData(typeof(PhaseRejectCommandOptions))]
    [InlineData(typeof(PhaseSkillsCommandOptions))]
    public void PhaseCommandOptions_Phase_IsRequiredFirstPositionalValue(Type optionsType)
    {
        // Act
        var attribute = optionsType.GetProperty("Phase")!.GetCustomAttribute<OptionValueAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Index
            .Should().Be(0);

        attribute.IsRequired
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(PhaseCompleteCommandOptions))]
    [InlineData(typeof(PhaseNextCommandOptions))]
    public void PhaseCommandOptions_WithoutPhaseArgument_HaveNoPositionalValue(Type optionsType)
    {
        // Act
        var positionalProperties = optionsType.GetProperties()
            .Where(x => x.GetCustomAttribute<OptionValueAttribute>() is not null);

        // Assert
        positionalProperties
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(PhaseStartCommand))]
    [InlineData(typeof(PhaseApproveCommand))]
    [InlineData(typeof(PhaseRejectCommand))]
    [InlineData(typeof(PhaseCompleteCommand))]
    [InlineData(typeof(PhaseSkillsCommand))]
    [InlineData(typeof(PhaseNextCommand))]
    public void Ctor_NullContext_ThrowsArgumentNullException(Type commandType)
    {
        // Act
        var act = () => CreateCommand(commandType, null, A.Fake<IPhaseWorkflow>());

        // Assert
        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Theory]
    [InlineData(typeof(PhaseStartCommand))]
    [InlineData(typeof(PhaseApproveCommand))]
    [InlineData(typeof(PhaseRejectCommand))]
    [InlineData(typeof(PhaseCompleteCommand))]
    [InlineData(typeof(PhaseSkillsCommand))]
    [InlineData(typeof(PhaseNextCommand))]
    public void Ctor_NullWorkflow_ThrowsArgumentNullException(Type commandType)
    {
        // Act
        var act = () => CreateCommand(commandType, TestShadeKitCommandContext.Create(new TestConsole()), null);

        // Assert
        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentNullException>()
            .WithParameterName("phaseWorkflow");
    }

    [Theory]
    [InlineData(typeof(PhaseStartCommand))]
    [InlineData(typeof(PhaseApproveCommand))]
    [InlineData(typeof(PhaseRejectCommand))]
    [InlineData(typeof(PhaseCompleteCommand))]
    [InlineData(typeof(PhaseSkillsCommand))]
    [InlineData(typeof(PhaseNextCommand))]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException(Type commandType)
    {
        // Arrange
        var command = CreateCommand(commandType, TestShadeKitCommandContext.Create(new TestConsole()),
            A.Fake<IPhaseWorkflow>());
        var execute = commandType.GetMethod("ExecuteAsync")!;

        // Act
        var act = () => (Task<CommandResult>)execute.Invoke(command, [null])!;

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Theory]
    [InlineData(PhaseStartCommand.Name, ShadeKitCliErrorCodes.TransitionNotAllowed)]
    [InlineData(PhaseStartCommand.Name, ShadeKitCliErrorCodes.FeatureDone)]
    [InlineData(PhaseStartCommand.Name, ShadeKitCliErrorCodes.GitIdentityMissing)]
    [InlineData(PhaseApproveCommand.Name, ShadeKitCliErrorCodes.NoPendingTransition)]
    [InlineData(PhaseApproveCommand.Name, ShadeKitCliErrorCodes.PhaseStateInvalid)]
    [InlineData(PhaseRejectCommand.Name, ShadeKitCliErrorCodes.NoPendingTransition)]
    [InlineData(PhaseRejectCommand.Name, ShadeKitCliErrorCodes.FeatureDone)]
    [InlineData(PhaseCompleteCommand.Name, ShadeKitCliErrorCodes.CompleteNotAllowed)]
    [InlineData(PhaseCompleteCommand.Name, ShadeKitCliErrorCodes.UnknownProfile)]
    [InlineData(PhaseSkillsCommand.Name, ShadeKitCliErrorCodes.PhaseNotInProfile)]
    [InlineData(PhaseSkillsCommand.Name, ShadeKitCliErrorCodes.NoCurrentFeature)]
    [InlineData(PhaseNextCommand.Name, ShadeKitCliErrorCodes.NoCurrentFeature)]
    [InlineData(PhaseNextCommand.Name, ShadeKitCliErrorCodes.PhaseStateInvalid)]
    public async Task ExecuteAsync_JsonModeExpectedFailure_WritesFailureEnvelopeAndThrowsAbortWithExitCode(
        string command, string errorCode)
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var context = PhaseCommandTestHelper.CreateContext(console, jsonOutput);
        var workflow = PhaseCommandTestHelper.CreateThrowingWorkflow(CreateException(errorCode));

        // Act
        var act = () => PhaseCommandTestHelper.ExecuteAsync(command, context, workflow, json: true);

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(GetExitCode(errorCode));

        exception.PrintMessage
            .Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(errorCode);

        root.GetProperty("data").ValueKind
            .Should().Be(JsonValueKind.Null);

        console.Output
            .Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task ExecuteAsync_PlainModeExpectedFailure_ThrowsAbortWithMessageAndWritesNothing(string command)
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var context = PhaseCommandTestHelper.CreateContext(console, jsonOutput);
        var workflow = PhaseCommandTestHelper.CreateThrowingWorkflow(new NoCurrentFeatureException());

        // Act
        var act = () => PhaseCommandTestHelper.ExecuteAsync(command, context, workflow, json: false);

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.NoCurrentFeature);

        exception.PrintMessage
            .Should().BeTrue();

        exception.Message
            .Should().Contain("shade-kit feature set");

        console.Output
            .Should().BeEmpty();

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task ExecuteAsync_UnmappedFailure_EscapesUnchangedAndWritesNothing(string command)
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var context = PhaseCommandTestHelper.CreateContext(console, jsonOutput);
        var failure = new IOException("disk full");
        var workflow = PhaseCommandTestHelper.CreateThrowingWorkflow(failure);

        // Act
        var act = () => PhaseCommandTestHelper.ExecuteAsync(command, context, workflow, json: true);

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .Which.Should().BeSameAs(failure);

        jsonOutput.ToString()
            .Should().BeEmpty("the host writes the host-error envelope for an unmapped failure");

        console.Output
            .Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task ExecuteAsync_KitRootNotFound_ThrowsAbortWithoutCallingWorkflow(string command)
    {
        // Arrange
        var workflow = A.Fake<IPhaseWorkflow>();
        using var jsonOutput = new StringWriter();
        var context = TestShadeKitCommandContext.Create(new TestConsole(),
            kitStoreFactory: FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")),
            jsonOutput: jsonOutput);

        // Act
        var act = () => PhaseCommandTestHelper.ExecuteAsync(command, context, workflow, json: true);

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        jsonOutput.ToString()
            .Should().Contain(ShadeKitCliErrorCodes.KitRootNotFound);

        A.CallTo(workflow)
            .MustNotHaveHappened();
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task ExecuteAsync_StoreOfFactoryAndRunToken_ArePassedToWorkflow(string command)
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        using var cancellationSource = new CancellationTokenSource();
        var workflow = A.Fake<IPhaseWorkflow>();
        var context = PhaseCommandTestHelper.CreateContext(new TestConsole(),
            cancellation: new TestCliCancellation(cancellationSource.Token), kitStore: kitStore);
        var token = cancellationSource.Token;
        ConfigureResults(workflow);

        // Act
        await PhaseCommandTestHelper.ExecuteAsync(command, context, workflow, json: true);

        // Assert
        var calls = Fake.GetCalls(workflow).ToArray();

        calls
            .Should().ContainSingle();

        calls[0].Arguments[0]
            .Should().BeSameAs(kitStore);

        calls[0].Arguments[^1]
            .Should().Be(token);
    }

    private static object CreateCommand(Type commandType, ShadeKitCommandContext? context,
        IPhaseWorkflow? workflow)
    {
        return commandType.GetConstructors().Single().Invoke([context, workflow]);
    }

    private static void ConfigureResults(IPhaseWorkflow workflow)
    {
        var transition = new PhaseTransition(PhaseTransitionKind.Forward, "refine", "map");
        var snapshot = new PhaseSnapshot("refine", PhaseState.AwaitingApproval);
        var skills = new PhaseSkills("shade-kit-map", []);

        A.CallTo(() => workflow.StartAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(new PhaseStartResult(Id, "feature", PhaseStartOutcome.TransitionRequested, transition, snapshot,
                new PhaseGate("map", TransitionGate.Default), skills, []));
        A.CallTo(() => workflow.ApproveAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(new PhaseApproveResult(Id, transition, new PhaseSnapshot("map", PhaseState.InProgress), []));
        A.CallTo(() => workflow.RejectAsync(A<IKitStore>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(new PhaseRejectResult(Id, transition, new PhaseSnapshot("refine", PhaseState.InProgress), null,
                []));
        A.CallTo(() => workflow.CompleteAsync(A<IKitStore>._, A<CancellationToken>._))
            .Returns(new PhaseCompleteResult(Id, "finish", Core.Features.FeatureStatus.Done, false, []));
        A.CallTo(() => workflow.GetSkillsAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(new PhaseSkillsResult(Id, "feature", "map", skills, []));
        A.CallTo(() => workflow.GetNextAsync(A<IKitStore>._, A<CancellationToken>._))
            .Returns(new PhaseNextResult(Id, snapshot, Core.Features.FeatureStatus.Active, null, null, null, null,
                []));
    }

    private static Exception CreateException(string errorCode)
    {
        return errorCode switch
        {
            ShadeKitCliErrorCodes.NoCurrentFeature => new NoCurrentFeatureException(),
            ShadeKitCliErrorCodes.UnknownProfile => new UnknownProfileException("legacy",
                ["feature", "bugfix", "quick"], $".shadekit/features/{Id}/feature.json"),
            ShadeKitCliErrorCodes.PhaseNotInProfile => new PhaseNotInProfileException("review", "feature",
                ["new", "refine", "map", "plan", "implement", "verify", "finish"]),
            ShadeKitCliErrorCodes.TransitionNotAllowed => new PhaseTransitionNotAllowedException(Id, "plan", "refine",
                PhaseState.InProgress, null, PhaseTransitionRefusalReason.SkipsPhases),
            ShadeKitCliErrorCodes.NoPendingTransition => new NoPendingTransitionException(Id, "map", null),
            ShadeKitCliErrorCodes.FeatureDone => new FeatureDoneException(Id),
            ShadeKitCliErrorCodes.CompleteNotAllowed => new PhaseCompleteNotAllowedException(Id, "refine",
                PhaseState.InProgress, "finish"),
            ShadeKitCliErrorCodes.PhaseStateInvalid => new InvalidPhaseStateException(
                $".shadekit/features/{Id}/feature.json", "currentPhase", "it is not a phase of the profile"),
            ShadeKitCliErrorCodes.GitIdentityMissing => new GitIdentityNotConfiguredException("/repo",
                ["user.email"]),
            _ => throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "No exception for the code.")
        };
    }

    private static int GetExitCode(string errorCode)
    {
        return errorCode switch
        {
            ShadeKitCliErrorCodes.NoCurrentFeature => ShadeKitCliExitCodes.NoCurrentFeature,
            ShadeKitCliErrorCodes.UnknownProfile => ShadeKitCliExitCodes.UnknownProfile,
            ShadeKitCliErrorCodes.PhaseNotInProfile => ShadeKitCliExitCodes.PhaseNotInProfile,
            ShadeKitCliErrorCodes.TransitionNotAllowed => ShadeKitCliExitCodes.TransitionNotAllowed,
            ShadeKitCliErrorCodes.NoPendingTransition => ShadeKitCliExitCodes.NoPendingTransition,
            ShadeKitCliErrorCodes.FeatureDone => ShadeKitCliExitCodes.FeatureDone,
            ShadeKitCliErrorCodes.CompleteNotAllowed => ShadeKitCliExitCodes.CompleteNotAllowed,
            ShadeKitCliErrorCodes.PhaseStateInvalid => ShadeKitCliExitCodes.PhaseStateInvalid,
            ShadeKitCliErrorCodes.GitIdentityMissing => ShadeKitCliExitCodes.GitIdentityMissing,
            _ => throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "No exit code for the code.")
        };
    }
}
