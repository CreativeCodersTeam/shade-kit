using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

/// <summary>
/// Runs the engine on synthetic profiles to prove that it knows no profile or phase by name.
/// </summary>
public class PhaseEngineGenericityTests
{
    [Fact]
    public void FullRun_FourPhaseSyntheticProfile_FollowsProfileDefinition()
    {
        // Arrange
        var engine = new PhaseEngine();
        var gates = new Dictionary<string, TransitionGate> { ["gamma"] = new(false, null) };
        var history = new List<PhaseHistoryRecord>();
        var state = engine.CreateInitialState(Greek, FeatureId, FeatureFilePath);

        // Act
        state = Apply(engine.Start(state, "beta", gates).Change!, history);
        state = Apply(engine.Approve(state, "beta").Change, history);

        var performed = engine.Start(state, "gamma", gates);
        state = Apply(performed.Change!, history);

        state = Apply(engine.Start(state, "delta", gates).Change!, history);
        state = Apply(engine.Reject(state, "delta", " not yet ").Change, history);

        var gammaInProgress = state;
        var reopenBeta = () => engine.Start(gammaInProgress, "beta", gates);

        state = Apply(engine.Start(state, "alpha", gates).Change!, history);
        state = Apply(engine.Approve(state, "alpha").Change, history);
        state = Apply(engine.Start(state, "beta", gates).Change!, history);
        state = Apply(engine.Approve(state, "beta").Change, history);
        state = Apply(engine.Start(state, "gamma", gates).Change!, history);
        state = Apply(engine.Start(state, "delta", gates).Change!, history);
        state = Apply(engine.Approve(state, "delta").Change, history);

        var completed = engine.Complete(state);
        state = Apply(completed.Change, history);

        // Assert
        performed.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPerformed);

        performed.Skills
            .Should().BeEquivalentTo(new PhaseSkills("skill-gamma", ["general-one", "general-two"]));

        reopenBeta.Should().Throw<PhaseTransitionNotAllowedException>()
            .Which.Reason.Should().Be(PhaseTransitionRefusalReason.PhaseCannotBeReopened);

        completed.Phase
            .Should().Be("delta");

        state.Status
            .Should().Be(FeatureStatus.Done);

        history
            .Should().Equal(
                new PhaseHistoryRecord(PhaseHistoryAction.Start, "alpha", "beta", null),
                new PhaseHistoryRecord(PhaseHistoryAction.Approve, "alpha", "beta", null),
                new PhaseHistoryRecord(PhaseHistoryAction.Approve, "beta", "gamma", "approval off"),
                new PhaseHistoryRecord(PhaseHistoryAction.Start, "gamma", "delta", null),
                new PhaseHistoryRecord(PhaseHistoryAction.Reject, "gamma", "delta", "not yet"),
                new PhaseHistoryRecord(PhaseHistoryAction.Reopen, "gamma", "alpha", null),
                new PhaseHistoryRecord(PhaseHistoryAction.Approve, "gamma", "alpha", null),
                new PhaseHistoryRecord(PhaseHistoryAction.Start, "alpha", "beta", null),
                new PhaseHistoryRecord(PhaseHistoryAction.Approve, "alpha", "beta", null),
                new PhaseHistoryRecord(PhaseHistoryAction.Approve, "beta", "gamma", "approval off"),
                new PhaseHistoryRecord(PhaseHistoryAction.Start, "gamma", "delta", null),
                new PhaseHistoryRecord(PhaseHistoryAction.Approve, "gamma", "delta", null),
                new PhaseHistoryRecord(PhaseHistoryAction.Complete, "delta", null, null));
    }

    [Fact]
    public void FullRun_TwoPhaseProfile_MovesForwardAndCompletes()
    {
        // Arrange
        var engine = new PhaseEngine();
        var state = engine.CreateInitialState(TwoPhases, FeatureId, FeatureFilePath);

        // Act
        var started = engine.Start(state, "second", NoGates);
        var approved = engine.Approve(started.State, "second");
        var nextSteps = engine.GetNext(approved.Change.State, NoGates);
        var completed = engine.Complete(approved.Change.State);

        // Assert
        started.Skills
            .Should().BeEquivalentTo(new PhaseSkills("do-second", ["helper"]));

        nextSteps.Forward
            .Should().BeNull();

        nextSteps.Reopen
            .Should().Equal(new PhaseStep("first", "do-first"));

        completed.Change.State.Phases
            .Should().Equal(Phases(("first", PhaseState.Approved), ("second", PhaseState.Approved)));
    }

    [Fact]
    public void FullRun_OnePhaseProfile_StartsCurrentPhaseAndCompletes()
    {
        // Arrange
        var engine = new PhaseEngine();
        var state = engine.CreateInitialState(OnePhase, FeatureId, FeatureFilePath);

        // Act
        var started = engine.Start(state, "only", NoGates);
        var nextSteps = engine.GetNext(state, NoGates);
        var completed = engine.Complete(state);
        var doneSteps = engine.GetNext(completed.Change.State, NoGates);

        // Assert
        started.Outcome
            .Should().Be(PhaseStartOutcome.AlreadyInProgress);

        nextSteps.Should().Be(new PhaseNextSteps(state, new PhaseStep("only", "do-only"), null, null,
            nextSteps.Reopen));

        nextSteps.Reopen
            .Should().BeEmpty();

        completed.Change.State.Status
            .Should().Be(FeatureStatus.Done);

        doneSteps.Continue
            .Should().BeNull();
    }

    [Fact]
    public void ReadState_PhaseRemovedFromProfileAfterStateWasWritten_KeepsEntryAndIgnoresIt()
    {
        // Arrange
        var engine = new PhaseEngine();
        var stored = CreateStored("second", Phases(
            ("first", PhaseState.Approved),
            ("dropped", PhaseState.Approved),
            ("second", PhaseState.InProgress)));

        // Act
        var state = engine.ReadState(TwoPhases, stored);
        var steps = engine.GetNext(state, NoGates);
        var completed = engine.Complete(state);

        // Assert
        steps.Reopen
            .Should().Equal(new PhaseStep("first", "do-first"));

        completed.Change.State.Phases
            .Should().Equal(Phases(
                ("first", PhaseState.Approved),
                ("second", PhaseState.Approved),
                ("dropped", PhaseState.Approved)));
    }

    [Fact]
    public void Start_DroppedPhaseKeptInState_ThrowsPhaseNotInProfile()
    {
        // Arrange
        var engine = new PhaseEngine();
        var state = engine.ReadState(TwoPhases, CreateStored("first", Phases(
            ("first", PhaseState.InProgress),
            ("dropped", PhaseState.Pending))));

        // Act
        var act = () => engine.Start(state, "dropped", NoGates);

        // Assert
        act.Should().Throw<PhaseNotInProfileException>()
            .Which.ProfilePhases.Should().Equal("first", "second");
    }

    // Records the history and persists the new state the way the workflow does: the state is read back from its
    // stored fields, so every state the engine produces must be valid for ReadState.
    private static FeaturePhaseState Apply(PhaseStateChange change, List<PhaseHistoryRecord> history)
    {
        history.Add(change.History);

        var state = change.State;
        var stored = new StoredPhaseState(state.FeatureId, state.FeatureFilePath, state.Status, state.CurrentPhase,
            new OrderedDictionary<string, PhaseState>(state.Phases), state.PendingTransition);

        var reread = new PhaseEngine().ReadState(state.Profile, stored);

        reread.Should().BeEquivalentTo(state);

        return reread;
    }
}
