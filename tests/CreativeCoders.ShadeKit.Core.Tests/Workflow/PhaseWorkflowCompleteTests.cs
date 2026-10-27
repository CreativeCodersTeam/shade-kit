using System.Runtime.Versioning;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.PhaseWorkflowTestContext;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow;

public sealed class PhaseWorkflowCompleteTests : IDisposable
{
    private const string OtherFeatureId = "gh-7-checkout";

    private static readonly WorkflowProfile Quick = BuiltInWorkflowProfiles.Quick;

    private readonly PhaseWorkflowTestContext _context = new PhaseWorkflowTestContext();

    public void Dispose()
    {
        _context.Dispose();
    }

    private static string FinishInProgress => PhaseFields(Quick, "finish", null,
        ["approved", "approved", "in-progress"]);

    [Fact]
    [Trait("Requirement", "FP04-AC8")]
    public async Task CompleteAsync_LastPhaseInProgress_SetsFeatureDoneAndAppendsCompleteEntry()
    {
        // Arrange
        _context.WriteFeature(FinishInProgress, WorkflowProfileNames.Quick);

        // Act
        var result = await _context.CreateWorkflow().CompleteAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeEquivalentTo(new PhaseCompleteResult(FeatureId, WorkflowPhaseNames.Finish,
                FeatureStatus.Done, false, []));

        var document = await _context.ReadFeatureAsync();

        document.Status
            .Should().Be(FeatureStatus.Done);

        document.Phases![WorkflowPhaseNames.Finish]
            .Should().Be(PhaseState.Approved);

        document.History
            .Should().ContainSingle().Which
            .Should().BeEquivalentTo(new PhaseHistoryEntry
            {
                At = LocalNow,
                By = new PhaseHistoryActor { Name = UserName, Email = UserEmail },
                Action = PhaseHistoryAction.Complete,
                From = WorkflowPhaseNames.Finish
            });
    }

    [Fact]
    [Trait("Requirement", "FP04-AC8")]
    public async Task CompleteAsync_OverrideSelectsFeature_DeletesOverrideAndReportsIt()
    {
        // Arrange
        _context.WriteFeature(FinishInProgress, WorkflowProfileNames.Quick);
        _context.WriteOverride(FeatureId);

        // Act
        var result = await _context.CreateWorkflow().CompleteAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.OverrideCleared
            .Should().BeTrue();

        _context.OverrideExists()
            .Should().BeFalse();
    }

    [Theory]
    [Trait("Requirement", "FP04-AC8")]
    [InlineData(OtherFeatureId)]
    [InlineData("{ not json")]
    public async Task CompleteAsync_OverrideSelectsOtherFeatureOrIsUnreadable_KeepsOverride(string overrideContent)
    {
        // Arrange
        _context.WriteFeature(FinishInProgress, WorkflowProfileNames.Quick);

        if (overrideContent == OtherFeatureId)
        {
            _context.WriteOverride(OtherFeatureId);
        }
        else
        {
            _context.WriteOverrideContent(overrideContent);
        }

        var overrideBytes = _context.Repository.ReadFileBytes(".shadekit", "features", "current-feature.json");

        // Act
        var result = await _context.CreateWorkflow().CompleteAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        result.OverrideCleared
            .Should().BeFalse();

        _context.Repository.ReadFileBytes(".shadekit", "features", "current-feature.json")
            .Should().Equal(overrideBytes);

        (await _context.ReadFeatureAsync()).Status
            .Should().Be(FeatureStatus.Done);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC8")]
    public async Task CompleteAsync_NotInLastPhase_ThrowsCompleteNotAllowedAndWritesNothing()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Quick, "quick", null, ["approved", "in-progress", "pending"]),
            WorkflowProfileNames.Quick);
        _context.WriteOverride(FeatureId);
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().CompleteAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<PhaseCompleteNotAllowedException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyIdentityNotRead();
    }

    [Fact]
    public async Task CompleteAsync_GitIdentityMissing_ThrowsAndLeavesFeatureAndOverrideUnchanged()
    {
        // Arrange
        _context.WriteFeature(FinishInProgress, WorkflowProfileNames.Quick);
        _context.WriteOverride(FeatureId);
        _context.SetIdentityMissing();
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        var act = () => _context.CreateWorkflow().CompleteAsync(_context.CreateStore(),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GitIdentityNotConfiguredException>();

        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task CompleteAsync_OverrideCannotBeDeleted_ThrowsAndKeepsFeatureDone()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a folder read-only.");
        _context.WriteFeature(FinishInProgress, WorkflowProfileNames.Quick);
        _context.WriteOverride(FeatureId);
        var workflow = _context.CreateWorkflow();

        Exception? thrown;

        using (UnixFileModeScope.MakeReadOnly(Path.Combine(_context.RepositoryRootPath, ".shadekit", "features")))
        {
            // Act
            thrown = await Record.ExceptionAsync(() => workflow.CompleteAsync(_context.CreateStore(),
                TestContext.Current.CancellationToken));
        }

        // Assert
        thrown
            .Should().Match(x => x is IOException || x is UnauthorizedAccessException);

        (await _context.ReadFeatureAsync()).Status
            .Should().Be(FeatureStatus.Done);

        _context.OverrideExists()
            .Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_StoreNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _context.CreateWorkflow().CompleteAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
