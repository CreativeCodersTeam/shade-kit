using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Approve;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Complete;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Next;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Reject;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Skills;
using CreativeCoders.ShadeKit.Cli.Commands.Phase.Start;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow;
using FakeItEasy;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase;

/// <summary>
/// Provides the context, the fakes, and the assertions that the unit tests of the phase commands share.
/// </summary>
internal static class PhaseCommandTestHelper
{
    public const string FeatureId = FeatureCommandTestHelper.FeatureId;

    /// <summary>
    /// Creates a real command context whose store factory returns the specified store or a fake one.
    /// </summary>
    public static ShadeKitCommandContext CreateContext(IAnsiConsole console, TextWriter? jsonOutput = null,
        ICliCancellation? cancellation = null, IKitStore? kitStore = null)
    {
        return TestShadeKitCommandContext.Create(console,
            kitStoreFactory: FeatureCommandTestHelper.CreateKitStoreFactory(kitStore), jsonOutput: jsonOutput,
            cancellation: cancellation);
    }

    /// <summary>
    /// Creates a fake workflow whose every operation fails with the specified exception.
    /// </summary>
    public static IPhaseWorkflow CreateThrowingWorkflow(Exception exception)
    {
        var workflow = A.Fake<IPhaseWorkflow>();

        A.CallTo(() => workflow.StartAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(exception);
        A.CallTo(() => workflow.ApproveAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(exception);
        A.CallTo(() => workflow.RejectAsync(A<IKitStore>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .ThrowsAsync(exception);
        A.CallTo(() => workflow.CompleteAsync(A<IKitStore>._, A<CancellationToken>._))
            .ThrowsAsync(exception);
        A.CallTo(() => workflow.GetSkillsAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(exception);
        A.CallTo(() => workflow.GetNextAsync(A<IKitStore>._, A<CancellationToken>._))
            .ThrowsAsync(exception);

        return workflow;
    }

    /// <summary>
    /// Runs the phase command with the specified name, with the phase argument <c>map</c> where it takes one.
    /// </summary>
    public static Task<CommandResult> ExecuteAsync(string command, ShadeKitCommandContext context,
        IPhaseWorkflow workflow, bool json)
    {
        return command switch
        {
            PhaseStartCommand.Name => new PhaseStartCommand(context, workflow)
                .ExecuteAsync(new PhaseStartCommandOptions { Phase = "map", Json = json }),
            PhaseApproveCommand.Name => new PhaseApproveCommand(context, workflow)
                .ExecuteAsync(new PhaseApproveCommandOptions { Phase = "map", Json = json }),
            PhaseRejectCommand.Name => new PhaseRejectCommand(context, workflow)
                .ExecuteAsync(new PhaseRejectCommandOptions { Phase = "map", Json = json }),
            PhaseCompleteCommand.Name => new PhaseCompleteCommand(context, workflow)
                .ExecuteAsync(new PhaseCompleteCommandOptions { Json = json }),
            PhaseSkillsCommand.Name => new PhaseSkillsCommand(context, workflow)
                .ExecuteAsync(new PhaseSkillsCommandOptions { Phase = "map", Json = json }),
            PhaseNextCommand.Name => new PhaseNextCommand(context, workflow)
                .ExecuteAsync(new PhaseNextCommandOptions { Json = json }),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown phase command.")
        };
    }

    /// <summary>
    /// Asserts that the specified JSON element has exactly the expected properties, in the expected order, with the
    /// expected values.
    /// </summary>
    public static void ShouldBeJson(this JsonElement actual, string expected)
    {
        JsonSerializer.Serialize(actual)
            .Should().Be(JsonNode.Parse(expected)!.ToJsonString());
    }

    /// <summary>
    /// Parses the JSON output and returns a copy of its <c>data</c> object.
    /// </summary>
    public static JsonElement GetData(StringWriter jsonOutput)
    {
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        return document.RootElement.GetProperty("data").Clone();
    }
}
