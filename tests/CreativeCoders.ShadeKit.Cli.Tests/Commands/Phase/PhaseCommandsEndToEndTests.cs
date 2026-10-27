using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Trackers;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase;

/// <summary>
/// Runs the <c>shade-kit phase</c> commands through
/// <see cref="Program.RunAsync(string[], TextWriter, TextWriter, Action{IServiceCollection})" /> against a real temporary
/// repository; git is answered by a fake <see cref="IGitCommandRunner" />, the tracker issue reader is a fake, so no
/// test starts <c>git</c> or <c>gh</c>, and the clock is a <see cref="FakeTimeProvider" />, so the history timestamps
/// are exact.
/// </summary>
[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed partial class PhaseCommandsEndToEndTests : IDisposable
{
    private const string IssueUrl = "https://github.com/acme/shop/issues/142";

    private const string FeatureId = "gh-142-login-sso";

    private const string UserName = "Jane Doe";

    private const string UserEmail = "jane@example.com";

    private static readonly string[] CurrentBranchArguments = ["symbolic-ref", "--short", "-q", "HEAD"];

    private static readonly DateTimeOffset StartUtc = new(2026, 10, 26, 7, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo UtcPlusTwo =
        TimeZoneInfo.CreateCustomTimeZone("Test UTC+02", TimeSpan.FromHours(2), "Test UTC+02", "Test UTC+02");

    private readonly CurrentDirectoryScope _currentDirectory = new CurrentDirectoryScope();

    private readonly IGitCommandRunner _gitCommandRunner = A.Fake<IGitCommandRunner>();

    private readonly ITrackerIssueReader _trackerIssueReader = A.Fake<ITrackerIssueReader>();

    private readonly TempKitDirectory _tempDirectory = new TempKitDirectory("shade-kit-phase-tests-");

    private readonly FakeTimeProvider _timeProvider = new FakeTimeProvider(StartUtc);

    private string? _branch = "main";

    public PhaseCommandsEndToEndTests()
    {
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ReturnsLazily((string _, IReadOnlyList<string> arguments, CancellationToken _) =>
                Task.FromResult(AnswerGit(arguments)));

        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Returns(new TrackerIssue("Login with SSO", "Users sign in with the company account.", []));

        _timeProvider.SetLocalTimeZone(UtcPlusTwo);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC11")]
    [Trait("Requirement", "FP04-AC2")]
    [Trait("Requirement", "FP04-AC5")]
    [Trait("Requirement", "FP04-AC8")]
    [Trait("Requirement", "FP04-AC14")]
    public async Task QuickProfile_FullRun_CompletesFeatureRecordsHistoryAndClearsOverride()
    {
        // Arrange
        await InitAsync();
        _branch = $"feature/{FeatureId}";
        await CreateFeatureAsync("quick");

        // Act
        var startQuick = await RunJsonAtAsync(1, "phase", "start", "quick", "--json");
        var approveQuick = await RunJsonAtAsync(2, "phase", "approve", "quick", "--json");
        var startFinish = await RunJsonAtAsync(3, "phase", "start", "finish", "--json");
        var approveFinish = await RunJsonAtAsync(4, "phase", "approve", "finish", "--json");
        var nextInFinish = await RunJsonAtAsync(5, "phase", "next", "--json");
        var complete = await RunJsonAtAsync(6, "phase", "complete", "--json");
        var nextWhenDone = await RunJsonAtAsync(7, "phase", "next", "--json");
        var current = await RunJsonAtAsync(8, "feature", "current", "--json");

        // Assert
        new[]
            {
                startQuick.ExitCode, approveQuick.ExitCode, startFinish.ExitCode, approveFinish.ExitCode,
                nextInFinish.ExitCode, complete.ExitCode, nextWhenDone.ExitCode, current.ExitCode
            }
            .Should().AllBeEquivalentTo(0);

        startQuick.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "quick", "outcome": "transition-requested",
              "transition": { "kind": "forward", "from": "new", "to": "quick" },
              "currentPhase": { "name": "new", "state": "awaiting-approval" },
              "gate": { "phase": "quick", "approval": true, "checklist": null },
              "skills": { "phaseSkill": "shade-kit-quick", "generalSkills": ["shade-kit-interview", "shade-kit-review"] }
            }
            """);

        approveQuick.Root.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso",
              "transition": { "kind": "forward", "from": "new", "to": "quick" },
              "currentPhase": { "name": "quick", "state": "in-progress" }
            }
            """);

        startFinish.Root.GetProperty("data").GetProperty("skills").ShouldBeJson(
            """{ "phaseSkill": "shade-kit-finish", "generalSkills": ["shade-kit-report"] }""");

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

        File.Exists(GetCurrentFeatureFilePath())
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
                "action": "start", "from": "new", "to": "quick" },
              { "at": "2026-10-26T09:02:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "approve", "from": "new", "to": "quick" },
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
        await InitAsync();
        _branch = $"feature/{FeatureId}";
        await CreateFeatureAsync("quick");
        await RunAllOkAsync(["phase", "start", "quick"], ["phase", "approve", "quick"], ["phase", "start", "finish"],
            ["phase", "approve", "finish"], ["phase", "complete"]);
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
        await InitAsync();
        await CreateFeatureAsync("feature");
        var requestBytes = await File.ReadAllBytesAsync(GetFeaturePath("request.md"),
            TestContext.Current.CancellationToken);

        // Act
        var startRefine = await RunJsonAtAsync(1, "phase", "start", "refine", "--json");
        var rejectRefine = await RunJsonAtAsync(2, "phase", "reject", "refine", "--note", "DOR-04 failed", "--json");
        await RunJsonAtAsync(3, "phase", "start", "refine", "--json");
        await RunJsonAtAsync(4, "phase", "approve", "refine", "--json");
        await File.WriteAllTextAsync(GetFeaturePath("spec.md"), "# Spec\n", TestContext.Current.CancellationToken);
        await RunJsonAtAsync(5, "phase", "start", "map", "--json");
        await RunJsonAtAsync(6, "phase", "approve", "map", "--json");
        await File.WriteAllTextAsync(GetFeaturePath("map.md"), "# Map\n", TestContext.Current.CancellationToken);
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
              "gate": { "phase": "refine", "approval": true, "checklist": null },
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

        shown.GetProperty("phases").ShouldBeJson(
            """
            { "new": "approved", "refine": "in-progress", "map": "pending", "plan": "pending",
              "implement": "pending", "verify": "pending", "finish": "pending" }
            """);

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

        (await File.ReadAllTextAsync(GetFeaturePath("spec.md"), TestContext.Current.CancellationToken))
            .Should().Be("# Spec\n");

        (await File.ReadAllTextAsync(GetFeaturePath("map.md"), TestContext.Current.CancellationToken))
            .Should().Be("# Map\n");

        (await File.ReadAllBytesAsync(GetFeaturePath("request.md"), TestContext.Current.CancellationToken))
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
        await InitAsync();
        await CreateFeatureAsync("feature");
        await RunAllOkAsync(["phase", "start", "refine"], ["phase", "approve", "refine"]);
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
    [Trait("Requirement", "FP04-AC3")]
    public async Task PhaseStart_SkippedPhase_Returns42AndLeavesFeatureFileUnchanged()
    {
        // Arrange
        await InitAsync();
        await CreateFeatureAsync("feature");
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
        await InitAsync();
        await CreateFeatureAsync("feature");
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
        await InitAsync();
        await CreateFeatureAsync("quick");
        await RunAllOkAsync(["phase", "start", "quick"], ["phase", "approve", "quick"]);
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
        await InitAsync();
        await CreateFeatureAsync("feature");
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
        await InitAsync();

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
        await InitAsync();
        await CreateFeatureAsync("feature");
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
        await InitAsync();
        await CreateFeatureAsync("feature");
        await RunAllOkAsync(["phase", "start", "refine"]);

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
        await InitAsync();
        await CreateFeatureAsync("bugfix");

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
                "Continue: new (skill: shade-kit-new) " +
                "Forward: diagnose (skill: shade-kit-diagnose, approval: required, checklist: none) Reopen: none ");

        using var featureFile = await ReadFeatureFileAsync();

        featureFile.RootElement.GetProperty("history")[1].GetProperty("note").GetString()
            .Should().Be("Needs a repro", "the note is trimmed");
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

        run.Root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(errorCode);
    }

    private ExternalProcessResult AnswerGit(IReadOnlyList<string> arguments)
    {
        if (arguments.SequenceEqual(CurrentBranchArguments))
        {
            return _branch is null
                ? new ExternalProcessResult(1, string.Empty, string.Empty)
                : new ExternalProcessResult(0, _branch + "\n", string.Empty);
        }

        return arguments switch
        {
            ["config", "user.name"] => new ExternalProcessResult(0, UserName + "\n", string.Empty),
            ["config", "user.email"] => new ExternalProcessResult(0, UserEmail + "\n", string.Empty),
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

    /// <summary>
    /// Sets the clock to the specified minute after the start time, then runs the command in plain mode.
    /// </summary>
    private Task<(int ExitCode, string Output, string Error)> RunAtAsync(int minute, params string[] args)
    {
        _timeProvider.SetUtcNow(StartUtc.AddMinutes(minute));

        return RunAsync(args);
    }

    /// <summary>
    /// Sets the clock to the specified minute after the start time, then runs the command in JSON mode.
    /// </summary>
    private Task<(int ExitCode, JsonElement Root)> RunJsonAtAsync(int minute, params string[] args)
    {
        _timeProvider.SetUtcNow(StartUtc.AddMinutes(minute));

        return RunJsonAsync(args);
    }

    private async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(args, output, error, x => x
            .AddSingleton(_gitCommandRunner)
            .AddSingleton(_trackerIssueReader)
            .AddSingleton<TimeProvider>(_timeProvider));

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

    private string GetCurrentFeatureFilePath()
    {
        return Path.Combine(_tempDirectory.RootPath, ".shadekit", "features", "current-feature.json");
    }

    private Task<byte[]> ReadFeatureFileBytesAsync()
    {
        return File.ReadAllBytesAsync(GetFeaturePath("feature.json"), TestContext.Current.CancellationToken);
    }

    private async Task<JsonDocument> ReadFeatureFileAsync()
    {
        return JsonDocument.Parse(await ReadFeatureFileBytesAsync());
    }

    private static string[] GetLines(string output)
    {
        return [.. output.Split('\n').Select(x => x.TrimEnd('\r'))];
    }

    private static string FoldWhitespace(string text)
    {
        // The console wraps long lines, so line breaks are folded back into single spaces.
        return WhitespaceRegex().Replace(text, " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
