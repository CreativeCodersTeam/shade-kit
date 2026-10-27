using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Start;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase.Start;

public class PhaseStartCommandTests
{
    private const string Id = PhaseCommandTestHelper.FeatureId;

    private static readonly PhaseTransition RefineToMap = new(PhaseTransitionKind.Forward, "refine", "map");

    private static readonly PhaseSkills MapSkills = new("shade-kit-map", ["shade-kit-research"]);

    [Fact]
    [Trait("Requirement", "FP04-AC2")]
    public async Task ExecuteAsync_JsonModeTransitionRequested_WritesDataWithGateAndSkillsOfTargetPhase()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateWorkflow(CreateRequestedResult()), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "map", Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "feature", "outcome": "transition-requested",
              "transition": { "kind": "forward", "from": "refine", "to": "map" },
              "currentPhase": { "name": "refine", "state": "awaiting-approval" },
              "gate": { "phase": "map", "approval": true, "checklist": null },
              "skills": { "phaseSkill": "shade-kit-map", "generalSkills": ["shade-kit-research"] }
            }
            """);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC11")]
    public async Task ExecuteAsync_JsonModeAlreadyInProgress_WritesNullTransitionAndNullGate()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var startResult = new PhaseStartResult(Id, "feature", PhaseStartOutcome.AlreadyInProgress, null,
            new PhaseSnapshot("refine", PhaseState.InProgress), null,
            new PhaseSkills("shade-kit-refine", ["shade-kit-interview", "shade-kit-research"]), []);
        var command = CreateCommand(new TestConsole(), CreateWorkflow(startResult), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "refine", Json = true });

        // Assert
        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "feature", "outcome": "already-in-progress",
              "transition": null,
              "currentPhase": { "name": "refine", "state": "in-progress" },
              "gate": null,
              "skills": { "phaseSkill": "shade-kit-refine",
                          "generalSkills": ["shade-kit-interview", "shade-kit-research"] }
            }
            """);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeTransitionPerformedWithChecklist_WritesReopenKindAndGateValues()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var startResult = new PhaseStartResult(Id, "feature", PhaseStartOutcome.TransitionPerformed,
            new PhaseTransition(PhaseTransitionKind.Reopen, "map", "refine"),
            new PhaseSnapshot("refine", PhaseState.InProgress),
            new PhaseGate("refine", new TransitionGate(false, ".shadekit/checks/dor.md")),
            new PhaseSkills("shade-kit-refine", []), []);
        var command = CreateCommand(new TestConsole(), CreateWorkflow(startResult), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "refine", Json = true });

        // Assert
        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "feature", "outcome": "transition-performed",
              "transition": { "kind": "reopen", "from": "map", "to": "refine" },
              "currentPhase": { "name": "refine", "state": "in-progress" },
              "gate": { "phase": "refine", "approval": false, "checklist": ".shadekit/checks/dor.md" },
              "skills": { "phaseSkill": "shade-kit-refine", "generalSkills": [] }
            }
            """);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeTransitionRequested_WritesResultLinesAndApprovalHint()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateWorkflow(CreateRequestedResult()));

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "map" });

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id} (feature)",
                "Outcome: transition-requested",
                "Transition: refine -> map (forward)",
                "Current phase: refine (awaiting-approval)",
                "Gate: map (approval: required, checklist: none)",
                "Phase skill: shade-kit-map",
                "General skills: shade-kit-research",
                "Approve with 'shade-kit phase approve map' or reject with 'shade-kit phase reject map'.");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeAlreadyInProgress_WritesNoneForTransitionAndGateWithoutHint()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var startResult = new PhaseStartResult(Id, "quick", PhaseStartOutcome.AlreadyInProgress, null,
            new PhaseSnapshot("finish", PhaseState.InProgress), null, new PhaseSkills("shade-kit-finish", []), []);
        var command = CreateCommand(console, CreateWorkflow(startResult));

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "finish" });

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id} (quick)",
                "Outcome: already-in-progress",
                "Transition: none",
                "Current phase: finish (in-progress)",
                "Gate: none",
                "Phase skill: shade-kit-finish",
                "General skills: none");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeResumePending_WritesPendingTransitionAndApprovalHint()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var startResult = new PhaseStartResult(Id, "feature", PhaseStartOutcome.ResumePending, RefineToMap,
            new PhaseSnapshot("refine", PhaseState.AwaitingApproval), null,
            new PhaseSkills("shade-kit-refine", ["shade-kit-interview", "shade-kit-research"]), []);
        var command = CreateCommand(console, CreateWorkflow(startResult));

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "refine" });

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id} (feature)",
                "Outcome: resume-pending",
                "Transition: refine -> map (forward)",
                "Current phase: refine (awaiting-approval)",
                "Gate: none",
                "Phase skill: shade-kit-refine",
                "General skills: shade-kit-interview, shade-kit-research",
                "Approve with 'shade-kit phase approve map' or reject with 'shade-kit phase reject map'.");
    }

    [Fact]
    [Trait("Requirement", "FP04-AC12")]
    public async Task ExecuteAsync_PlainModeTransitionPending_WritesSameGateAndApprovalHint()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var startResult = new PhaseStartResult(Id, "feature", PhaseStartOutcome.TransitionPending, RefineToMap,
            new PhaseSnapshot("refine", PhaseState.AwaitingApproval), new PhaseGate("map", TransitionGate.Default),
            MapSkills, []);
        var command = CreateCommand(console, CreateWorkflow(startResult));

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "map" });

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id} (feature)",
                "Outcome: transition-pending",
                "Transition: refine -> map (forward)",
                "Current phase: refine (awaiting-approval)",
                "Gate: map (approval: required, checklist: none)",
                "Phase skill: shade-kit-map",
                "General skills: shade-kit-research",
                "Approve with 'shade-kit phase approve map' or reject with 'shade-kit phase reject map'.");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeReopenRequested_WritesReopenKindAndHintForReopenTarget()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var startResult = new PhaseStartResult(Id, "feature", PhaseStartOutcome.TransitionRequested,
            new PhaseTransition(PhaseTransitionKind.Reopen, "map", "refine"),
            new PhaseSnapshot("map", PhaseState.AwaitingApproval), new PhaseGate("refine", TransitionGate.Default),
            new PhaseSkills("shade-kit-refine", []), []);
        var command = CreateCommand(console, CreateWorkflow(startResult));

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "refine" });

        // Assert
        console.Lines
            .Should().Contain("Transition: map -> refine (reopen)")
            .And.Contain("Current phase: map (awaiting-approval)")
            .And.Contain(
                "Approve with 'shade-kit phase approve refine' or reject with 'shade-kit phase reject refine'.");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeTransitionPerformedWithoutApproval_WritesGateWithoutHint()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var startResult = new PhaseStartResult(Id, "feature", PhaseStartOutcome.TransitionPerformed, RefineToMap,
            new PhaseSnapshot("map", PhaseState.InProgress),
            new PhaseGate("map", new TransitionGate(false, "checks/dor.md")), MapSkills, []);
        var command = CreateCommand(console, CreateWorkflow(startResult));

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "map" });

        // Assert
        console.Lines
            .Should().Contain("Gate: map (approval: not required, checklist: checks/dor.md)")
            .And.Contain("Current phase: map (in-progress)")
            .And.NotContain(x => x.StartsWith("Approve with", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithWarning_WritesWarningBeforeResult()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console,
            CreateWorkflow(CreateRequestedResult([FeatureCommandTestHelper.UpgradeWarning])));

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "map" });

        // Assert
        console.Lines[0]
            .Should().StartWith("Warning: ").And.Contain(".shadekit/config.json");

        console.Lines[1]
            .Should().Be($"Feature: {Id} (feature)");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithWarning_WritesWarningCodeAndNoPlainOutput()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console,
            CreateWorkflow(CreateRequestedResult([FeatureCommandTestHelper.UpgradeWarning])), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "map", Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PhaseArgument_IsPassedToWorkflow()
    {
        // Arrange
        var workflow = CreateWorkflow(CreateRequestedResult());
        var command = CreateCommand(new TestConsole(), workflow);

        // Act
        await command.ExecuteAsync(new PhaseStartCommandOptions { Phase = "map" });

        // Assert
        A.CallTo(() => workflow.StartAsync(A<IKitStore>._, "map", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    private static PhaseStartResult CreateRequestedResult(IReadOnlyList<KitWarning>? warnings = null)
    {
        return new PhaseStartResult(Id, "feature", PhaseStartOutcome.TransitionRequested, RefineToMap,
            new PhaseSnapshot("refine", PhaseState.AwaitingApproval), new PhaseGate("map", TransitionGate.Default),
            MapSkills, warnings ?? []);
    }

    private static IPhaseWorkflow CreateWorkflow(PhaseStartResult result)
    {
        var workflow = A.Fake<IPhaseWorkflow>();
        A.CallTo(() => workflow.StartAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._)).Returns(result);

        return workflow;
    }

    private static PhaseStartCommand CreateCommand(IAnsiConsole console, IPhaseWorkflow workflow,
        TextWriter? jsonOutput = null)
    {
        return new PhaseStartCommand(PhaseCommandTestHelper.CreateContext(console, jsonOutput), workflow);
    }
}
