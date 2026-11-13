using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static CreativeCoders.ShadeKit.Cli.Tests.Commands.CliEndToEndHarness;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase;

/// <summary>
/// Runs the <c>shade-kit phase</c> commands through the <see cref="CliEndToEndHarness" /> against a real temporary
/// repository; git is answered by a fake <see cref="IGitCommandRunner" />, the tracker issue reader is a fake, so no
/// test starts <c>git</c> or <c>gh</c>, and the clock is a <see cref="FakeTimeProvider" />, so the history timestamps
/// are exact.
/// </summary>
[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed class PhaseCommandsEndToEndTests : IDisposable
{
    private const string FeatureId = IssueFeatureId;

    private static readonly DateTimeOffset StartUtc = new(2026, 10, 26, 7, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo UtcPlusTwo =
        TimeZoneInfo.CreateCustomTimeZone("Test UTC+02", TimeSpan.FromHours(2), "Test UTC+02", "Test UTC+02");

    private readonly CliEndToEndHarness _harness = new CliEndToEndHarness("shade-kit-phase-tests-");

    private readonly FakeTimeProvider _timeProvider = new FakeTimeProvider(StartUtc);

    public PhaseCommandsEndToEndTests()
    {
        _timeProvider.SetLocalTimeZone(UtcPlusTwo);
        _harness.ConfigureServices = x => x.AddSingleton<TimeProvider>(_timeProvider);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC11")]
    [Trait("Requirement", "FP04-AC2")]
    [Trait("Requirement", "FP04-AC5")]
    [Trait("Requirement", "FP04-AC8")]
    [Trait("Requirement", "FP04-AC14")]
    [Trait("Requirement", "FP05-AC4")]
    [Trait("Requirement", "FP05-AC7")]
    public async Task QuickProfile_FullRun_CompletesFeatureRecordsHistoryAndClearsOverride()
    {
        // Arrange
        await _harness.InitAsync();
        _harness.Branch = $"feature/{FeatureId}";
        await _harness.CreateIssueFeatureAsync("quick");

        // Act
        var startQuick = await RunJsonAtAsync(1, "phase", "start", "quick", "--json");
        var startFinish = await RunJsonAtAsync(3, "phase", "start", "finish", "--json");
        var approveFinish = await RunJsonAtAsync(4, "phase", "approve", "finish", "--json");
        var nextInFinish = await RunJsonAtAsync(5, "phase", "next", "--json");
        var complete = await RunJsonAtAsync(6, "phase", "complete", "--json");
        var nextWhenDone = await RunJsonAtAsync(7, "phase", "next", "--json");
        var current = await RunJsonAtAsync(8, "feature", "current", "--json");

        // Assert
        new[]
            {
                startQuick.ExitCode, startFinish.ExitCode, approveFinish.ExitCode,
                nextInFinish.ExitCode, complete.ExitCode, nextWhenDone.ExitCode, current.ExitCode
            }
            .Should().AllBeEquivalentTo(0);

        startQuick.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "quick", "outcome": "transition-performed",
              "transition": { "kind": "forward", "from": "new", "to": "quick" },
              "currentPhase": { "name": "quick", "state": "in-progress" },
              "gate": { "phase": "quick", "approval": false, "checklist": null, "checklistCreated": null },
              "skills": { "phaseSkill": "shade-kit-quick", "generalSkills": ["shade-kit-interview", "shade-kit-review"] }
            }
            """);

        startFinish.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "quick", "outcome": "transition-requested",
              "transition": { "kind": "forward", "from": "quick", "to": "finish" },
              "currentPhase": { "name": "quick", "state": "awaiting-approval" },
              "gate": { "phase": "finish", "approval": true, "checklist": "checks/definition-of-done.md",
                        "checklistCreated": true },
              "skills": { "phaseSkill": "shade-kit-finish", "generalSkills": ["shade-kit-report"] }
            }
            """);

        approveFinish.Root.GetProperty("data").GetProperty("currentPhase").ShouldBeJson(
            """{ "name": "finish", "state": "in-progress" }""");

        nextInFinish.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "current": { "featureId": "gh-142-login-sso", "phase": "finish", "state": "in-progress",
                           "status": "active" },
              "continue": { "phase": "finish", "skill": "shade-kit-finish" },
              "pending": null,
              "forward": null,
              "reopen": [ { "phase": "quick", "skill": "shade-kit-quick" } ]
            }
            """);

        var definitionOfDone = await File.ReadAllTextAsync(_harness.GetIssueFeaturePath("checks", "definition-of-done.md"),
            TestContext.Current.CancellationToken);

        definitionOfDone
            .Should().StartWith($"# Definition of Done: {FeatureId}")
            .And.NotContain("{{feature-id}}");

        complete.Root.GetProperty("data").ShouldBeJson(
            """{ "featureId": "gh-142-login-sso", "phase": "finish", "status": "done", "overrideCleared": true }""");

        nextWhenDone.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "current": { "featureId": "gh-142-login-sso", "phase": "finish", "state": "approved",
                           "status": "done" },
              "continue": null, "pending": null, "forward": null, "reopen": null
            }
            """);

        var currentFeature = current.Root.GetProperty("data").GetProperty("feature");

        current.Root.GetProperty("data").GetProperty("resolvedFrom").GetString()
            .Should().Be("branch");

        currentFeature.GetProperty("status").GetString()
            .Should().Be("done");

        currentFeature.GetProperty("currentPhase").GetString()
            .Should().Be("finish");

        currentFeature.GetProperty("phaseState").GetString()
            .Should().Be("approved");

        File.Exists(_harness.GetCurrentFeatureFilePath())
            .Should().BeFalse("phase complete clears the local override that selects the completed feature");

        using var featureFile = await ReadFeatureFileAsync();
        var stored = featureFile.RootElement;

        stored.GetProperty("phases").ShouldBeJson(
            """{ "new": "approved", "quick": "approved", "finish": "approved" }""");

        stored.TryGetProperty("pendingTransition", out _)
            .Should().BeFalse();

        stored.GetProperty("history").ShouldBeJson(
            """
            [
              { "at": "2026-10-26T09:01:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "approve", "from": "new", "to": "quick", "note": "approval off" },
              { "at": "2026-10-26T09:03:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "start", "from": "quick", "to": "finish" },
              { "at": "2026-10-26T09:04:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "approve", "from": "quick", "to": "finish" },
              { "at": "2026-10-26T09:06:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "complete", "from": "finish" }
            ]
            """);
    }

    [Fact]
    public async Task QuickProfile_DoneFeature_RefusesMutatingCommandsWith44AndAnswersReadingCommands()
    {
        // Arrange
        await _harness.InitAsync();
        _harness.Branch = $"feature/{FeatureId}";
        await _harness.CreateIssueFeatureAsync("quick");
        await _harness.RunAllOkAsync(["phase", "start", "quick"], ["phase", "start", "finish"], ["phase", "approve", "finish"],
            ["phase", "complete"]);
        var bytesBefore = await ReadFeatureFileBytesAsync();

        // Act
        var start = await RunJsonAtAsync(1, "phase", "start", "finish", "--json");
        var complete = await RunJsonAtAsync(2, "phase", "complete", "--json");
        var skills = await RunJsonAtAsync(3, "phase", "skills", "quick", "--json");

        // Assert
        AssertError(start, ShadeKitCliExitCodes.FeatureDone, ShadeKitCliErrorCodes.FeatureDone);
        AssertError(complete, ShadeKitCliExitCodes.FeatureDone, ShadeKitCliErrorCodes.FeatureDone);

        skills.ExitCode
            .Should().Be(0);

        skills.Root.GetProperty("data").ShouldBeJson(
            """
            { "featureId": "gh-142-login-sso", "profile": "quick", "phase": "quick",
              "phaseSkill": "shade-kit-quick", "generalSkills": ["shade-kit-interview", "shade-kit-review"] }
            """);

        (await ReadFeatureFileBytesAsync())
            .Should().Equal(bytesBefore);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC5")]
    [Trait("Requirement", "FP04-AC6")]
    [Trait("Requirement", "FP04-AC7")]
    public async Task FeatureProfile_RejectApproveAndReopen_RecordsHistoryResetsLaterPhasesAndKeepsArtifacts()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        var requestBytes = await File.ReadAllBytesAsync(_harness.GetIssueFeaturePath("request.md"),
            TestContext.Current.CancellationToken);

        // Act
        var startRefine = await RunJsonAtAsync(1, "phase", "start", "refine", "--json");
        var rejectRefine = await RunJsonAtAsync(2, "phase", "reject", "refine", "--note", "DOR-04 failed", "--json");
        await RunJsonAtAsync(3, "phase", "start", "refine", "--json");
        await RunJsonAtAsync(4, "phase", "approve", "refine", "--json");
        await File.WriteAllTextAsync(_harness.GetIssueFeaturePath("spec.md"), "# Spec\n", TestContext.Current.CancellationToken);
        await RunJsonAtAsync(5, "phase", "start", "map", "--json");
        await RunJsonAtAsync(6, "phase", "approve", "map", "--json");
        await File.WriteAllTextAsync(_harness.GetIssueFeaturePath("map.md"), "# Map\n", TestContext.Current.CancellationToken);
        var reopen = await RunJsonAtAsync(7, "phase", "start", "refine", "--json");
        var approveReopen = await RunJsonAtAsync(8, "phase", "approve", "refine", "--json");
        var show = await RunJsonAtAsync(9, "feature", "show", FeatureId, "--json");

        // Assert
        startRefine.Root.GetProperty("data").GetProperty("outcome").GetString()
            .Should().Be("transition-requested");

        rejectRefine.ExitCode
            .Should().Be(0);

        rejectRefine.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso",
              "rejected": { "kind": "forward", "from": "new", "to": "refine" },
              "currentPhase": { "name": "new", "state": "in-progress" },
              "note": "DOR-04 failed"
            }
            """);

        reopen.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "feature", "outcome": "transition-requested",
              "transition": { "kind": "reopen", "from": "map", "to": "refine" },
              "currentPhase": { "name": "map", "state": "awaiting-approval" },
              "gate": { "phase": "refine", "approval": true, "checklist": null, "checklistCreated": null },
              "skills": { "phaseSkill": "shade-kit-refine",
                          "generalSkills": ["shade-kit-interview", "shade-kit-research"] }
            }
            """);

        approveReopen.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso",
              "transition": { "kind": "reopen", "from": "map", "to": "refine" },
              "currentPhase": { "name": "refine", "state": "in-progress" }
            }
            """);

        var shown = show.Root.GetProperty("data");

        shown.GetProperty("currentPhase").GetString()
            .Should().Be("refine");

        shown.GetProperty("pendingTransition").ValueKind
            .Should().Be(JsonValueKind.Null);

        shown.GetProperty("phases").EnumerateArray()
            .Select(x => $"{x.GetProperty("name").GetString()}={x.GetProperty("state").GetString()}")
            .Should().Equal("new=approved", "refine=in-progress", "map=pending", "plan=pending", "implement=pending",
                "verify=pending", "finish=pending");

        shown.GetProperty("history").ShouldBeJson(
            """
            [
              { "at": "2026-10-26T09:01:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "start", "from": "new", "to": "refine", "note": null },
              { "at": "2026-10-26T09:02:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "reject", "from": "new", "to": "refine", "note": "DOR-04 failed" },
              { "at": "2026-10-26T09:03:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "start", "from": "new", "to": "refine", "note": null },
              { "at": "2026-10-26T09:04:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "approve", "from": "new", "to": "refine", "note": null },
              { "at": "2026-10-26T09:05:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "start", "from": "refine", "to": "map", "note": null },
              { "at": "2026-10-26T09:06:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "approve", "from": "refine", "to": "map", "note": null },
              { "at": "2026-10-26T09:07:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "reopen", "from": "map", "to": "refine", "note": null },
              { "at": "2026-10-26T09:08:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "approve", "from": "map", "to": "refine", "note": null }
            ]
            """);

        (await File.ReadAllTextAsync(_harness.GetIssueFeaturePath("spec.md"), TestContext.Current.CancellationToken))
            .Should().Be("# Spec\n");

        (await File.ReadAllTextAsync(_harness.GetIssueFeaturePath("map.md"), TestContext.Current.CancellationToken))
            .Should().Be("# Map\n");

        (await File.ReadAllBytesAsync(_harness.GetIssueFeaturePath("request.md"), TestContext.Current.CancellationToken))
            .Should().Equal(requestBytes);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC11")]
    [Trait("Requirement", "FP04-AC12")]
    [Trait("Requirement", "FP04-AC13")]
    [Trait("Requirement", "FP04-AC14")]
    public async Task NoOpCommands_RepeatedStartReEntryResumeNextAndSkills_LeaveFeatureFileUnchanged()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync(["phase", "start", "refine"], ["phase", "approve", "refine"]);
        var inProgressBytes = await ReadFeatureFileBytesAsync();

        // Act
        var reEntry = await RunJsonAtAsync(1, "phase", "start", "refine", "--json");
        var nextInProgress = await RunJsonAtAsync(2, "phase", "next", "--json");
        var skills = await RunJsonAtAsync(3, "phase", "skills", "map", "--json");
        var bytesAfterInProgressCalls = await ReadFeatureFileBytesAsync();

        var request = await RunJsonAtAsync(4, "phase", "start", "map", "--json");
        var pendingBytes = await ReadFeatureFileBytesAsync();
        var repeated = await RunJsonAtAsync(5, "phase", "start", "map", "--json");
        var resume = await RunJsonAtAsync(6, "phase", "start", "refine", "--json");
        var nextPending = await RunJsonAtAsync(7, "phase", "next", "--json");
        var bytesAfterPendingCalls = await ReadFeatureFileBytesAsync();

        // Assert
        new[]
            {
                reEntry.ExitCode, nextInProgress.ExitCode, skills.ExitCode, request.ExitCode, repeated.ExitCode,
                resume.ExitCode, nextPending.ExitCode
            }
            .Should().AllBeEquivalentTo(0);

        reEntry.Root.GetProperty("data").ShouldBeJson(
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

        nextInProgress.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "current": { "featureId": "gh-142-login-sso", "phase": "refine", "state": "in-progress",
                           "status": "active" },
              "continue": { "phase": "refine", "skill": "shade-kit-refine" },
              "pending": null,
              "forward": { "phase": "map", "gate": { "approval": true, "checklist": null }, "skill": "shade-kit-map" },
              "reopen": []
            }
            """);

        skills.Root.GetProperty("data").ShouldBeJson(
            """
            { "featureId": "gh-142-login-sso", "profile": "feature", "phase": "map",
              "phaseSkill": "shade-kit-map", "generalSkills": ["shade-kit-research"] }
            """);

        bytesAfterInProgressCalls
            .Should().Equal(inProgressBytes);

        repeated.Root.GetProperty("data").GetProperty("outcome").GetString()
            .Should().Be("transition-pending");

        repeated.Root.GetProperty("data").GetProperty("gate")
            .ShouldBeJson(JsonSerializer.Serialize(request.Root.GetProperty("data").GetProperty("gate")));

        resume.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "feature", "outcome": "resume-pending",
              "transition": { "kind": "forward", "from": "refine", "to": "map" },
              "currentPhase": { "name": "refine", "state": "awaiting-approval" },
              "gate": null,
              "skills": { "phaseSkill": "shade-kit-refine",
                          "generalSkills": ["shade-kit-interview", "shade-kit-research"] }
            }
            """);

        nextPending.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "current": { "featureId": "gh-142-login-sso", "phase": "refine", "state": "awaiting-approval",
                           "status": "active" },
              "continue": null,
              "pending": { "phase": "map", "gate": { "approval": true, "checklist": null }, "skill": "shade-kit-map" },
              "forward": null,
              "reopen": null
            }
            """);

        bytesAfterPendingCalls
            .Should().Equal(pendingBytes);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public async Task PhaseNext_NewFeature_ReturnsNoContinueButForwardToFirstWorkPhase()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var next = await RunJsonAtAsync(1, "phase", "next", "--json");

        // Assert
        next.ExitCode
            .Should().Be(0);

        next.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "current": { "featureId": "gh-142-login-sso", "phase": "new", "state": "in-progress",
                           "status": "active" },
              "continue": null,
              "pending": null,
              "forward": { "phase": "refine", "gate": { "approval": true, "checklist": null },
                           "skill": "shade-kit-refine" },
              "reopen": []
            }
            """);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC3")]
    public async Task PhaseStart_SkippedPhase_Returns42AndLeavesFeatureFileUnchanged()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        var bytesBefore = await ReadFeatureFileBytesAsync();

        // Act
        var run = await RunJsonAtAsync(1, "phase", "start", "map", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.TransitionNotAllowed, ShadeKitCliErrorCodes.TransitionNotAllowed);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("'map'").And.Contain("'new'");

        (await ReadFeatureFileBytesAsync())
            .Should().Equal(bytesBefore);
    }

    [Theory]
    [Trait("Requirement", "FP04-AC4")]
    [Trait("Requirement", "FP04-AC6")]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task PhaseApproveOrReject_WithoutPendingTransition_Returns43AndLeavesFeatureFileUnchanged(
        string command)
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        var bytesBefore = await ReadFeatureFileBytesAsync();

        // Act
        var run = await RunJsonAtAsync(1, "phase", command, "refine", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.NoPendingTransition, ShadeKitCliErrorCodes.NoPendingTransition);

        (await ReadFeatureFileBytesAsync())
            .Should().Equal(bytesBefore);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC8")]
    public async Task PhaseComplete_OutsideLastPhase_Returns45AndLeavesFeatureFileUnchanged()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("quick");
        await _harness.RunAllOkAsync(["phase", "start", "quick"]);
        var bytesBefore = await ReadFeatureFileBytesAsync();

        // Act
        var run = await RunJsonAtAsync(1, "phase", "complete", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.CompleteNotAllowed, ShadeKitCliErrorCodes.CompleteNotAllowed);

        (await ReadFeatureFileBytesAsync())
            .Should().Equal(bytesBefore);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("skills")]
    public async Task PhaseCommand_PhaseNotInProfile_Returns41ListingProfilePhases(string command)
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        var bytesBefore = await ReadFeatureFileBytesAsync();

        // Act
        var run = await RunJsonAtAsync(1, "phase", command, "diagnose", "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.PhaseNotInProfile, ShadeKitCliErrorCodes.PhaseNotInProfile);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("diagnose").And.Contain("refine");

        (await ReadFeatureFileBytesAsync())
            .Should().Equal(bytesBefore);
    }

    [Theory]
    [InlineData("next")]
    [InlineData("start", "refine")]
    [InlineData("complete")]
    public async Task PhaseCommand_NoCurrentFeature_Returns37(params string[] arguments)
    {
        // Arrange
        await _harness.InitAsync();

        // Act
        var run = await RunJsonAtAsync(1, ["phase", .. arguments, "--json"]);

        // Assert
        AssertError(run, ShadeKitCliExitCodes.NoCurrentFeature, ShadeKitCliErrorCodes.NoCurrentFeature);

        run.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("shade-kit feature set");
    }

    [Theory]
    [InlineData("start")]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("skills")]
    public async Task PhaseCommand_WithoutPhaseArgument_Returns2WithInvalidArgumentsEnvelope(string command)
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        var bytesBefore = await ReadFeatureFileBytesAsync();

        // Act
        var run = await RunJsonAtAsync(1, "phase", command, "--json");

        // Assert
        AssertError(run, ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments);

        (await ReadFeatureFileBytesAsync())
            .Should().Equal(bytesBefore);
    }

    [Theory]
    [Trait("Requirement", "FP04-AC6")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PhaseReject_EmptyOrWhitespaceNote_RecordsRejectWithoutNote(string note)
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync(["phase", "start", "refine"]);

        // Act
        var run = await RunJsonAtAsync(1, "phase", "reject", "refine", "--note", note, "--json");

        // Assert
        run.ExitCode
            .Should().Be(0);

        run.Root.GetProperty("data").GetProperty("note").ValueKind
            .Should().Be(JsonValueKind.Null);

        using var featureFile = await ReadFeatureFileAsync();

        featureFile.RootElement.GetProperty("history")[1].ShouldBeJson(
            """
            { "at": "2026-10-26T09:01:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
              "action": "reject", "from": "new", "to": "refine" }
            """);
    }

    [Fact]
    public async Task PlainMode_PhaseCommandsWithShortNoteOption_WriteExpectedLinesAndRecordNote()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("bugfix");

        // Act
        var start = await RunAtAsync(1, "phase", "start", "diagnose");
        var reject = await RunAtAsync(2, "phase", "reject", "diagnose", "-n", "  Needs a repro  ");
        var skills = await RunAtAsync(3, "phase", "skills", "plan");
        var next = await RunAtAsync(4, "phase", "next");

        // Assert
        new[] { start.ExitCode, reject.ExitCode, skills.ExitCode, next.ExitCode }
            .Should().AllBeEquivalentTo(0);

        GetLines(start.Output)
            .Should().ContainInConsecutiveOrder(
                $"Feature: {FeatureId} (bugfix)",
                "Outcome: transition-requested",
                "Transition: new -> diagnose (forward)",
                "Current phase: new (awaiting-approval)",
                "Gate: diagnose (approval: required, checklist: none)",
                "Phase skill: shade-kit-diagnose",
                "General skills: shade-kit-interview, shade-kit-research");

        // The console wraps the long hint line, so its parts are folded again.
        FoldWhitespace(start.Output)
            .Should().EndWith(
                " Approve with 'shade-kit phase approve diagnose' or reject with 'shade-kit phase reject diagnose'. ");

        GetLines(reject.Output)
            .Should().ContainInConsecutiveOrder(
                $"Feature: {FeatureId}",
                "Rejected: new -> diagnose (forward)",
                "Current phase: new (in-progress)",
                "Note: Needs a repro");

        GetLines(skills.Output)
            .Should().ContainInConsecutiveOrder(
                $"Feature: {FeatureId} (bugfix)",
                "Phase: plan",
                "Phase skill: shade-kit-plan",
                "General skills: shade-kit-interview");

        FoldWhitespace(next.Output)
            .Should().Be(
                $"Feature: {FeatureId} (active) Current phase: new (in-progress) " +
                "Forward: diagnose (skill: shade-kit-diagnose, approval: required, checklist: none) Reopen: none ");

        using var featureFile = await ReadFeatureFileAsync();

        featureFile.RootElement.GetProperty("history")[1].GetProperty("note").GetString()
            .Should().Be("Needs a repro", "the note is trimmed");
    }

    [Fact]
    [Trait("Requirement", "FP05-AC2")]
    public async Task FeatureProfile_StartPlan_CopiesDefinitionOfReadyWithFeatureIdAndReturnsItsPath()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync(StartAndApproveRefineAndMap);

        // Act
        var startPlan = await _harness.RunJsonAsync("phase", "start", "plan", "--json");

        // Assert
        startPlan.ExitCode
            .Should().Be(0);

        startPlan.Root.GetProperty("data").GetProperty("gate").ShouldBeJson(
            """
            { "phase": "plan", "approval": true, "checklist": "checks/definition-of-ready.md", "checklistCreated": true }
            """);

        var template = await File.ReadAllTextAsync(_harness.GetKitPath("templates", "checks", "definition-of-ready.md"),
            TestContext.Current.CancellationToken);
        var copy = await File.ReadAllTextAsync(_harness.GetIssueFeaturePath("checks", "definition-of-ready.md"),
            TestContext.Current.CancellationToken);

        copy
            .Should().StartWith($"# Definition of Ready: {FeatureId}")
            .And.NotContain("{{feature-id}}")
            .And.Be(template.Replace("{{feature-id}}", FeatureId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlainMode_StartPlanTwice_WritesChecklistCopyCreatedThenAlreadyExistsAfterGateLine()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync(StartAndApproveRefineAndMap);

        // Act
        var first = await _harness.RunAsync("phase", "start", "plan");
        var second = await _harness.RunAsync("phase", "start", "plan");

        // Assert
        new[] { first.ExitCode, second.ExitCode }
            .Should().AllBeEquivalentTo(0);

        GetLines(first.Output)
            .Should().ContainInConsecutiveOrder(
                "Gate: plan (approval: required, checklist: checks/definition-of-ready.md)",
                "Checklist copy: created");

        GetLines(second.Output)
            .Should().ContainInConsecutiveOrder(
                "Outcome: transition-pending",
                "Transition: map -> plan (forward)",
                "Current phase: map (awaiting-approval)",
                "Gate: plan (approval: required, checklist: checks/definition-of-ready.md)",
                "Checklist copy: already exists");
    }

    [Fact]
    [Trait("Requirement", "FP05-AC3")]
    public async Task FeatureProfile_ReopenAndStartPlanAgain_KeepsFilledChecklistCopyByteForByte()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync([.. StartAndApproveRefineAndMap, ["phase", "start", "plan"], ["phase", "approve", "plan"]]);
        var copyPath = _harness.GetIssueFeaturePath("checks", "definition-of-ready.md");
        await File.AppendAllTextAsync(copyPath, "| DOR-01 | filled by the agent | pass | spec.md |\n",
            TestContext.Current.CancellationToken);
        var filledBytes = await File.ReadAllBytesAsync(copyPath, TestContext.Current.CancellationToken);

        // Act
        var reopen = await _harness.RunJsonAsync("phase", "start", "map", "--json");
        var approveReopen = await _harness.RunJsonAsync("phase", "approve", "map", "--json");
        var startPlanAgain = await _harness.RunJsonAsync("phase", "start", "plan", "--json");
        var startPlanRepeated = await _harness.RunJsonAsync("phase", "start", "plan", "--json");

        // Assert
        new[] { reopen.ExitCode, approveReopen.ExitCode, startPlanAgain.ExitCode, startPlanRepeated.ExitCode }
            .Should().AllBeEquivalentTo(0);

        reopen.Root.GetProperty("data").GetProperty("transition").GetProperty("kind").GetString()
            .Should().Be("reopen");

        startPlanAgain.Root.GetProperty("data").GetProperty("gate").GetProperty("checklist").GetString()
            .Should().Be("checks/definition-of-ready.md");

        startPlanAgain.Root.GetProperty("data").GetProperty("gate").GetProperty("checklistCreated").GetBoolean()
            .Should().BeFalse("the filled copy already exists");

        startPlanRepeated.Root.GetProperty("data").GetProperty("outcome").GetString()
            .Should().Be("transition-pending");

        startPlanRepeated.Root.GetProperty("data").GetProperty("gate").GetProperty("checklistCreated").GetBoolean()
            .Should().BeFalse("the filled copy already exists");

        (await File.ReadAllBytesAsync(copyPath, TestContext.Current.CancellationToken))
            .Should().Equal(filledBytes);
    }

    [Fact]
    [Trait("Requirement", "FP05-AC4")]
    public async Task FeatureProfile_TeamGateWithApprovalOff_PerformsTransitionAndRecordsApprovalOff()
    {
        // Arrange
        await _harness.InitAsync();
        await WriteGatesAsync("""{ "feature": { "refine": { "approval": false } } }""");
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var startRefine = await RunJsonAtAsync(1, "phase", "start", "refine", "--json");

        // Assert
        startRefine.ExitCode
            .Should().Be(0);

        var data = startRefine.Root.GetProperty("data");

        data.GetProperty("outcome").GetString()
            .Should().Be("transition-performed");

        data.GetProperty("currentPhase").ShouldBeJson("""{ "name": "refine", "state": "in-progress" }""");

        data.GetProperty("gate").ShouldBeJson(
            """{ "phase": "refine", "approval": false, "checklist": null, "checklistCreated": null }""");

        using var featureFile = await ReadFeatureFileAsync();

        featureFile.RootElement.GetProperty("history").ShouldBeJson(
            """
            [
              { "at": "2026-10-26T09:01:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "approve", "from": "new", "to": "refine", "note": "approval off" }
            ]
            """);
    }

    [Fact]
    [Trait("Requirement", "FP05-AC5")]
    public async Task FeatureProfile_TransitionWithoutChecklist_ReturnsNullChecklistAndCopiesNothing()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync(["phase", "start", "refine"], ["phase", "approve", "refine"]);

        // Act
        var startMap = await _harness.RunJsonAsync("phase", "start", "map", "--json");

        // Assert
        startMap.ExitCode
            .Should().Be(0);

        startMap.Root.GetProperty("data").GetProperty("gate").ShouldBeJson(
            """{ "phase": "map", "approval": true, "checklist": null, "checklistCreated": null }""");

        var checksPath = _harness.GetIssueFeaturePath("checks");

        (Directory.Exists(checksPath) ? Directory.EnumerateFileSystemEntries(checksPath) : [])
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP05-AC6")]
    public async Task FeatureProfile_GatesWithoutEntryForTransition_RequiresApproval()
    {
        // Arrange
        await _harness.InitAsync();
        await WriteGatesAsync("""{ "feature": { "map": { "approval": false } } }""");
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var startRefine = await _harness.RunJsonAsync("phase", "start", "refine", "--json");

        // Assert
        startRefine.ExitCode
            .Should().Be(0);

        var data = startRefine.Root.GetProperty("data");

        data.GetProperty("outcome").GetString()
            .Should().Be("transition-requested");

        data.GetProperty("currentPhase").ShouldBeJson("""{ "name": "new", "state": "awaiting-approval" }""");

        data.GetProperty("gate").ShouldBeJson(
            """{ "phase": "refine", "approval": true, "checklist": null, "checklistCreated": null }""");
    }

    [Fact]
    [Trait("Requirement", "FP05-AC8")]
    public async Task FeatureProfile_TeamChecklistTemplate_CopiesItIntoFeatureFolderAndReturnsItsPath()
    {
        // Arrange
        await _harness.InitAsync();
        await WriteGatesAsync("""{ "feature": { "refine": { "approval": true, "checklist": "checks/refine.md" } } }""");
        Directory.CreateDirectory(_harness.GetKitPath("templates", "checks"));
        await File.WriteAllTextAsync(_harness.GetKitPath("templates", "checks", "refine.md"),
            "# Refine check: {{feature-id}}\n\n- [ ] Request understood\n", TestContext.Current.CancellationToken);
        await _harness.CreateIssueFeatureAsync("feature");

        // Act
        var startRefine = await _harness.RunJsonAsync("phase", "start", "refine", "--json");

        // Assert
        startRefine.ExitCode
            .Should().Be(0);

        startRefine.Root.GetProperty("data").GetProperty("gate").ShouldBeJson(
            """{ "phase": "refine", "approval": true, "checklist": "checks/refine.md", "checklistCreated": true }""");

        // The copy is written with the resolved line ending; with the git fake and no .editorconfig in the
        // repository that is the line ending of the operating system.
        var newLine = Environment.NewLine;

        (await File.ReadAllTextAsync(_harness.GetIssueFeaturePath("checks", "refine.md"), TestContext.Current.CancellationToken))
            .Should().Be($"# Refine check: {FeatureId}{newLine}{newLine}- [ ] Request understood{newLine}");
    }

    [Fact]
    public async Task PhaseStart_ChecklistTemplateMissing_Returns47AndLeavesFeatureFileUnchanged()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync(StartAndApproveRefineAndMap);
        File.Delete(_harness.GetKitPath("templates", "checks", "definition-of-ready.md"));
        var featureBytes = await ReadFeatureFileBytesAsync();

        // Act
        var startPlan = await _harness.RunJsonAsync("phase", "start", "plan", "--json");

        // Assert
        AssertError(startPlan, 47, "checklist-template-not-found");

        startPlan.Root.GetProperty("data").ValueKind
            .Should().Be(JsonValueKind.Null);

        (await ReadFeatureFileBytesAsync())
            .Should().Equal(featureBytes);

        File.Exists(_harness.GetIssueFeaturePath("checks", "definition-of-ready.md"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task PhaseStart_FileAtChecksDirectoryOfFeature_Returns17AndLeavesFeatureFileUnchanged()
    {
        // Arrange
        await _harness.InitAsync();
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync(StartAndApproveRefineAndMap);
        await File.WriteAllTextAsync(_harness.GetIssueFeaturePath("checks"), "stray\n", TestContext.Current.CancellationToken);
        var featureBytes = await ReadFeatureFileBytesAsync();

        // Act
        var startPlan = await _harness.RunJsonAsync("phase", "start", "plan", "--json");

        // Assert
        AssertError(startPlan, 17, "kit-file-write-failed");

        startPlan.Root.GetProperty("error").GetProperty("message").GetString()
            .Should().StartWith($"'.shadekit/features/{FeatureId}/checks/definition-of-ready.md' cannot be written.");

        (await ReadFeatureFileBytesAsync())
            .Should().Equal(featureBytes);

        (await File.ReadAllTextAsync(_harness.GetIssueFeaturePath("checks"), TestContext.Current.CancellationToken))
            .Should().Be("stray\n");
    }

    [Fact]
    public async Task PhaseStart_TwoGatesShareChecklist_WarnsAndStartsLaterGateWithoutChecklist()
    {
        // Arrange
        await _harness.InitAsync();
        await WriteGatesAsync(
            """{ "feature": { "refine": { "checklist": "checks/definition-of-ready.md" }, "map": { "checklist": "checks/definition-of-ready.md" } } }""");
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync(["phase", "start", "refine"], ["phase", "approve", "refine"]);

        // Act
        var startMap = await _harness.RunJsonAsync("phase", "start", "map", "--json");

        // Assert
        startMap.ExitCode
            .Should().Be(0);

        FeatureCommandTestHelper.GetWarningCodes(startMap.Root)
            .Should().Contain("gate-entry-ignored");

        startMap.Root.GetProperty("data").GetProperty("gate").GetProperty("checklist").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task PhaseStartAndNext_GatesFileWithoutProfiles_WarnDefaultsUsedAndCopyDefinitionOfReady()
    {
        // Arrange
        await _harness.InitAsync();
        await File.WriteAllTextAsync(_harness.GetKitPath("gates.json"), TestGatesJson.HeaderOnly,
            TestContext.Current.CancellationToken);
        await _harness.CreateIssueFeatureAsync("feature");
        await _harness.RunAllOkAsync(StartAndApproveRefineAndMap);

        // Act
        var startPlan = await _harness.RunJsonAsync("phase", "start", "plan", "--json");
        var next = await _harness.RunJsonAsync("phase", "next", "--json");

        // Assert
        startPlan.ExitCode
            .Should().Be(0);

        // The fake git fails `git check-attr`, so the two writes (the copy and feature.json) report the attributes
        // warning, once, after the warning of reading gates.json.
        FeatureCommandTestHelper.GetWarningCodes(startPlan.Root)
            .Should().Equal("gates-defaults-used", "git-attributes-unavailable");

        FeatureCommandTestHelper.GetWarningCodes(next.Root)
            .Should().Equal("gates-defaults-used");

        startPlan.Root.GetProperty("data").GetProperty("gate").GetProperty("checklist").GetString()
            .Should().Be("checks/definition-of-ready.md");

        File.Exists(_harness.GetIssueFeaturePath("checks", "definition-of-ready.md"))
            .Should().BeTrue();
    }

    public void Dispose()
    {
        _harness.Dispose();
    }

    private static string[][] StartAndApproveRefineAndMap =>
    [
        ["phase", "start", "refine"], ["phase", "approve", "refine"],
        ["phase", "start", "map"], ["phase", "approve", "map"]
    ];

    /// <summary>
    /// Replaces <c>.shadekit/gates.json</c> with a file of format version 1 whose <c>profiles</c> field is the
    /// specified JSON object.
    /// </summary>
    private Task WriteGatesAsync(string profiles)
    {
        return File.WriteAllTextAsync(_harness.GetKitPath("gates.json"), TestGatesJson.WithProfiles(profiles),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Sets the clock to the specified minute after the start time, then runs the command in plain mode.
    /// </summary>
    private Task<(int ExitCode, string Output, string Error)> RunAtAsync(int minute, params string[] args)
    {
        _timeProvider.SetUtcNow(StartUtc.AddMinutes(minute));

        return _harness.RunAsync(args);
    }

    /// <summary>
    /// Sets the clock to the specified minute after the start time, then runs the command in JSON mode.
    /// </summary>
    private Task<(int ExitCode, JsonElement Root)> RunJsonAtAsync(int minute, params string[] args)
    {
        _timeProvider.SetUtcNow(StartUtc.AddMinutes(minute));

        return _harness.RunJsonAsync(args);
    }

    private Task<byte[]> ReadFeatureFileBytesAsync()
    {
        return File.ReadAllBytesAsync(_harness.GetIssueFeaturePath("feature.json"), TestContext.Current.CancellationToken);
    }

    private async Task<JsonDocument> ReadFeatureFileAsync()
    {
        return JsonDocument.Parse(await ReadFeatureFileBytesAsync());
    }

}
