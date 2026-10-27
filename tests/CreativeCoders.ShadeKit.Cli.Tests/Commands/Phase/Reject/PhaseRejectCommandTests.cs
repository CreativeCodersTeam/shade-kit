using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Reject;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.SysConsole.Cli.Parsing;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase.Reject;

public class PhaseRejectCommandTests
{
    private const string Id = PhaseCommandTestHelper.FeatureId;

    [Fact]
    [Trait("Requirement", "FP04-AC6")]
    public async Task ExecuteAsync_JsonModeWithNote_WritesRejectedTransitionCurrentPhaseAndNote()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateWorkflow(CreateResult("DOR-04 failed")), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new PhaseRejectCommandOptions
        {
            Phase = "map", Note = "DOR-04 failed", Json = true
        });

        // Assert
        result.ExitCode
            .Should().Be(0);

        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso",
              "rejected": { "kind": "forward", "from": "refine", "to": "map" },
              "currentPhase": { "name": "refine", "state": "in-progress" },
              "note": "DOR-04 failed"
            }
            """);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithoutNote_WritesNullNote()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateWorkflow(CreateResult(null)), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseRejectCommandOptions { Phase = "map", Json = true });

        // Assert
        PhaseCommandTestHelper.GetData(jsonOutput).GetProperty("note").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("DOR-04 failed", "Note: DOR-04 failed")]
    [InlineData(null, "Note: none")]
    [InlineData("DOR-04 [failed] [red]x[/]", "Note: DOR-04 [failed] [red]x[/]")]
    public async Task ExecuteAsync_PlainMode_WritesRejectedTransitionCurrentPhaseAndNote(string? note,
        string expectedNoteLine)
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateWorkflow(CreateResult(note)));

        // Act
        await command.ExecuteAsync(new PhaseRejectCommandOptions { Phase = "map", Note = note });

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id}",
                "Rejected: refine -> map (forward)",
                "Current phase: refine (in-progress)",
                expectedNoteLine);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithWarning_WritesWarningBeforeResult()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console,
            CreateWorkflow(CreateResult(null, [FeatureCommandTestHelper.UpgradeWarning])));

        // Act
        await command.ExecuteAsync(new PhaseRejectCommandOptions { Phase = "map" });

        // Assert
        console.Lines[0]
            .Should().StartWith("Warning: ");

        console.Lines[1]
            .Should().Be($"Feature: {Id}");
    }

    [Theory]
    [InlineData("DOR-04 failed")]
    [InlineData(null)]
    public async Task ExecuteAsync_PhaseAndNote_ArePassedToWorkflow(string? note)
    {
        // Arrange
        var workflow = CreateWorkflow(CreateResult(note));
        var command = CreateCommand(new TestConsole(), workflow);

        // Act
        await command.ExecuteAsync(new PhaseRejectCommandOptions { Phase = "map", Note = note });

        // Assert
        A.CallTo(() => workflow.RejectAsync(A<IKitStore>._, "map", note, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void PhaseRejectCommandOptions_Note_IsOptionalNoteParameterWithShortName()
    {
        // Act
        var attribute = typeof(PhaseRejectCommandOptions).GetProperty(nameof(PhaseRejectCommandOptions.Note))!
            .GetCustomAttribute<OptionParameterAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.LongName
            .Should().Be("note");

        attribute.ShortName
            .Should().Be("n");

        attribute.IsRequired
            .Should().BeFalse();
    }

    private static PhaseRejectResult CreateResult(string? note, IReadOnlyList<KitWarning>? warnings = null)
    {
        return new PhaseRejectResult(Id, new PhaseTransition(PhaseTransitionKind.Forward, "refine", "map"),
            new PhaseSnapshot("refine", PhaseState.InProgress), note, warnings ?? []);
    }

    private static IPhaseWorkflow CreateWorkflow(PhaseRejectResult result)
    {
        var workflow = A.Fake<IPhaseWorkflow>();
        A.CallTo(() => workflow.RejectAsync(A<IKitStore>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(result);

        return workflow;
    }

    private static PhaseRejectCommand CreateCommand(IAnsiConsole console, IPhaseWorkflow workflow,
        TextWriter? jsonOutput = null)
    {
        return new PhaseRejectCommand(PhaseCommandTestHelper.CreateContext(console, jsonOutput), workflow);
    }
}
