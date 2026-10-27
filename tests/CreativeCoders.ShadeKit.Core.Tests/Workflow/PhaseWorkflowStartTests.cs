using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using FakeItEasy;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.PhaseWorkflowTestContext;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow;

public sealed class PhaseWorkflowStartTests : IDisposable
{
    private const string Checklist = ".shadekit/checks/dor.md";

    private static readonly WorkflowProfile Feature = BuiltInWorkflowProfiles.Feature;

    private readonly PhaseWorkflowTestContext _context = new PhaseWorkflowTestContext();

    public void Dispose()
    {
        _context.Dispose();
    }

    private static string RefineInProgress => PhaseFields(Feature, "refine", null,
        ["approved", "in-progress", "pending", "pending", "pending", "pending", "pending"]);

    private static string RefineAwaitingMap => PhaseFields(Feature, "refine", "map",
        ["approved", "awaiting-approval", "pending", "pending", "pending", "pending", "pending"]);

    [Fact]
    [Trait("Requirement", "FP04-AC2")]
    public async Task StartAsync_NextPhaseWhileInProgress_RequestsTransitionAndReturnsGateAndSkillsOfNextPhase()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new PhaseStartResult(FeatureId, WorkflowProfileNames.Feature,
                PhaseStartOutcome.TransitionRequested,
                new PhaseTransition(PhaseTransitionKind.Forward, WorkflowPhaseNames.Refine, WorkflowPhaseNames.Map),
                new PhaseSnapshot(WorkflowPhaseNames.Refine, PhaseState.AwaitingApproval),
                new PhaseGate(WorkflowPhaseNames.Map, TransitionGate.Default),
                new PhaseSkills("shade-kit-map", ["shade-kit-research"]), []));
    }

    [Fact]
    [Trait("Requirement", "FP04-AC2")]
    public async Task StartAsync_NextPhaseWhileInProgress_WritesAwaitingApprovalPendingTransitionAndStartEntry()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);

        // Act
        await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        var document = await _context.ReadFeatureAsync();

        document.CurrentPhase
            .Should().Be(WorkflowPhaseNames.Refine);

        document.PendingTransition
            .Should().Be(WorkflowPhaseNames.Map);

        document.Phases![WorkflowPhaseNames.Refine]
            .Should().Be(PhaseState.AwaitingApproval);

        document.History
            .Should().ContainSingle().Which
            .Should().BeEquivalentTo(new PhaseHistoryEntry
            {
                At = LocalNow,
                By = new PhaseHistoryActor { Name = UserName, Email = UserEmail },
                Action = PhaseHistoryAction.Start,
                From = WorkflowPhaseNames.Refine,
                To = WorkflowPhaseNames.Map
            });

        _context.ReadFeatureText()
            .Should().Contain($"\"at\": \"{LocalNowText}\"");
    }

    [Fact]
    [Trait("Requirement", "FP04-AC11")]
    public async Task StartAsync_CurrentPhaseInProgress_ReturnsAlreadyInProgressWithoutGateAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(PhaseStartOutcome.AlreadyInProgress);

        result.Transition
            .Should().BeNull();

        result.Gate
            .Should().BeNull();

        result.CurrentPhase
            .Should().Be(new PhaseSnapshot(WorkflowPhaseNames.Refine, PhaseState.InProgress));

        result.Skills.PhaseSkill
            .Should().Be("shade-kit-refine");

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    [Trait("Requirement", "FP04-AC12")]
    public async Task StartAsync_TransitionToPhaseAlreadyPending_ReturnsSameGateAgainAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(RefineAwaitingMap);
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(true, Checklist));
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPending);

        result.Gate
            .Should().Be(new PhaseGate(WorkflowPhaseNames.Map, new TransitionGate(true, Checklist)));

        result.CurrentPhase
            .Should().Be(new PhaseSnapshot(WorkflowPhaseNames.Refine, PhaseState.AwaitingApproval));

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    [Trait("Requirement", "FP04-AC13")]
    public async Task StartAsync_CurrentPhaseWhileTransitionAwayIsPending_ReturnsPendingTransitionWithoutGate()
    {
        // Arrange
        _context.WriteFeature(RefineAwaitingMap);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(PhaseStartOutcome.ResumePending);

        result.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Forward, WorkflowPhaseNames.Refine,
                WorkflowPhaseNames.Map));

        result.Gate
            .Should().BeNull();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    public async Task StartAsync_GateWithoutApproval_PerformsTransitionAndWritesApproveEntryWithApprovalOffNote()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(false, null));

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPerformed);

        result.CurrentPhase
            .Should().Be(new PhaseSnapshot(WorkflowPhaseNames.Map, PhaseState.InProgress));

        result.Gate
            .Should().Be(new PhaseGate(WorkflowPhaseNames.Map, new TransitionGate(false, null)));

        var document = await _context.ReadFeatureAsync();

        document.CurrentPhase
            .Should().Be(WorkflowPhaseNames.Map);

        document.PendingTransition
            .Should().BeNull();

        document.Phases![WorkflowPhaseNames.Refine]
            .Should().Be(PhaseState.Approved);

        document.History
            .Should().ContainSingle().Which
            .Should().BeEquivalentTo(new PhaseHistoryEntry
            {
                At = LocalNow,
                By = new PhaseHistoryActor { Name = UserName, Email = UserEmail },
                Action = PhaseHistoryAction.Approve,
                From = WorkflowPhaseNames.Refine,
                To = WorkflowPhaseNames.Map,
                Note = "approval off"
            });
    }

    [Fact]
    public async Task StartAsync_AnyRequest_AsksGateProviderForEveryPhaseOfProfile()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        var store = _context.CreateStore();

        // Act
        await _context.CreateWorkflow().StartAsync(store, WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        foreach (var phase in Feature.Phases)
        {
            A.CallTo(() => _context.GateProvider.GetGateAsync(store,
                    A<WorkflowProfile>.That.Matches(x => x.Name == WorkflowProfileNames.Feature), phase.Name,
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }
    }

    [Fact]
    public async Task StartAsync_FeatureWithoutPhaseFields_WritesAllPhaseFieldsInProfileOrder()
    {
        // Arrange
        _context.WriteFeature(string.Empty, WorkflowProfileNames.Bugfix);

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Diagnose,
            TestContext.Current.CancellationToken);

        // Assert
        result.CurrentPhase
            .Should().Be(new PhaseSnapshot(WorkflowPhaseNames.New, PhaseState.AwaitingApproval));

        _context.ReadPhaseNamesInFileOrder()
            .Should().Equal(BuiltInWorkflowProfiles.Bugfix.Phases.Select(x => x.Name));

        var document = await _context.ReadFeatureAsync();

        document.Phases!.Values
            .Should().Equal(PhaseState.AwaitingApproval, PhaseState.Pending, PhaseState.Pending, PhaseState.Pending,
                PhaseState.Pending, PhaseState.Pending);

        document.CurrentPhase
            .Should().Be(WorkflowPhaseNames.New);

        document.PendingTransition
            .Should().Be(WorkflowPhaseNames.Diagnose);

        document.History
            .Should().ContainSingle().Which.Action
            .Should().Be(PhaseHistoryAction.Start);
    }

    [Fact]
    public async Task StartAsync_EarlierReopenablePhase_RequestsReopenWithDefaultGateAndWritesReopenEntry()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, "plan", null,
            ["approved", "approved", "approved", "in-progress", "pending", "pending", "pending"]));
        _context.SetGate(WorkflowPhaseNames.Refine, new TransitionGate(false, Checklist));

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        result.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Reopen, WorkflowPhaseNames.Plan,
                WorkflowPhaseNames.Refine));

        result.Gate
            .Should().Be(new PhaseGate(WorkflowPhaseNames.Refine, TransitionGate.Default));

        var document = await _context.ReadFeatureAsync();

        document.PendingTransition
            .Should().Be(WorkflowPhaseNames.Refine);

        document.History
            .Should().ContainSingle().Which.Action
            .Should().Be(PhaseHistoryAction.Reopen);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC3")]
    public async Task StartAsync_PhaseThatSkipsPhases_ThrowsTransitionNotAllowedAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Plan,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<PhaseTransitionNotAllowedException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    public async Task StartAsync_GitIdentityMissing_ThrowsGitIdentityNotConfiguredAndLeavesFileUnchanged()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        _context.SetIdentityMissing();
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitIdentityNotConfiguredException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task StartAsync_DoneFeature_ThrowsFeatureDoneAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, "finish", null,
            ["approved", "approved", "approved", "approved", "approved", "approved", "approved"]), status: "done");
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<FeatureDoneException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task StartAsync_WarningsOfResolutionIdCheckAndWrite_ReturnsThemInOrder()
    {
        // Arrange
        var mismatchWarning = new CurrentFeatureMismatchWarning("gh-7-checkout", FeatureId);
        var writeWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        _context.SelectFeature(FeatureId, mismatchWarning);
        _context.WriteWarnings = [writeWarning];
        _context.WriteFeature(RefineInProgress, recordedId: "gh-142-login");

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(mismatchWarning, new FeatureIdMismatchWarning(FeatureId, "gh-142-login"), writeWarning);
    }

    [Fact]
    public async Task StartAsync_StoreNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _context.CreateWorkflow().StartAsync(null!, WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StartAsync_PhaseNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), null!,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
