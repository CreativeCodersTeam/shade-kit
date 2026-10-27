using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Approve;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase.Approve;

public class PhaseApproveCommandTests
{
    private const string Id = PhaseCommandTestHelper.FeatureId;

    [Fact]
    [Trait("Requirement", "FP04-AC5")]
    public async Task ExecuteAsync_JsonModeForwardApproval_WritesTransitionAndNewCurrentPhase()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateWorkflow(CreateResult()), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new PhaseApproveCommandOptions { Phase = "map", Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso",
              "transition": { "kind": "forward", "from": "refine", "to": "map" },
              "currentPhase": { "name": "map", "state": "in-progress" }
            }
            """);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC7")]
    public async Task ExecuteAsync_JsonModeReopenApproval_WritesReopenKind()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var approveResult = new PhaseApproveResult(Id, new PhaseTransition(PhaseTransitionKind.Reopen, "map", "refine"),
            new PhaseSnapshot("refine", PhaseState.InProgress), []);
        var command = CreateCommand(new TestConsole(), CreateWorkflow(approveResult), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseApproveCommandOptions { Phase = "refine", Json = true });

        // Assert
        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso",
              "transition": { "kind": "reopen", "from": "map", "to": "refine" },
              "currentPhase": { "name": "refine", "state": "in-progress" }
            }
            """);
    }

    [Fact]
    public async Task ExecuteAsync_PlainMode_WritesApprovedTransitionAndCurrentPhase()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateWorkflow(CreateResult()));

        // Act
        await command.ExecuteAsync(new PhaseApproveCommandOptions { Phase = "map" });

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id}",
                "Approved: refine -> map (forward)",
                "Current phase: map (in-progress)");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeReopenApproval_WritesReopenKind()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var approveResult = new PhaseApproveResult(Id, new PhaseTransition(PhaseTransitionKind.Reopen, "map", "refine"),
            new PhaseSnapshot("refine", PhaseState.InProgress), []);
        var command = CreateCommand(console, CreateWorkflow(approveResult));

        // Act
        await command.ExecuteAsync(new PhaseApproveCommandOptions { Phase = "refine" });

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id}",
                "Approved: map -> refine (reopen)",
                "Current phase: refine (in-progress)");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithWarning_WritesWarningBeforeResult()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateWorkflow(CreateResult([FeatureCommandTestHelper.UpgradeWarning])));

        // Act
        await command.ExecuteAsync(new PhaseApproveCommandOptions { Phase = "map" });

        // Assert
        console.Lines.Count
            .Should().Be(4);

        console.Lines[0]
            .Should().StartWith("Warning: ");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithWarning_WritesWarningCode()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            CreateWorkflow(CreateResult([FeatureCommandTestHelper.UpgradeWarning])), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseApproveCommandOptions { Phase = "map", Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded);
    }

    [Fact]
    public async Task ExecuteAsync_PhaseArgument_IsPassedToWorkflow()
    {
        // Arrange
        var workflow = CreateWorkflow(CreateResult());
        var command = CreateCommand(new TestConsole(), workflow);

        // Act
        await command.ExecuteAsync(new PhaseApproveCommandOptions { Phase = "map" });

        // Assert
        A.CallTo(() => workflow.ApproveAsync(A<IKitStore>._, "map", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    private static PhaseApproveResult CreateResult(IReadOnlyList<KitWarning>? warnings = null)
    {
        return new PhaseApproveResult(Id, new PhaseTransition(PhaseTransitionKind.Forward, "refine", "map"),
            new PhaseSnapshot("map", PhaseState.InProgress), warnings ?? []);
    }

    private static IPhaseWorkflow CreateWorkflow(PhaseApproveResult result)
    {
        var workflow = A.Fake<IPhaseWorkflow>();
        A.CallTo(() => workflow.ApproveAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._)).Returns(result);

        return workflow;
    }

    private static PhaseApproveCommand CreateCommand(IAnsiConsole console, IPhaseWorkflow workflow,
        TextWriter? jsonOutput = null)
    {
        return new PhaseApproveCommand(PhaseCommandTestHelper.CreateContext(console, jsonOutput), workflow);
    }
}
