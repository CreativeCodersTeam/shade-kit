using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Tests.Features;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.PhaseWorkflowTestContext;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow;

/// <summary>
/// Tests <see cref="PhaseWorkflow.GetSkillsAsync" />, <see cref="PhaseWorkflow.GetNextAsync" />, and the loading of
/// the current feature that every call shares.
/// </summary>
public sealed class PhaseWorkflowReadTests : IDisposable
{
    private const string Checklist = ".shadekit/checks/dor.md";

    private static readonly WorkflowProfile Feature = BuiltInWorkflowProfiles.Feature;

    private readonly PhaseWorkflowTestContext _context = new PhaseWorkflowTestContext();

    public void Dispose()
    {
        _context.Dispose();
    }

    private static string FeatureDone => PhaseFields(Feature, "finish", null,
        ["approved", "approved", "approved", "approved", "approved", "approved", "approved"]);

    [Theory]
    [Trait("Requirement", "FP04-AC10")]
    [InlineData(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Plan, "shade-kit-plan", new[] { "shade-kit-interview" })]
    [InlineData(WorkflowProfileNames.Quick, WorkflowPhaseNames.Quick, "shade-kit-quick",
        new[] { "shade-kit-interview", "shade-kit-review" })]
    [InlineData(WorkflowProfileNames.Feature, WorkflowPhaseNames.Refine, "shade-kit-refine",
        new[] { "shade-kit-interview", "shade-kit-research" })]
    public async Task GetSkillsAsync_PhaseOfProfile_ReturnsSkillsOfProfilePhaseAndWritesNothing(string profile,
        string phase, string phaseSkill, string[] generalSkills)
    {
        // Arrange
        _context.WriteFeature(string.Empty, profile);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateWorkflow().GetSkillsAsync(_context.CreateStore(), phase,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new PhaseSkillsResult(FeatureId, profile, phase,
                new PhaseSkills(phaseSkill, generalSkills), []));

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    [Trait("Requirement", "FP04-AC10")]
    public async Task GetSkillsAsync_PhaseNotInProfile_ThrowsPhaseNotInProfile()
    {
        // Arrange
        _context.WriteFeature(string.Empty, WorkflowProfileNames.Bugfix);

        // Act
        var act = () => _context.CreateWorkflow().GetSkillsAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<PhaseNotInProfileException>();
    }

    [Fact]
    public async Task GetSkillsAsync_DoneFeature_ReturnsSkills()
    {
        // Arrange
        _context.WriteFeature(FeatureDone, status: "done");

        // Act
        var result = await _context.CreateWorkflow().GetSkillsAsync(_context.CreateStore(), WorkflowPhaseNames.Finish,
            TestContext.Current.CancellationToken);

        // Assert
        result.Skills
            .Should().BeEquivalentTo(new PhaseSkills("shade-kit-finish", ["shade-kit-report"]));
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public async Task GetNextAsync_PhaseInProgress_ReturnsContinueForwardAndReopenAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, "plan", null,
            ["approved", "approved", "approved", "in-progress", "pending", "pending", "pending"]));
        _context.SetGate(WorkflowPhaseNames.Implement, new TransitionGate(true, Checklist));
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateWorkflow().GetNextAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new PhaseNextResult(FeatureId,
                new PhaseSnapshot(WorkflowPhaseNames.Plan, PhaseState.InProgress), FeatureStatus.Active,
                new PhaseStep(WorkflowPhaseNames.Plan, "shade-kit-plan"), null,
                new GatedPhaseStep(WorkflowPhaseNames.Implement, new TransitionGate(true, Checklist),
                    "shade-kit-implement"),
                [
                    new PhaseStep(WorkflowPhaseNames.Refine, "shade-kit-refine"),
                    new PhaseStep(WorkflowPhaseNames.Map, "shade-kit-map")
                ], []));

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public async Task GetNextAsync_PhaseAwaitingApproval_ReturnsOnlyPending()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, "refine", "map",
            ["approved", "awaiting-approval", "pending", "pending", "pending", "pending", "pending"]));

        // Act
        var result = await _context.CreateWorkflow().GetNextAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Pending
            .Should().Be(new GatedPhaseStep(WorkflowPhaseNames.Map, TransitionGate.Default, "shade-kit-map"));

        result.Continue
            .Should().BeNull();

        result.Forward
            .Should().BeNull();

        result.Reopen
            .Should().BeNull();
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public async Task GetNextAsync_LastPhaseInProgress_ReturnsNoForward()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, "finish", null,
            ["approved", "approved", "approved", "approved", "approved", "approved", "in-progress"]));

        // Act
        var result = await _context.CreateWorkflow().GetNextAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Forward
            .Should().BeNull();

        result.Continue
            .Should().Be(new PhaseStep(WorkflowPhaseNames.Finish, "shade-kit-finish"));
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public async Task GetNextAsync_DoneFeature_ReturnsOnlyCurrentDataAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(FeatureDone, status: "done");
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var result = await _context.CreateWorkflow().GetNextAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new PhaseNextResult(FeatureId,
                new PhaseSnapshot(WorkflowPhaseNames.Finish, PhaseState.Approved), FeatureStatus.Done, null, null,
                null, null, []));

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task GetNextAsync_OutdatedFeatureFileAndResolutionWarning_ReturnsFormatWarningFirst()
    {
        // Arrange
        var mismatchWarning = new CurrentFeatureMismatchWarning("gh-7-checkout", FeatureId);
        _context.SelectFeature(FeatureId, mismatchWarning);
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Feature);
        _context.WriteFeature(string.Empty);

        // Act
        var result = await _context.CreateWorkflow().GetNextAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, FeatureFilePath, 1, 2),
                mismatchWarning);
    }

    [Fact]
    public async Task GetNextAsync_NoCurrentFeature_ThrowsNoCurrentFeatureException()
    {
        // Arrange
        _context.SelectFeature(null);

        // Act
        var act = () => _context.CreateWorkflow().GetNextAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NoCurrentFeatureException>();
    }

    [Fact]
    public async Task StartAsync_NoCurrentFeature_ThrowsNoCurrentFeatureExceptionAndReadsNoIdentity()
    {
        // Arrange
        _context.SelectFeature(null);

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NoCurrentFeatureException>();

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    public async Task StartAsync_FeatureFileMissing_ThrowsInvalidKitFileExceptionNamingFile()
    {
        // Arrange
        _context.Repository.CreateDirectory(".shadekit", "features", FeatureId);

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>()).Which.FilePath
            .Should().Be(FeatureFilePath);
    }

    [Fact]
    public async Task StartAsync_UnknownProfileInFeatureFile_ThrowsUnknownProfileExceptionNamingFileAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(string.Empty, "hotfix");
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UnknownProfileException>()).Which;

        exception.Profile
            .Should().Be("hotfix");

        exception.KnownProfiles
            .Should().Equal(WorkflowProfileNames.Feature, WorkflowProfileNames.Bugfix, WorkflowProfileNames.Quick);

        exception.FeatureFilePath
            .Should().Be(FeatureFilePath);

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task GetNextAsync_CurrentPhaseNotInProfile_ThrowsInvalidPhaseStateExceptionNamingFile()
    {
        // Arrange
        _context.WriteFeature(""" "currentPhase": "deploy", "phases": { "new": "approved" } """);

        // Act
        var act = () => _context.CreateWorkflow().GetNextAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidPhaseStateException>()).Which;

        exception.FeatureFilePath
            .Should().Be(FeatureFilePath);

        exception.Field
            .Should().Be("currentPhase");
    }

    [Fact]
    public async Task StartAsync_OutdatedFeatureFile_ThrowsUpgradeNeededAndWritesNothing()
    {
        // Arrange
        _context.Registry = FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Feature);
        _context.WriteFeature(string.Empty);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().StartAsync(_context.CreateStore(), WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UpgradeNeededException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task GetSkillsAsync_PhaseNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _context.CreateWorkflow().GetSkillsAsync(_context.CreateStore(), null!,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetNextAsync_StoreNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _context.CreateWorkflow().GetNextAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
