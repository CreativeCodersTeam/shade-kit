using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Next;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace",
    Target = "~N:CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase.Next",
    Justification = "The test namespace mirrors the command folder Commands/Phase/Next/.")]

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase.Next;

public class PhaseNextCommandTests
{
    private const string Id = PhaseCommandTestHelper.FeatureId;

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public async Task ExecuteAsync_JsonModeInProgress_WritesContinueForwardAndReopenWithNullPending()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateWorkflow(CreateInProgressResult()), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new PhaseNextCommandOptions { Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            """
            {
              "current": { "featureId": "gh-142-login-sso", "phase": "map", "state": "in-progress",
                           "status": "active" },
              "continue": { "phase": "map", "skill": "shade-kit-map" },
              "pending": null,
              "forward": { "phase": "plan", "gate": { "approval": true, "checklist": null },
                           "skill": "shade-kit-plan" },
              "reopen": [ { "phase": "refine", "skill": "shade-kit-refine" } ]
            }
            """);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public async Task ExecuteAsync_JsonModeAwaitingApproval_WritesOnlyCurrentAndPending()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var nextResult = new PhaseNextResult(Id, new PhaseSnapshot("refine", PhaseState.AwaitingApproval),
            FeatureStatus.Active, null,
            new GatedPhaseStep("map", new TransitionGate(true, "checks/dor.md"), "shade-kit-map"), null, null, []);
        var command = CreateCommand(new TestConsole(), CreateWorkflow(nextResult), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseNextCommandOptions { Json = true });

        // Assert
        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            """
            {
              "current": { "featureId": "gh-142-login-sso", "phase": "refine", "state": "awaiting-approval",
                           "status": "active" },
              "continue": null,
              "pending": { "phase": "map", "gate": { "approval": true, "checklist": "checks/dor.md" },
                           "skill": "shade-kit-map" },
              "forward": null,
              "reopen": null
            }
            """);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public async Task ExecuteAsync_JsonModeDoneFeature_WritesOnlyCurrent()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateWorkflow(CreateDoneResult()), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseNextCommandOptions { Json = true });

        // Assert
        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            """
            {
              "current": { "featureId": "gh-142-login-sso", "phase": "finish", "state": "approved",
                           "status": "done" },
              "continue": null, "pending": null, "forward": null, "reopen": null
            }
            """);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeInProgressWithoutReopenablePhases_WritesEmptyReopenArray()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var nextResult = new PhaseNextResult(Id, new PhaseSnapshot("new", PhaseState.InProgress),
            FeatureStatus.Active, new PhaseStep("new", "shade-kit-new"),
            null, new GatedPhaseStep("quick", TransitionGate.Default, "shade-kit-quick"), [], []);
        var command = CreateCommand(new TestConsole(), CreateWorkflow(nextResult), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseNextCommandOptions { Json = true });

        // Assert
        PhaseCommandTestHelper.GetData(jsonOutput).GetProperty("reopen").GetArrayLength()
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeInProgress_WritesCurrentAndEveryOfferedStep()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateWorkflow(CreateInProgressResult(
            [new PhaseStep("refine", "shade-kit-refine"), new PhaseStep("new", "shade-kit-new")])));

        // Act
        await command.ExecuteAsync(new PhaseNextCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id} (active)",
                "Current phase: map (in-progress)",
                "Continue: map (skill: shade-kit-map)",
                "Forward: plan (skill: shade-kit-plan, approval: required, checklist: none)",
                "Reopen:",
                "  refine (skill: shade-kit-refine)",
                "  new (skill: shade-kit-new)");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeLastPhaseWithoutReopenablePhases_WritesReopenNone()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var nextResult = new PhaseNextResult(Id, new PhaseSnapshot("finish", PhaseState.InProgress),
            FeatureStatus.Active, new PhaseStep("finish", "shade-kit-finish"), null, null, [], []);
        var command = CreateCommand(console, CreateWorkflow(nextResult));

        // Act
        await command.ExecuteAsync(new PhaseNextCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id} (active)",
                "Current phase: finish (in-progress)",
                "Continue: finish (skill: shade-kit-finish)",
                "Reopen: none");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeAwaitingApproval_WritesPendingStep()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var nextResult = new PhaseNextResult(Id, new PhaseSnapshot("refine", PhaseState.AwaitingApproval),
            FeatureStatus.Active, null,
            new GatedPhaseStep("map", new TransitionGate(false, "checks/dor.md"), "shade-kit-map"), null, null, []);
        var command = CreateCommand(console, CreateWorkflow(nextResult));

        // Act
        await command.ExecuteAsync(new PhaseNextCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id} (active)",
                "Current phase: refine (awaiting-approval)",
                "Pending: map (skill: shade-kit-map, approval: not required, checklist: checks/dor.md)");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeDoneFeature_WritesOnlyCurrentLines()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateWorkflow(CreateDoneResult()));

        // Act
        await command.ExecuteAsync(new PhaseNextCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id} (done)",
                "Current phase: finish (approved)");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithWarning_WritesWarningBeforeResult()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console,
            CreateWorkflow(CreateDoneResult([FeatureCommandTestHelper.UpgradeWarning])));

        // Act
        await command.ExecuteAsync(new PhaseNextCommandOptions());

        // Assert
        console.Lines[0]
            .Should().StartWith("Warning: ");

        console.Lines[1]
            .Should().Be($"Feature: {Id} (done)");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithWarning_WritesWarningCode()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            CreateWorkflow(CreateDoneResult([FeatureCommandTestHelper.UpgradeWarning])), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseNextCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded);
    }

    private static PhaseNextResult CreateInProgressResult(IReadOnlyList<PhaseStep>? reopen = null)
    {
        return new PhaseNextResult(Id, new PhaseSnapshot("map", PhaseState.InProgress), FeatureStatus.Active,
            new PhaseStep("map", "shade-kit-map"), null,
            new GatedPhaseStep("plan", TransitionGate.Default, "shade-kit-plan"),
            reopen ?? [new PhaseStep("refine", "shade-kit-refine")], []);
    }

    private static PhaseNextResult CreateDoneResult(IReadOnlyList<KitWarning>? warnings = null)
    {
        return new PhaseNextResult(Id, new PhaseSnapshot("finish", PhaseState.Approved), FeatureStatus.Done, null,
            null, null, null, warnings ?? []);
    }

    private static IPhaseWorkflow CreateWorkflow(PhaseNextResult result)
    {
        var workflow = A.Fake<IPhaseWorkflow>();
        A.CallTo(() => workflow.GetNextAsync(A<IKitStore>._, A<CancellationToken>._)).Returns(result);

        return workflow;
    }

    private static PhaseNextCommand CreateCommand(IAnsiConsole console, IPhaseWorkflow workflow,
        TextWriter? jsonOutput = null)
    {
        return new PhaseNextCommand(PhaseCommandTestHelper.CreateContext(console, jsonOutput), workflow);
    }
}
