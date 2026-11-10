using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase;
using CreativeCoders.ShadeKit.Core.Git;
using static CreativeCoders.ShadeKit.Cli.Tests.Commands.CliEndToEndHarness;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Gate;

/// <summary>
/// Runs <c>shade-kit gate info</c> through the <see cref="CliEndToEndHarness" /> against a real temporary repository; git is answered by a fake <see cref="IGitCommandRunner" /> and the tracker issue reader is a fake, so no
/// test starts <c>git</c> or <c>gh</c>.
/// </summary>
[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed class GateCommandsEndToEndTests : IDisposable
{
    private const string FeatureId = IssueFeatureId;

    private readonly CliEndToEndHarness _harness = new CliEndToEndHarness("shade-kit-gate-tests-");

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GateInfo_AfterInitAndFeatureNew_ReturnsDefinitionOfReadyGateForPlan()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "plan", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        run.Root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        run.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "feature",
              "featureDir": ".shadekit/features/gh-142-login-sso/",
              "phases": [
                { "name": "new", "state": "in-progress",
                  "artifacts": [ { "file": "request.md", "onDemand": false } ] },
                { "name": "refine", "state": "pending",
                  "artifacts": [ { "file": "spec.md", "onDemand": false }, { "file": "researches.md", "onDemand": false } ] },
                { "name": "map", "state": "pending",
                  "artifacts": [ { "file": "researches.md", "onDemand": false } ] },
                { "name": "plan", "state": "pending",
                  "artifacts": [ { "file": "plan.md", "onDemand": false }, { "file": "tasks.md", "onDemand": false } ] },
                { "name": "implement", "state": "pending",
                  "artifacts": [ { "file": "review.md", "onDemand": false } ] },
                { "name": "verify", "state": "pending",
                  "artifacts": [ { "file": "verification.md", "onDemand": false }, { "file": "playbook.md", "onDemand": false } ] },
                { "name": "finish", "state": "pending",
                  "artifacts": [ { "file": "report.md", "onDemand": true } ] }
              ],
              "gate": { "phase": "plan", "approval": true, "checklist": "checks/definition-of-ready.md",
                        "checklistPath": ".shadekit/features/gh-142-login-sso/checks/definition-of-ready.md",
                        "checklistExists": false },
              "coverageThreshold": null
            }
            """);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task GateInfo_FeatureOptionAfterPlanStart_ReturnsExistingChecklistCopyAndPhaseStates()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync(["phase", "start", "refine"], ["phase", "approve", "refine"],
            ["phase", "start", "map"], ["phase", "approve", "map"], ["phase", "start", "plan"]);

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "plan", "--feature", FeatureId, "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        var data = run.Root.GetProperty("data");

        data.GetProperty("phases").EnumerateArray()
            .Select(x => $"{x.GetProperty("name").GetString()}={x.GetProperty("state").GetString()}")
            .Should().Equal("new=approved", "refine=approved", "map=awaiting-approval", "plan=pending",
                "implement=pending", "verify=pending", "finish=pending");

        data.GetProperty("gate").GetProperty("checklistExists").GetBoolean()
            .Should().BeTrue();

        File.Exists(_harness.GetIssueFeaturePath("checks", "definition-of-ready.md"))
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GateInfo_ConfigWithCoverageThreshold_ReturnsThreshold()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        await SetCoverageThresholdAsync(82.5);

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "verify", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("coverageThreshold").GetDouble()
            .Should().Be(82.5);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GateInfo_PlainText_WritesFeaturePhasesGateAndThreshold()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var (exitCode, output, _) = await _harness.RunAsync("gate", "info", "refine");

        // Assert
        exitCode
            .Should().Be(0);

        // The host ends the output with line breaks of its own, which are not part of the command output.
        GetLines(output.TrimEnd('\r', '\n'))
            .Should().Equal(
                $"Feature: {FeatureId} (profile feature)",
                $"Feature directory: .shadekit/features/{FeatureId}/",
                "Phases:",
                "  new: in-progress",
                "  refine: pending",
                "  map: pending",
                "  plan: pending",
                "  implement: pending",
                "  verify: pending",
                "  finish: pending",
                "Gate: refine (approval: required, checklist: none)",
                "Coverage threshold: not set");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task GateInfo_UnknownFeature_WritesFailureEnvelopeWithFeatureNotFound()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "plan", "--feature", "does-not-exist", "--json");

        // Assert
        AssertError(run, 31, "feature-not-found");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task GateInfo_InvalidFeatureId_WritesFailureEnvelopeWithInvalidFeatureId()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "plan", "--feature", "Not_Valid", "--json");

        // Assert
        AssertError(run, 32, "invalid-feature-id");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task GateInfo_NoCurrentFeature_WritesFailureEnvelopeWithNoCurrentFeature()
    {
        // Arrange
        await _harness.InitAsync();

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "plan", "--json");

        // Assert
        AssertError(run, 37, "no-current-feature");
    }

    [Fact]
    public async Task GateInfo_WithoutPhaseArgument_WritesInvalidArgumentsEnvelope()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "--json");

        // Assert
        AssertError(run, 2, "invalid-arguments");
    }

    [Fact]
    public async Task GateInfo_BeforeInit_WritesFailureEnvelopeWithKitRootNotFound()
    {
        // Arrange
        _harness.TempDirectory.CreateGitRoot();
        Directory.SetCurrentDirectory(_harness.TempDirectory.RootPath);

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "plan", "--json");

        // Assert
        AssertError(run, 10, "kit-root-not-found");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task GateInfo_FirstPhase_ReturnsNullGate()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "new", "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        var data = run.Root.GetProperty("data");

        data.GetProperty("gate").ValueKind
            .Should().Be(JsonValueKind.Null);

        data.GetProperty("featureId").GetString()
            .Should().Be(FeatureId);

        data.GetProperty("phases").GetArrayLength()
            .Should().Be(7);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task GateInfo_PhaseNotInProfile_WritesFailureEnvelopeWithPhaseNotInProfile()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "nope", "--json");

        // Assert
        AssertError(run, 41, "phase-not-in-profile");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task GateInfo_DoneFeature_AnswersNormally()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("quick");
        await _harness.RunAllOkAsync(["phase", "start", "quick"], ["phase", "start", "finish"],
            ["phase", "approve", "finish"], ["phase", "complete"]);

        // Act
        var run = await _harness.RunJsonAsync("gate", "info", "finish", "--feature", FeatureId, "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "quick",
              "featureDir": ".shadekit/features/gh-142-login-sso/",
              "phases": [
                { "name": "new", "state": "approved", "artifacts": [ { "file": "request.md", "onDemand": false } ] },
                { "name": "quick", "state": "approved", "artifacts": [
                    { "file": "spec.md", "onDemand": false }, { "file": "review.md", "onDemand": false },
                    { "file": "verification.md", "onDemand": false }, { "file": "playbook.md", "onDemand": true } ] },
                { "name": "finish", "state": "approved", "artifacts": [ { "file": "report.md", "onDemand": true } ] }
              ],
              "gate": { "phase": "finish", "approval": true, "checklist": "checks/definition-of-done.md",
                        "checklistPath": ".shadekit/features/gh-142-login-sso/checks/definition-of-done.md",
                        "checklistExists": true },
              "coverageThreshold": null
            }
            """);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GateInfo_Json_ChangesNoFileInKitStore()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        var before = _harness.TempDirectory.TakeSnapshot();

        // Act
        var runs = new[]
        {
            (await _harness.RunAsync("gate", "info", "plan", "--json")).ExitCode,
            (await _harness.RunAsync("gate", "info", "new", "--json")).ExitCode,
            (await _harness.RunAsync("gate", "info", "finish", "--feature", FeatureId, "--json")).ExitCode,
            (await _harness.RunAsync("gate", "info", "refine")).ExitCode,
            (await _harness.RunAsync("gate", "info", "nope", "--json")).ExitCode
        };

        // Assert
        runs
            .Should().Equal(0, 0, 0, 0, 41);

        _harness.TempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(before);
    }

    public void Dispose()
    {
        _harness.Dispose();
    }

    private async Task SetCoverageThresholdAsync(double threshold)
    {
        var configPath = _harness.GetKitPath("config.json");
        var config = JsonNode.Parse(await File.ReadAllTextAsync(configPath, TestContext.Current.CancellationToken))!;

        config["coverageThreshold"] = threshold;

        await File.WriteAllTextAsync(configPath, config.ToJsonString(), TestContext.Current.CancellationToken);
    }
}
