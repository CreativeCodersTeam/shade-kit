using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseEngineRejectTests
{
    [Theory]
    [Trait("Requirement", "FP04-AC6")]
    [MemberData(nameof(BuiltInProfilePhasesWithNext), MemberType = typeof(PhaseEngineTestData))]
    public void Reject_PendingForwardTransition_ReturnsCurrentPhaseToInProgressAndRecordsNote(string profileName,
        int currentIndex)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var current = profile.Phases[currentIndex].Name;
        var next = profile.Phases[currentIndex + 1].Name;
        var state = CreateAwaitingState(profile, current, next);

        // Act
        var decision = new PhaseEngine().Reject(state, next, "DOR-04 failed");

        // Assert
        decision.Rejected
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Forward, current, next));

        decision.Note
            .Should().Be("DOR-04 failed");

        decision.Change.History
            .Should().Be(new PhaseHistoryRecord(PhaseHistoryAction.Reject, current, next, "DOR-04 failed"));

        decision.Change.State.CurrentPhase
            .Should().Be(current);

        decision.Change.State.CurrentPhaseState
            .Should().Be(PhaseState.InProgress);

        decision.Change.State.PendingTransition
            .Should().BeNull();

        decision.Change.State.GetPhaseState(next)
            .Should().Be(PhaseState.Pending);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC6")]
    public void Reject_PendingReopen_ReturnsCurrentPhaseToInProgressAndKeepsOtherPhases()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "delta", "beta");

        // Act
        var decision = new PhaseEngine().Reject(state, "beta", null);

        // Assert
        decision.Rejected
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Reopen, "delta", "beta"));

        decision.Change.History
            .Should().Be(new PhaseHistoryRecord(PhaseHistoryAction.Reject, "delta", "beta", null));

        decision.Change.State.Phases
            .Should().Equal(Phases(
                ("alpha", PhaseState.Approved),
                ("beta", PhaseState.Approved),
                ("gamma", PhaseState.Approved),
                ("delta", PhaseState.InProgress)));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" \r\n\t ", null)]
    [InlineData("  DOR-04 failed  ", "DOR-04 failed")]
    [InlineData("\nDOR-04 failed\nDOR-07 failed\n", "DOR-04 failed\nDOR-07 failed")]
    [InlineData("line one\r\n  line two", "line one\r\n  line two")]
    public void Reject_Note_IsTrimmedAndDroppedWhenEmpty(string? note, string? expectedNote)
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "alpha", "beta");

        // Act
        var decision = new PhaseEngine().Reject(state, "beta", note);

        // Assert
        decision.Note
            .Should().Be(expectedNote);

        decision.Change.History.Note
            .Should().Be(expectedNote);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC6")]
    public void Reject_WithoutPendingTransition_ThrowsNoPendingTransition()
    {
        // Arrange
        var state = CreateState(Greek, "alpha");

        // Act
        var act = () => new PhaseEngine().Reject(state, "beta", "note");

        // Assert
        act.Should().Throw<NoPendingTransitionException>()
            .Which.Should().BeEquivalentTo(new { FeatureId, Phase = "beta", PendingTransition = (string?)null });
    }

    [Fact]
    [Trait("Requirement", "FP04-AC6")]
    public void Reject_PhaseOtherThanPendingTarget_ThrowsNoPendingTransitionNamingPendingTarget()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "beta", "gamma");

        // Act
        var act = () => new PhaseEngine().Reject(state, "delta", null);

        // Assert
        act.Should().Throw<NoPendingTransitionException>()
            .Which.PendingTransition.Should().Be("gamma");
    }

    [Fact]
    public void Reject_PhaseNotInProfile_ThrowsPhaseNotInProfile()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "alpha", "beta");

        // Act
        var act = () => new PhaseEngine().Reject(state, "omega", null);

        // Assert
        act.Should().Throw<PhaseNotInProfileException>();
    }

    [Fact]
    public void Reject_DoneFeature_ThrowsFeatureDone()
    {
        // Arrange
        var state = CreateDoneState(Greek);

        // Act
        var act = () => new PhaseEngine().Reject(state, "omega", null);

        // Assert
        act.Should().Throw<FeatureDoneException>();
    }

    [Fact]
    public void Reject_NullArguments_ThrowArgumentNullException()
    {
        // Arrange
        var engine = new PhaseEngine();

        // Act
        var withoutState = () => engine.Reject(null!, "beta", null);
        var withoutPhase = () => engine.Reject(CreateAwaitingState(Greek, "alpha", "beta"), null!, null);

        // Assert
        withoutState.Should().Throw<ArgumentNullException>();
        withoutPhase.Should().Throw<ArgumentNullException>();
    }
}
