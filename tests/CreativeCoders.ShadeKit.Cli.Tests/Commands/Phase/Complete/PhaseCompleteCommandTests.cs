using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Complete;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase.Complete;

public class PhaseCompleteCommandTests
{
    private const string Id = PhaseCommandTestHelper.FeatureId;

    [Theory]
    [Trait("Requirement", "FP04-AC8")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_JsonMode_WritesPhaseStatusAndOverrideFlag(bool overrideCleared)
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateWorkflow(CreateResult(overrideCleared)), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new PhaseCompleteCommandOptions { Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        PhaseCommandTestHelper.GetData(jsonOutput).ShouldBeJson(
            $$"""
              {
                "featureId": "gh-142-login-sso", "phase": "finish", "status": "done",
                "overrideCleared": {{(overrideCleared ? "true" : "false")}}
              }
              """);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeOverrideCleared_WritesResultAndOverrideLine()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateWorkflow(CreateResult(true)));

        // Act
        await command.ExecuteAsync(new PhaseCompleteCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id}",
                "Completed phase: finish",
                "Status: done",
                "The local override pointed to this feature and was cleared.");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeOverrideKept_WritesResultWithoutOverrideLine()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateWorkflow(CreateResult(false)));

        // Act
        await command.ExecuteAsync(new PhaseCompleteCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                $"Feature: {Id}",
                "Completed phase: finish",
                "Status: done");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithWarning_WritesWarningBeforeResult()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console,
            CreateWorkflow(CreateResult(false, [FeatureCommandTestHelper.UpgradeWarning])));

        // Act
        await command.ExecuteAsync(new PhaseCompleteCommandOptions());

        // Assert
        console.Lines[0]
            .Should().StartWith("Warning: ");

        console.Lines[1]
            .Should().Be($"Feature: {Id}");
    }

    [Fact]
    public async Task ExecuteAsync_Always_CallsCompleteOnce()
    {
        // Arrange
        var workflow = CreateWorkflow(CreateResult(false));
        var command = CreateCommand(new TestConsole(), workflow);

        // Act
        await command.ExecuteAsync(new PhaseCompleteCommandOptions());

        // Assert
        A.CallTo(() => workflow.CompleteAsync(A<IKitStore>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    private static PhaseCompleteResult CreateResult(bool overrideCleared, IReadOnlyList<KitWarning>? warnings = null)
    {
        return new PhaseCompleteResult(Id, "finish", FeatureStatus.Done, overrideCleared, warnings ?? []);
    }

    private static IPhaseWorkflow CreateWorkflow(PhaseCompleteResult result)
    {
        var workflow = A.Fake<IPhaseWorkflow>();
        A.CallTo(() => workflow.CompleteAsync(A<IKitStore>._, A<CancellationToken>._)).Returns(result);

        return workflow;
    }

    private static PhaseCompleteCommand CreateCommand(IAnsiConsole console, IPhaseWorkflow workflow,
        TextWriter? jsonOutput = null)
    {
        return new PhaseCompleteCommand(PhaseCommandTestHelper.CreateContext(console, jsonOutput), workflow);
    }
}
