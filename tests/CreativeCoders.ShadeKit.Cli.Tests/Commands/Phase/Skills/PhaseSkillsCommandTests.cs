using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Skills;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase.Skills;

public class PhaseSkillsCommandTests
{
    private const string Id = PhaseCommandTestHelper.FeatureId;

    [Fact]
    [Trait("Requirement", "FP04-AC10")]
    public async Task ExecuteAsync_JsonMode_WritesProfilePhaseAndSkills()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateWorkflow(CreateResult(["shade-kit-interview"])),
            jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new PhaseSkillsCommandOptions { Phase = "plan", Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "bugfix", "phase": "plan",
              "phaseSkill": "shade-kit-plan", "generalSkills": ["shade-kit-interview"]
            }
            """);
    }

    [Theory]
    [InlineData(new[] { "shade-kit-interview", "shade-kit-research" },
        "General skills: shade-kit-interview, shade-kit-research")]
    [InlineData(new string[0], "General skills: none")]
    public async Task ExecuteAsync_PlainMode_WritesProfilePhaseAndSkills(string[] generalSkills,
        string expectedSkillsLine)
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateWorkflow(CreateResult(generalSkills)));

        // Act
        await command.ExecuteAsync(new PhaseSkillsCommandOptions { Phase = "plan" });

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id} (bugfix)",
                "Phase: plan",
                "Phase skill: shade-kit-plan",
                expectedSkillsLine);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithWarning_WritesWarningCode()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            CreateWorkflow(CreateResult([], [FeatureCommandTestHelper.UpgradeWarning])), jsonOutput);

        // Act
        await command.ExecuteAsync(new PhaseSkillsCommandOptions { Phase = "plan", Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded);
    }

    [Fact]
    public async Task ExecuteAsync_PhaseArgument_IsPassedToWorkflow()
    {
        // Arrange
        var workflow = CreateWorkflow(CreateResult([]));
        var command = CreateCommand(new TestConsole(), workflow);

        // Act
        await command.ExecuteAsync(new PhaseSkillsCommandOptions { Phase = "plan" });

        // Assert
        A.CallTo(() => workflow.GetSkillsAsync(A<IKitStore>._, "plan", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    private static PhaseSkillsResult CreateResult(IReadOnlyList<string> generalSkills,
        IReadOnlyList<KitWarning>? warnings = null)
    {
        return new PhaseSkillsResult(Id, "bugfix", "plan", new PhaseSkills("shade-kit-plan", generalSkills),
            warnings ?? []);
    }

    private static IPhaseWorkflow CreateWorkflow(PhaseSkillsResult result)
    {
        var workflow = A.Fake<IPhaseWorkflow>();
        A.CallTo(() => workflow.GetSkillsAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(result);

        return workflow;
    }

    private static PhaseSkillsCommand CreateCommand(IAnsiConsole console, IPhaseWorkflow workflow,
        TextWriter? jsonOutput = null)
    {
        return new PhaseSkillsCommand(PhaseCommandTestHelper.CreateContext(console, jsonOutput), workflow);
    }
}
