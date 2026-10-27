using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseEngineApproveTests
{
    [Theory]
    [MemberData(nameof(BuiltInProfilePhasesWithNext), MemberType = typeof(PhaseEngineTestData))]
    public void Approve_PendingForwardTransition_ApprovesCurrentAndMovesToTarget(string profileName,
        int currentIndex)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var current = profile.Phases[currentIndex].Name;
        var next = profile.Phases[currentIndex + 1].Name;
        var state = CreateAwaitingState(profile, current, next);

        // Act
        var decision = new PhaseEngine().Approve(state, next);

        // Assert
        decision.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Forward, current, next));

        decision.Change.History
            .Should().Be(new PhaseHistoryRecord(PhaseHistoryAction.Approve, current, next, null));

        decision.Change.State.CurrentPhase
            .Should().Be(next);

        decision.Change.State.PendingTransition
            .Should().BeNull();

        decision.Change.State.Phases
            .Should().Equal(profile.Phases.Select((x, index) => KeyValuePair.Create(x.Name,
                (index - currentIndex) switch
                {
                    <= 0 => PhaseState.Approved,
                    1 => PhaseState.InProgress,
                    _ => PhaseState.Pending
                })));
    }

    [Fact]
    [Trait("Requirement", "FP04-AC7")]
    public void Approve_PendingReopen_SetsTargetInProgressAndEveryLaterPhasePending()
    {
        // Arrange
        var profile = BuiltInWorkflowProfiles.Feature;
        var state = CreateAwaitingState(profile, WorkflowPhaseNames.Implement, WorkflowPhaseNames.Map);

        // Act
        var decision = new PhaseEngine().Approve(state, WorkflowPhaseNames.Map);

        // Assert
        decision.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Reopen, WorkflowPhaseNames.Implement,
                WorkflowPhaseNames.Map));

        decision.Change.History
            .Should().Be(new PhaseHistoryRecord(PhaseHistoryAction.Approve, WorkflowPhaseNames.Implement,
                WorkflowPhaseNames.Map, null));

        decision.Change.State.CurrentPhase
            .Should().Be(WorkflowPhaseNames.Map);

        decision.Change.State.PendingTransition
            .Should().BeNull();

        decision.Change.State.Phases
            .Should().Equal(Phases(
                (WorkflowPhaseNames.New, PhaseState.Approved),
                (WorkflowPhaseNames.Refine, PhaseState.Approved),
                (WorkflowPhaseNames.Map, PhaseState.InProgress),
                (WorkflowPhaseNames.Plan, PhaseState.Pending),
                (WorkflowPhaseNames.Implement, PhaseState.Pending),
                (WorkflowPhaseNames.Verify, PhaseState.Pending),
                (WorkflowPhaseNames.Finish, PhaseState.Pending)));
    }

    [Fact]
    [Trait("Requirement", "FP04-AC7")]
    public void Approve_PendingReopenWithEntriesOfRemovedPhases_KeepsThoseEntriesUnchanged()
    {
        // Arrange
        var stored = CreateStored("gamma", Phases(
            ("alpha", PhaseState.Approved),
            ("removed", PhaseState.InProgress),
            ("beta", PhaseState.Approved),
            ("gamma", PhaseState.AwaitingApproval)), "alpha");
        var state = new PhaseEngine().ReadState(Greek, stored);

        // Act
        var decision = new PhaseEngine().Approve(state, "alpha");

        // Assert
        decision.Change.State.Phases
            .Should().Equal(Phases(
                ("alpha", PhaseState.InProgress),
                ("beta", PhaseState.Pending),
                ("gamma", PhaseState.Pending),
                ("delta", PhaseState.Pending),
                ("removed", PhaseState.InProgress)));
    }

    [Fact]
    public void Approve_PendingReopenOfPhaseTheProfileNoLongerAllowsToReopen_ThrowsPhaseCannotBeReopened()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "gamma", "beta");

        // Act
        var act = () => new PhaseEngine().Approve(state, "beta");

        // Assert
        act.Should().Throw<PhaseTransitionNotAllowedException>()
            .Which.Should().BeEquivalentTo(new
            {
                Phase = "beta",
                CurrentPhase = "gamma",
                CurrentState = PhaseState.AwaitingApproval,
                PendingTransition = "beta",
                Reason = PhaseTransitionRefusalReason.PhaseCannotBeReopened
            });
    }

    [Theory]
    [Trait("Requirement", "FP04-AC4")]
    [MemberData(nameof(BuiltInProfilePhasesWithNext), MemberType = typeof(PhaseEngineTestData))]
    public void Approve_NextPhaseWithoutPendingTransition_ThrowsNoPendingTransition(string profileName,
        int currentIndex)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var next = profile.Phases[currentIndex + 1].Name;
        var state = CreateState(profile, profile.Phases[currentIndex].Name);

        // Act
        var act = () => new PhaseEngine().Approve(state, next);

        // Assert
        act.Should().Throw<NoPendingTransitionException>()
            .Which.Should().BeEquivalentTo(new { FeatureId, Phase = next, PendingTransition = (string?)null });
    }

    [Theory]
    [Trait("Requirement", "FP04-AC4")]
    [InlineData("alpha")]
    [InlineData("beta")]
    [InlineData("delta")]
    public void Approve_PhaseOtherThanPendingTarget_ThrowsNoPendingTransitionNamingPendingTarget(string phase)
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "beta", "gamma");

        // Act
        var act = () => new PhaseEngine().Approve(state, phase);

        // Assert
        act.Should().Throw<NoPendingTransitionException>()
            .Which.Should().BeEquivalentTo(new { Phase = phase, PendingTransition = "gamma" });
    }

    [Fact]
    public void Approve_PhaseNotInProfile_ThrowsPhaseNotInProfile()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "alpha", "beta");

        // Act
        var act = () => new PhaseEngine().Approve(state, "omega");

        // Assert
        act.Should().Throw<PhaseNotInProfileException>()
            .Which.ProfilePhases.Should().Equal("alpha", "beta", "gamma", "delta");
    }

    [Theory]
    [InlineData("delta")]
    [InlineData("omega")]
    public void Approve_DoneFeature_ThrowsFeatureDone(string phase)
    {
        // Arrange
        var state = CreateDoneState(Greek);

        // Act
        var act = () => new PhaseEngine().Approve(state, phase);

        // Assert
        act.Should().Throw<FeatureDoneException>();
    }

    [Fact]
    public void Approve_NullArguments_ThrowArgumentNullException()
    {
        // Arrange
        var engine = new PhaseEngine();

        // Act
        var withoutState = () => engine.Approve(null!, "beta");
        var withoutPhase = () => engine.Approve(CreateState(Greek, "alpha"), null!);

        // Assert
        withoutState.Should().Throw<ArgumentNullException>();
        withoutPhase.Should().Throw<ArgumentNullException>();
    }
}
