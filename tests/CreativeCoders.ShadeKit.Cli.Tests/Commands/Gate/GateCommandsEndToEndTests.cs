using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Trackers;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Gate;

/// <summary>
/// Runs <c>shade-kit gate info</c> through
/// <see cref="Program.RunAsync(string[], TextWriter, TextWriter, Action{IServiceCollection})" /> against a real temporary
/// repository; git is answered by a fake <see cref="IGitCommandRunner" /> and the tracker issue reader is a fake, so no
/// test starts <c>git</c> or <c>gh</c>.
/// </summary>
[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed class GateCommandsEndToEndTests : IDisposable
{
    private const string IssueUrl = "https://github.com/acme/shop/issues/142";

    private const string FeatureId = "gh-142-login-sso";

    private static readonly string[] CurrentBranchArguments = ["symbolic-ref", "--short", "-q", "HEAD"];

    private readonly CurrentDirectoryScope _currentDirectory = new CurrentDirectoryScope();

    private readonly IGitCommandRunner _gitCommandRunner = A.Fake<IGitCommandRunner>();

    private readonly ITrackerIssueReader _trackerIssueReader = A.Fake<ITrackerIssueReader>();

    private readonly TempKitDirectory _tempDirectory = new TempKitDirectory("shade-kit-gate-tests-");

    public GateCommandsEndToEndTests()
    {
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ReturnsLazily((string _, IReadOnlyList<string> arguments, CancellationToken _) =>
                Task.FromResult(AnswerGit(arguments)));

        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Returns(new TrackerIssue("Login with SSO", "Users sign in with the company account.", []));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GateInfo_AfterInitAndFeatureNew_ReturnsDefinitionOfReadyGateForPlan()
    {
        // Arrange
        await InitAsync();
        await CreateFeatureAsync("feature");

        // Act
        var run = await RunJsonAsync("gate", "info", "plan", "--json");

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
                { "name": "new", "state": "in-progress" }, { "name": "refine", "state": "pending" },
                { "name": "map", "state": "pending" }, { "name": "plan", "state": "pending" },
                { "name": "implement", "state": "pending" }, { "name": "verify", "state": "pending" },
                { "name": "finish", "state": "pending" }
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
        await InitAsync();
        await CreateFeatureAsync("feature");
        await RunAllOkAsync(["phase", "start", "refine"], ["phase", "approve", "refine"],
            ["phase", "start", "map"], ["phase", "approve", "map"], ["phase", "start", "plan"]);

        // Act
        var run = await RunJsonAsync("gate", "info", "plan", "--feature", FeatureId, "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        var data = run.Root.GetProperty("data");

        data.GetProperty("phases").ShouldBeJson(
            """
            [
              { "name": "new", "state": "approved" }, { "name": "refine", "state": "approved" },
              { "name": "map", "state": "awaiting-approval" }, { "name": "plan", "state": "pending" },
              { "name": "implement", "state": "pending" }, { "name": "verify", "state": "pending" },
              { "name": "finish", "state": "pending" }
            ]
            """);

        data.GetProperty("gate").GetProperty("checklistExists").GetBoolean()
            .Should().BeTrue();

        File.Exists(GetFeaturePath("checks", "definition-of-ready.md"))
            .Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GateInfo_ConfigWithCoverageThreshold_ReturnsThreshold()
    {
        // Arrange
        await InitAsync();
        await CreateFeatureAsync("feature");
        await SetCoverageThresholdAsync(82.5);

        // Act
        var run = await RunJsonAsync("gate", "info", "verify", "--json");

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
        await InitAsync();
        await CreateFeatureAsync("feature");

        // Act
        var (exitCode, output, _) = await RunAsync("gate", "info", "refine");

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
        await InitAsync();
        await CreateFeatureAsync("feature");

        // Act
        var run = await RunJsonAsync("gate", "info", "plan", "--feature", "does-not-exist", "--json");

        // Assert
        AssertError(run, 31, "feature-not-found");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task GateInfo_InvalidFeatureId_WritesFailureEnvelopeWithInvalidFeatureId()
    {
        // Arrange
        await InitAsync();
        await CreateFeatureAsync("feature");

        // Act
        var run = await RunJsonAsync("gate", "info", "plan", "--feature", "Not_Valid", "--json");

        // Assert
        AssertError(run, 32, "invalid-feature-id");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task GateInfo_NoCurrentFeature_WritesFailureEnvelopeWithNoCurrentFeature()
    {
        // Arrange
        await InitAsync();

        // Act
        var run = await RunJsonAsync("gate", "info", "plan", "--json");

        // Assert
        AssertError(run, 37, "no-current-feature");
    }

    [Fact]
    public async Task GateInfo_WithoutPhaseArgument_WritesInvalidArgumentsEnvelope()
    {
        // Arrange
        await InitAsync();
        await CreateFeatureAsync("feature");

        // Act
        var run = await RunJsonAsync("gate", "info", "--json");

        // Assert
        AssertError(run, 2, "invalid-arguments");
    }

    [Fact]
    public async Task GateInfo_BeforeInit_WritesFailureEnvelopeWithKitRootNotFound()
    {
        // Arrange
        _tempDirectory.CreateGitRoot();
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        // Act
        var run = await RunJsonAsync("gate", "info", "plan", "--json");

        // Assert
        AssertError(run, 10, "kit-root-not-found");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task GateInfo_FirstPhase_ReturnsNullGate()
    {
        // Arrange
        await InitAsync();
        await CreateFeatureAsync("feature");

        // Act
        var run = await RunJsonAsync("gate", "info", "new", "--json");

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
        await InitAsync();
        await CreateFeatureAsync("feature");

        // Act
        var run = await RunJsonAsync("gate", "info", "nope", "--json");

        // Assert
        AssertError(run, 41, "phase-not-in-profile");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task GateInfo_DoneFeature_AnswersNormally()
    {
        // Arrange
        await InitAsync();
        await CreateFeatureAsync("quick");
        await RunAllOkAsync(["phase", "start", "quick"], ["phase", "start", "finish"],
            ["phase", "approve", "finish"], ["phase", "complete"]);

        // Act
        var run = await RunJsonAsync("gate", "info", "finish", "--feature", FeatureId, "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "quick",
              "featureDir": ".shadekit/features/gh-142-login-sso/",
              "phases": [
                { "name": "new", "state": "approved" }, { "name": "quick", "state": "approved" },
                { "name": "finish", "state": "approved" }
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
        await InitAsync();
        await CreateFeatureAsync("feature");
        var before = _tempDirectory.TakeSnapshot();

        // Act
        var runs = new[]
        {
            (await RunAsync("gate", "info", "plan", "--json")).ExitCode,
            (await RunAsync("gate", "info", "new", "--json")).ExitCode,
            (await RunAsync("gate", "info", "finish", "--feature", FeatureId, "--json")).ExitCode,
            (await RunAsync("gate", "info", "refine")).ExitCode,
            (await RunAsync("gate", "info", "nope", "--json")).ExitCode
        };

        // Assert
        runs
            .Should().Equal(0, 0, 0, 0, 41);

        _tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(before);
    }

    public void Dispose()
    {
        _currentDirectory.Dispose();
        _tempDirectory.Dispose();
    }

    private static void AssertError((int ExitCode, JsonElement Root) run, int exitCode, string errorCode)
    {
        run.ExitCode
            .Should().Be(exitCode);

        run.Root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        run.Root.GetProperty("data").ValueKind
            .Should().Be(JsonValueKind.Null);

        run.Root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(errorCode);
    }

    private static ExternalProcessResult AnswerGit(IReadOnlyList<string> arguments)
    {
        if (arguments.SequenceEqual(CurrentBranchArguments))
        {
            return new ExternalProcessResult(0, "main\n", string.Empty);
        }

        return arguments switch
        {
            ["config", "user.name"] => new ExternalProcessResult(0, "Jane Doe\n", string.Empty),
            ["config", "user.email"] => new ExternalProcessResult(0, "jane@example.com\n", string.Empty),
            _ => new ExternalProcessResult(128, string.Empty, $"unexpected git call: {string.Join(' ', arguments)}")
        };
    }

    private async Task InitAsync()
    {
        _tempDirectory.CreateGitRoot();
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        (await RunAsync("init", "--json")).ExitCode
            .Should().Be(0);
    }

    private async Task CreateFeatureAsync(string profile)
    {
        (await RunJsonAsync("feature", "new", "--source", IssueUrl, "--title", "Login SSO", "--profile", profile,
                "--json"))
            .ExitCode.Should().Be(0);
    }

    private async Task RunAllOkAsync(params string[][] commands)
    {
        foreach (var command in commands)
        {
            (await RunJsonAsync([.. command, "--json"])).ExitCode
                .Should().Be(0, string.Join(' ', command));
        }
    }

    private async Task SetCoverageThresholdAsync(double threshold)
    {
        var configPath = Path.Combine(_tempDirectory.RootPath, ".shadekit", "config.json");
        var config = JsonNode.Parse(await File.ReadAllTextAsync(configPath, TestContext.Current.CancellationToken))!;

        config["coverageThreshold"] = threshold;

        await File.WriteAllTextAsync(configPath, config.ToJsonString(), TestContext.Current.CancellationToken);
    }

    private async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(args, output, error, x => x
            .AddSingleton(_gitCommandRunner)
            .AddSingleton(_trackerIssueReader));

        return (exitCode, output.ToString(), error.ToString());
    }

    private async Task<(int ExitCode, JsonElement Root)> RunJsonAsync(params string[] args)
    {
        var (exitCode, output, _) = await RunAsync(args);

        using var document = JsonDocument.Parse(output);

        return (exitCode, document.RootElement.Clone());
    }

    private string GetFeaturePath(params string[] parts)
    {
        return Path.Combine([_tempDirectory.RootPath, ".shadekit", "features", FeatureId, .. parts]);
    }

    private static string[] GetLines(string output)
    {
        return [.. output.Split('\n').Select(x => x.TrimEnd('\r'))];
    }
}
