using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
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

    private const string DefinitionOfReady = GatesConfig.DefinitionOfReadyChecklist;

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
    [Trait("Requirement", "FP05-AC2")]
    public async Task StartAsync_TransitionRequestedWithChecklist_CopiesChecklistIntoFolderOfResolvedFeature()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress, recordedId: "gh-142-login");
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(true, DefinitionOfReady));
        var store = _context.CreateStore();

        // Act
        var result = await _context.CreateWorkflow().StartAsync(store, WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gate
            .Should().Be(new PhaseGate(WorkflowPhaseNames.Map, new TransitionGate(true, DefinitionOfReady)));

        A.CallTo(() => _context.ChecklistWriter.EnsureCopyAsync(store, FeatureId, DefinitionOfReady,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    [Trait("Requirement", "FP05-AC4")]
    public async Task StartAsync_TransitionPerformedWithChecklist_CopiesChecklistBeforeFeatureFileIsWritten()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(false, DefinitionOfReady));
        var featureTextBefore = _context.ReadFeatureText();
        string? featureTextAtCopy = null;
        A.CallTo(() => _context.ChecklistWriter.EnsureCopyAsync(A<IKitStore>._, A<string>._, A<string>._,
                A<CancellationToken>._))
            .Invokes(() => featureTextAtCopy = _context.ReadFeatureText())
            .Returns(new FeatureChecklistCopyResult(Created: true, Warnings: []));

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPerformed);

        featureTextAtCopy
            .Should().Be(featureTextBefore);

        var document = await _context.ReadFeatureAsync();

        document.CurrentPhase
            .Should().Be(WorkflowPhaseNames.Map);

        A.CallTo(() => _context.ChecklistWriter.EnsureCopyAsync(A<IKitStore>._, FeatureId, DefinitionOfReady,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    [Trait("Requirement", "FP05-AC3")]
    public async Task StartAsync_TransitionPendingWithChecklist_CopiesChecklistWithoutReadingIdentityOrWritingFeature()
    {
        // Arrange
        _context.WriteFeature(RefineAwaitingMap);
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(true, DefinitionOfReady));
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPending);

        A.CallTo(() => _context.ChecklistWriter.EnsureCopyAsync(A<IKitStore>._, FeatureId, DefinitionOfReady,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Theory]
    [Trait("Requirement", "FP05-AC5")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartAsync_GateWithoutChecklist_CopiesNothing(bool approval)
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(approval, null));

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gate
            .Should().Be(new PhaseGate(WorkflowPhaseNames.Map, new TransitionGate(approval, null)));

        _context.VerifyChecklistNotCopied();
    }

    [Fact]
    public async Task StartAsync_ReopenOfPhaseWithChecklist_CopiesNothing()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, "plan", null,
            ["approved", "approved", "approved", "in-progress", "pending", "pending", "pending"]));
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(true, DefinitionOfReady));

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Transition!.Kind
            .Should().Be(PhaseTransitionKind.Reopen);

        _context.VerifyChecklistNotCopied();
    }

    [Fact]
    public async Task StartAsync_PendingReopenOfPhaseWithChecklist_CopiesNothing()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, "plan", "map",
            ["approved", "approved", "approved", "awaiting-approval", "pending", "pending", "pending"]));
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(true, DefinitionOfReady));

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPending);

        result.Gate
            .Should().Be(new PhaseGate(WorkflowPhaseNames.Map, TransitionGate.Default));

        _context.VerifyChecklistNotCopied();
    }

    [Theory]
    [InlineData(WorkflowPhaseNames.Refine, null, PhaseStartOutcome.AlreadyInProgress)]
    [InlineData(WorkflowPhaseNames.Refine, WorkflowPhaseNames.Map, PhaseStartOutcome.ResumePending)]
    public async Task StartAsync_CurrentPhaseRequestedAgain_CopiesNothing(string phase, string? pendingTransition,
        PhaseStartOutcome expectedOutcome)
    {
        // Arrange
        var state = pendingTransition is null ? "in-progress" : "awaiting-approval";
        _context.WriteFeature(PhaseFields(Feature, "refine", pendingTransition,
            ["approved", state, "pending", "pending", "pending", "pending", "pending"]));
        _context.SetGate(WorkflowPhaseNames.Refine, new TransitionGate(true, DefinitionOfReady));
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(true, DefinitionOfReady));

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), phase,
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(expectedOutcome);

        _context.VerifyChecklistNotCopied();
    }

    [Fact]
    public async Task StartAsync_EngineRefusesTransitionWithChecklist_CopiesNothing()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        _context.SetGate(WorkflowPhaseNames.Plan, new TransitionGate(true, DefinitionOfReady));

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Plan,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<PhaseTransitionNotAllowedException>();

        _context.VerifyChecklistNotCopied();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartAsync_GitIdentityMissingForTransitionWithChecklist_CopiesNothingAndWritesNothing(
        bool approval)
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(approval, DefinitionOfReady));
        _context.SetIdentityMissing();
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitIdentityNotConfiguredException>();

        _context.VerifyChecklistNotCopied();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task StartAsync_WarningsOfGatesReadCopyAndWrite_ReturnsFormatWarningsFirstAndEachOnce()
    {
        // Arrange
        var mismatchWarning = new CurrentFeatureMismatchWarning("gh-7-checkout", FeatureId);
        var gatesFormatWarning = new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/gates.json", 1, 2);
        var defaultsWarning = new GatesDefaultsUsedWarning(IsFileMissing: false);
        var copyWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        var writeWarning = new EditorConfigInvalidWarning("/repo/.editorconfig", "Broken glob.");
        _context.SelectFeature(FeatureId, mismatchWarning);
        _context.SetGatesConfigWarnings(defaultsWarning, gatesFormatWarning);
        _context.WriteWarnings = [copyWarning, writeWarning];
        _context.WriteFeature(RefineInProgress);
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(true, DefinitionOfReady));
        A.CallTo(() => _context.ChecklistWriter.EnsureCopyAsync(A<IKitStore>._, A<string>._, A<string>._,
                A<CancellationToken>._))
            .Returns(new FeatureChecklistCopyResult(Created: true, Warnings: [copyWarning]));

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(gatesFormatWarning, mismatchWarning, defaultsWarning, copyWarning, writeWarning);
    }

    [Fact]
    public async Task StartAsync_TransitionPendingWithCopyWarnings_ReturnsGatesAndCopyWarningsFormatWarningsFirst()
    {
        // Arrange
        var gatesFormatWarning = new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/gates.json", 1, 2);
        var defaultsWarning = new GatesDefaultsUsedWarning(IsFileMissing: false);
        var copyWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        _context.SetGatesConfigWarnings(defaultsWarning, gatesFormatWarning);
        _context.WriteFeature(RefineAwaitingMap);
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(true, DefinitionOfReady));
        A.CallTo(() => _context.ChecklistWriter.EnsureCopyAsync(A<IKitStore>._, A<string>._, A<string>._,
                A<CancellationToken>._))
            .Returns(new FeatureChecklistCopyResult(Created: true, Warnings: [copyWarning, defaultsWarning]));

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPending);

        result.Warnings
            .Should().Equal(gatesFormatWarning, defaultsWarning, copyWarning);
    }

    [Fact]
    public async Task StartAsync_TransitionPendingWithApprovalSwitchedOff_StaysPendingAndCopiesCurrentChecklist()
    {
        // Arrange
        _context.WriteFeature(RefineAwaitingMap);
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(false, DefinitionOfReady));
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        result.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPending);

        A.CallTo(() => _context.ChecklistWriter.EnsureCopyAsync(A<IKitStore>._, FeatureId, DefinitionOfReady,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartAsync_TransitionWithChecklist_PassesStoreAndTokenToReaderWriterAndIdentity(bool approval)
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        _context.SetGate(WorkflowPhaseNames.Map, new TransitionGate(approval, DefinitionOfReady));
        var store = _context.CreateStore();
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        // Act
        await _context.CreateWorkflow().StartAsync(store, WorkflowPhaseNames.Map, token);

        // Assert
        A.CallTo(() => _context.GatesConfigReader.ReadAsync(store, token))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _context.ChecklistWriter.EnsureCopyAsync(store, FeatureId, DefinitionOfReady, token))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _context.GitRepositoryInfo.GetIdentityAsync(_context.RepositoryRootPath, token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartAsync_AnyRequest_AsksGatesConfigReaderAfterEveryGateOfProvider()
    {
        // Arrange
        _context.WriteFeature(RefineInProgress);
        var store = _context.CreateStore();

        // Act
        await _context.CreateWorkflow().StartAsync(store, WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _context.GateProvider.GetGateAsync(store, A<WorkflowProfile>._, WorkflowPhaseNames.Finish,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _context.GatesConfigReader.ReadAsync(store, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task StartAsync_NoChange_ReturnsWarningsOfGatesRead()
    {
        // Arrange
        var defaultsWarning = new GatesDefaultsUsedWarning(IsFileMissing: true);
        _context.SetGatesConfigWarnings(defaultsWarning);
        _context.WriteFeature(RefineInProgress);

        // Act
        var result = await _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(defaultsWarning);
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
