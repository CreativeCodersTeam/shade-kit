using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.PhaseWorkflowTestContext;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow;

public sealed class PhaseWorkflowApproveRejectTests : IDisposable
{
    private const string EarlierEntry =
        """{ "at": "2026-10-19T09:00:00+02:00", "by": { "name": "John Roe", "email": "john@example.com" }, "action": "start", "from": "refine", "to": "map" }""";

    private static readonly WorkflowProfile Feature = BuiltInWorkflowProfiles.Feature;

    private readonly PhaseWorkflowTestContext _context = new PhaseWorkflowTestContext();

    public void Dispose()
    {
        _context.Dispose();
    }

    private static string RefineAwaitingMap => PhaseFields(Feature, "refine", "map",
        ["approved", "awaiting-approval", "pending", "pending", "pending", "pending", "pending"],
        $"[ {EarlierEntry} ]");

    private static string RefineInProgress => PhaseFields(Feature, "refine", null,
        ["approved", "in-progress", "pending", "pending", "pending", "pending", "pending"]);

    [Fact]
    [Trait("Requirement", "FP04-AC5")]
    public async Task ApproveAsync_PendingTransition_MovesCurrentPhaseAndAppendsApproveEntryWithTimeAndGitUser()
    {
        // Arrange
        _context.WriteFeature(RefineAwaitingMap);

        // Act
        var result = await _context.CreateWorkflow().ApproveAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new PhaseApproveResult(FeatureId,
                new PhaseTransition(PhaseTransitionKind.Forward, WorkflowPhaseNames.Refine, WorkflowPhaseNames.Map),
                new PhaseSnapshot(WorkflowPhaseNames.Map, PhaseState.InProgress), []));

        var document = await _context.ReadFeatureAsync();

        document.CurrentPhase
            .Should().Be(WorkflowPhaseNames.Map);

        document.PendingTransition
            .Should().BeNull();

        document.Phases![WorkflowPhaseNames.Refine]
            .Should().Be(PhaseState.Approved);

        document.Phases[WorkflowPhaseNames.Map]
            .Should().Be(PhaseState.InProgress);

        document.History
            .Should().HaveCount(2);

        document.History[1]
            .Should().BeEquivalentTo(new PhaseHistoryEntry
            {
                At = LocalNow,
                By = new PhaseHistoryActor { Name = UserName, Email = UserEmail },
                Action = PhaseHistoryAction.Approve,
                From = WorkflowPhaseNames.Refine,
                To = WorkflowPhaseNames.Map
            });

        document.History[1].At.Offset
            .Should().Be(TimeSpan.FromHours(2));

        document.History[0].By.Name
            .Should().Be("John Roe");

        _context.ReadFeatureText()
            .Should().Contain($"\"at\": \"{LocalNowText}\"")
            .And.NotContain("pendingTransition");
    }

    [Fact]
    [Trait("Requirement", "FP04-AC4")]
    public async Task ApproveAsync_NoPendingTransition_ThrowsNoPendingTransitionAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().ApproveAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NoPendingTransitionException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    [Trait("Requirement", "FP04-AC7")]
    public async Task ApproveAsync_PendingReopen_ResetsLaterPhasesToPendingAndKeepsOtherFeatureFiles()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, "implement", "refine",
            ["approved", "approved", "approved", "approved", "awaiting-approval", "pending", "pending"]));
        _context.WriteKitFile("# Spec\n", "features", FeatureId, "spec.md");
        _context.WriteKitFile("# Plan\n", "features", FeatureId, "plan.md");
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateWorkflow().ApproveAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        result.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Reopen, WorkflowPhaseNames.Implement,
                WorkflowPhaseNames.Refine));

        var document = await _context.ReadFeatureAsync();

        document.Phases!.Values
            .Should().Equal(PhaseState.Approved, PhaseState.InProgress, PhaseState.Pending, PhaseState.Pending,
                PhaseState.Pending, PhaseState.Pending, PhaseState.Pending);

        document.CurrentPhase
            .Should().Be(WorkflowPhaseNames.Refine);

        var featureFileKey = Path.Combine(".shadekit", "features", FeatureId, "feature.json");

        _context.Repository.TakeSnapshot()
            .Where(x => x.Key != featureFileKey)
            .Should().BeEquivalentTo(snapshot.Where(x => x.Key != featureFileKey));
    }

    [Fact]
    public async Task ApproveAsync_GitIdentityMissing_ThrowsGitIdentityNotConfiguredAndLeavesFileUnchanged()
    {
        // Arrange
        _context.WriteFeature(RefineAwaitingMap);
        _context.SetIdentityMissing();
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().ApproveAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitIdentityNotConfiguredException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC6")]
    public async Task RejectAsync_PendingTransitionWithNote_ReturnsCurrentPhaseToInProgressAndStoresTrimmedNote()
    {
        // Arrange
        _context.WriteFeature(RefineAwaitingMap);

        // Act
        var result = await _context.CreateWorkflow().RejectAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            "  DOR-04 failed\nDOR-07 failed  ", TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new PhaseRejectResult(FeatureId,
                new PhaseTransition(PhaseTransitionKind.Forward, WorkflowPhaseNames.Refine, WorkflowPhaseNames.Map),
                new PhaseSnapshot(WorkflowPhaseNames.Refine, PhaseState.InProgress), "DOR-04 failed\nDOR-07 failed",
                []));

        var document = await _context.ReadFeatureAsync();

        document.PendingTransition
            .Should().BeNull();

        document.Phases![WorkflowPhaseNames.Refine]
            .Should().Be(PhaseState.InProgress);

        document.History![^1]
            .Should().BeEquivalentTo(new PhaseHistoryEntry
            {
                At = LocalNow,
                By = new PhaseHistoryActor { Name = UserName, Email = UserEmail },
                Action = PhaseHistoryAction.Reject,
                From = WorkflowPhaseNames.Refine,
                To = WorkflowPhaseNames.Map,
                Note = "DOR-04 failed\nDOR-07 failed"
            });
    }

    [Fact]
    [Trait("Requirement", "FP04-AC6")]
    public async Task RejectAsync_WithoutNote_WritesEntryWithoutNote()
    {
        // Arrange
        _context.WriteFeature(RefineAwaitingMap);

        // Act
        var result = await _context.CreateWorkflow().RejectAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            "   ", TestContext.Current.CancellationToken);

        // Assert
        result.Note
            .Should().BeNull();

        (await _context.ReadFeatureAsync()).History![^1].Note
            .Should().BeNull();

        _context.ReadFeatureText()
            .Should().NotContain("\"note\"");
    }

    [Fact]
    [Trait("Requirement", "FP04-AC6")]
    public async Task RejectAsync_NoMatchingPendingTransition_ThrowsNoPendingTransitionAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(RefineAwaitingMap);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().RejectAsync(_context.CreateStore(), WorkflowPhaseNames.Plan, null,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NoPendingTransitionException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task ApproveAsync_PhaseNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _context.CreateWorkflow().ApproveAsync(_context.CreateStore(), null!,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RejectAsync_StoreNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _context.CreateWorkflow().RejectAsync(null!, WorkflowPhaseNames.Map, null,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
