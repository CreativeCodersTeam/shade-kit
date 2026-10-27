using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Features;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using FakeItEasy;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.PhaseWorkflowTestContext;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow;

/// <summary>
/// Tests the failure paths and the interplay of several calls of <see cref="PhaseWorkflow" />.
/// </summary>
public sealed class PhaseWorkflowRobustnessTests : IDisposable
{
    private static readonly WorkflowProfile Quick = BuiltInWorkflowProfiles.Quick;

    private readonly PhaseWorkflowTestContext _context = new PhaseWorkflowTestContext();

    public void Dispose()
    {
        _context.Dispose();
    }

    private static string FinishInProgress => PhaseFields(Quick, "finish", null,
        ["approved", "approved", "in-progress"]);

    private static string QuickAwaitingFinish => PhaseFields(Quick, "quick", "finish",
        ["approved", "awaiting-approval", "pending"]);

    public static TheoryData<string, Func<IPhaseWorkflow, IKitStore, Task>> ChangingCallsOnPendingFeature =>
        new()
        {
            { "approve", (workflow, store) => workflow.ApproveAsync(store, WorkflowPhaseNames.Finish,
                TestContext.Current.CancellationToken) },
            { "reject", (workflow, store) => workflow.RejectAsync(store, WorkflowPhaseNames.Finish, null,
                TestContext.Current.CancellationToken) }
        };

    [Fact]
    [Trait("Requirement", "FP04-AC8")]
    public async Task CompleteAsync_FeatureFileWriteFails_ThrowsAndKeepsOverride()
    {
        // Arrange
        _context.WriteFeature(FinishInProgress, WorkflowProfileNames.Quick);
        _context.WriteOverride(FeatureId);
        var snapshot = _context.Repository.TakeSnapshot();
        var failure = new IOException("Disk full.");

        // Act
        var act = () => _context.CreateWorkflow().CompleteAsync(_context.CreateStoreFailingFeatureWrite(failure),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IOException>()).Which
            .Should().BeSameAs(failure);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [MemberData(nameof(ChangingCallsOnPendingFeature))]
    public async Task ChangingCall_OutdatedFeatureFile_ThrowsUpgradeNeededAndWritesNothing(string name,
        Func<IPhaseWorkflow, IKitStore, Task> call)
    {
        // Arrange
        _ = name;
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Feature);
        _context.WriteFeature(QuickAwaitingFinish, WorkflowProfileNames.Quick);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => call(_context.CreateWorkflow(), _context.CreateStore());

        // Assert
        await act.Should().ThrowAsync<UpgradeNeededException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task CompleteAsync_OutdatedFeatureFile_ThrowsUpgradeNeededAndWritesNothing()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Feature);
        _context.WriteFeature(FinishInProgress, WorkflowProfileNames.Quick);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().CompleteAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UpgradeNeededException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [MemberData(nameof(ChangingCallsOnPendingFeature))]
    public async Task ChangingCall_DoneFeature_ThrowsFeatureDoneAndReadsNoIdentity(string name,
        Func<IPhaseWorkflow, IKitStore, Task> call)
    {
        // Arrange
        _ = name;
        _context.WriteFeature(PhaseFields(Quick, "finish", null, ["approved", "approved", "approved"]),
            WorkflowProfileNames.Quick, "done");
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => call(_context.CreateWorkflow(), _context.CreateStore());

        // Assert
        await act.Should().ThrowAsync<FeatureDoneException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    public async Task CompleteAsync_DoneFeatureStillSelectedByOverride_ThrowsFeatureDoneAndKeepsOverride()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Quick, "finish", null, ["approved", "approved", "approved"]),
            WorkflowProfileNames.Quick, "done");
        _context.WriteOverride(FeatureId);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().CompleteAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<FeatureDoneException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    public async Task StartAsync_GateProviderFails_ThrowsItsExceptionAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Quick, "quick", null, ["approved", "in-progress", "pending"]),
            WorkflowProfileNames.Quick);
        var failure = new InvalidKitFileException(".shadekit/gates.json", "Broken.");
        A.CallTo(() => _context.GateProvider.GetGateAsync(A<IKitStore>._, A<WorkflowProfile>._, A<string>._,
                A<CancellationToken>._))
            .ThrowsAsync(failure);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Finish,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>()).Which
            .Should().BeSameAs(failure);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    public async Task StartAsync_CanceledToken_ThrowsOperationCanceledAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Quick, "quick", null, ["approved", "in-progress", "pending"]),
            WorkflowProfileNames.Quick);
        var snapshot = _context.Repository.TakeSnapshot();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Finish,
            cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task ApproveAsync_PendingTransition_ReadsIdentityOfRepositoryRoot()
    {
        // Arrange
        _context.WriteFeature(QuickAwaitingFinish, WorkflowProfileNames.Quick);

        // Act
        await _context.CreateWorkflow().ApproveAsync(_context.CreateStore(), WorkflowPhaseNames.Finish,
            TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _context.GitRepositoryInfo.GetIdentityAsync(_context.RepositoryRootPath,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetSkillsAsync_OutdatedFeatureFile_ReturnsSkillsWithUpgradeNeededWarning()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Feature);
        _context.WriteFeature(string.Empty, WorkflowProfileNames.Quick);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateWorkflow().GetSkillsAsync(_context.CreateStore(), WorkflowPhaseNames.Quick,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, FeatureFilePath, 1, 2));

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task RejectAsync_NullNote_WritesEntryWithoutNote()
    {
        // Arrange
        _context.WriteFeature(QuickAwaitingFinish, WorkflowProfileNames.Quick);

        // Act
        var result = await _context.CreateWorkflow().RejectAsync(_context.CreateStore(), WorkflowPhaseNames.Finish,
            null, TestContext.Current.CancellationToken);

        // Assert
        result.Note
            .Should().BeNull();

        _context.ReadFeatureText()
            .Should().NotContain("\"note\"");
    }

    [Fact]
    public async Task PhaseCalls_WholeQuickRunWithReject_RecordHistoryInOrderAndEndDone()
    {
        // Arrange
        _context.WriteFeature(string.Empty, WorkflowProfileNames.Quick);
        var workflow = _context.CreateWorkflow();
        var store = _context.CreateStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await workflow.StartAsync(store, WorkflowPhaseNames.Quick, cancellationToken);
        await workflow.ApproveAsync(store, WorkflowPhaseNames.Quick, cancellationToken);
        await workflow.StartAsync(store, WorkflowPhaseNames.Finish, cancellationToken);
        await workflow.RejectAsync(store, WorkflowPhaseNames.Finish, "Tests missing.", cancellationToken);
        await workflow.StartAsync(store, WorkflowPhaseNames.Finish, cancellationToken);
        await workflow.ApproveAsync(store, WorkflowPhaseNames.Finish, cancellationToken);
        var result = await workflow.CompleteAsync(store, cancellationToken);

        // Assert
        result.Status
            .Should().Be(FeatureStatus.Done);

        var document = await _context.ReadFeatureAsync();

        document.History!.Select(x => (x.Action, x.From, x.To, x.Note))
            .Should().Equal(
                (PhaseHistoryAction.Start, "new", "quick", null),
                (PhaseHistoryAction.Approve, "new", "quick", null),
                (PhaseHistoryAction.Start, "quick", "finish", null),
                (PhaseHistoryAction.Reject, "quick", "finish", "Tests missing."),
                (PhaseHistoryAction.Start, "quick", "finish", null),
                (PhaseHistoryAction.Approve, "quick", "finish", null),
                (PhaseHistoryAction.Complete, "finish", null, null));

        _context.ReadPhaseNamesInFileOrder()
            .Should().Equal(WorkflowPhaseNames.New, WorkflowPhaseNames.Quick, WorkflowPhaseNames.Finish);

        document.Phases!.Values
            .Should().AllBeEquivalentTo(PhaseState.Approved);

        document.PendingTransition
            .Should().BeNull();
    }
}
