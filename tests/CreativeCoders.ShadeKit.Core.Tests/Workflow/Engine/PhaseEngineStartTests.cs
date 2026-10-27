using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseEngineStartTests
{
    [Theory]
    [Trait("Requirement", "FP04-AC2")]
    [MemberData(nameof(BuiltInProfilePhasesWithNext), MemberType = typeof(PhaseEngineTestData))]
    public void Start_NextPhaseWhileCurrentInProgress_RequestsTransitionAndReturnsSkillsOfNextPhase(
        string profileName, int currentIndex)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var current = profile.Phases[currentIndex];
        var next = profile.Phases[currentIndex + 1];
        var state = CreateState(profile, current.Name);

        // Act
        var decision = new PhaseEngine().Start(state, next.Name, NoGates);

        // Assert
        decision.Outcome
            .Should().Be(PhaseStartOutcome.TransitionRequested);

        decision.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Forward, current.Name, next.Name));

        decision.Gate
            .Should().Be(TransitionGate.Default);

        decision.Skills
            .Should().BeEquivalentTo(SkillsOf(next));

        decision.HasChanges
            .Should().BeTrue();

        decision.Change!.History
            .Should().Be(new PhaseHistoryRecord(PhaseHistoryAction.Start, current.Name, next.Name, null));

        decision.State
            .Should().BeSameAs(decision.Change.State);

        decision.State.CurrentPhase
            .Should().Be(current.Name);

        decision.State.CurrentPhaseState
            .Should().Be(PhaseState.AwaitingApproval);

        decision.State.PendingTransition
            .Should().Be(next.Name);

        decision.State.GetPhaseState(next.Name)
            .Should().Be(PhaseState.Pending);
    }

    [Fact]
    public void Start_NextPhaseWithGateWithChecklist_ReturnsGateOfNextPhase()
    {
        // Arrange
        var gate = new TransitionGate(true, ".shadekit/checks/dor.md");
        var gates = new Dictionary<string, TransitionGate> { ["beta"] = gate };
        var state = CreateState(Greek, "alpha");

        // Act
        var decision = new PhaseEngine().Start(state, "beta", gates);

        // Assert
        decision.Outcome
            .Should().Be(PhaseStartOutcome.TransitionRequested);

        decision.Gate
            .Should().BeSameAs(gate);
    }

    [Fact]
    public void Start_NextPhaseWithGateValueNull_UsesDefaultGate()
    {
        // Arrange
        var gates = new Dictionary<string, TransitionGate> { ["beta"] = null! };
        var state = CreateState(Greek, "alpha");

        // Act
        var decision = new PhaseEngine().Start(state, "beta", gates);

        // Assert
        decision.Gate
            .Should().Be(TransitionGate.Default);
    }

    [Theory]
    [MemberData(nameof(BuiltInProfilePhasesWithNext), MemberType = typeof(PhaseEngineTestData))]
    public void Start_NextPhaseWithApprovalOff_PerformsTransitionAtOnceWithApproveHistory(string profileName,
        int currentIndex)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var current = profile.Phases[currentIndex].Name;
        var next = profile.Phases[currentIndex + 1];
        var gate = new TransitionGate(false, ".shadekit/checks/gate.md");
        var gates = new Dictionary<string, TransitionGate> { [next.Name] = gate };
        var state = CreateState(profile, current);

        // Act
        var decision = new PhaseEngine().Start(state, next.Name, gates);

        // Assert
        decision.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPerformed);

        decision.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Forward, current, next.Name));

        decision.Gate
            .Should().Be(gate);

        decision.Skills
            .Should().BeEquivalentTo(SkillsOf(next));

        decision.Change!.History
            .Should().Be(new PhaseHistoryRecord(PhaseHistoryAction.Approve, current, next.Name, "approval off"));

        decision.State.CurrentPhase
            .Should().Be(next.Name);

        decision.State.CurrentPhaseState
            .Should().Be(PhaseState.InProgress);

        decision.State.GetPhaseState(current)
            .Should().Be(PhaseState.Approved);

        decision.State.PendingTransition
            .Should().BeNull();
    }

    [Theory]
    [Trait("Requirement", "FP04-AC12")]
    [MemberData(nameof(BuiltInProfilePhasesWithNext), MemberType = typeof(PhaseEngineTestData))]
    public void Start_PendingForwardTarget_ReturnsSameGateAgainWithoutChange(string profileName, int currentIndex)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var current = profile.Phases[currentIndex].Name;
        var next = profile.Phases[currentIndex + 1];
        var gate = new TransitionGate(true, ".shadekit/checks/gate.md");
        var gates = new Dictionary<string, TransitionGate> { [next.Name] = gate };
        var state = CreateAwaitingState(profile, current, next.Name);

        // Act
        var decision = new PhaseEngine().Start(state, next.Name, gates);

        // Assert
        decision.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPending);

        decision.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Forward, current, next.Name));

        decision.Gate
            .Should().Be(gate);

        decision.Skills
            .Should().BeEquivalentTo(SkillsOf(next));

        decision.HasChanges
            .Should().BeFalse();

        decision.Change
            .Should().BeNull();

        decision.State
            .Should().BeSameAs(state);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC12")]
    public void Start_PendingForwardTargetWhoseGateWasSwitchedOff_StillRequiresApprovalWithoutChange()
    {
        // Arrange
        var gates = new Dictionary<string, TransitionGate> { ["gamma"] = new(false, "checklist.md") };
        var state = CreateAwaitingState(Greek, "beta", "gamma");

        // Act
        var decision = new PhaseEngine().Start(state, "gamma", gates);

        // Assert
        decision.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPending);

        decision.Gate
            .Should().Be(new TransitionGate(true, "checklist.md"));

        decision.HasChanges
            .Should().BeFalse();
    }

    [Fact]
    public void Start_PendingReopenOfPhaseTheProfileNoLongerAllowsToReopen_ReturnsTransitionPendingWithoutChange()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "gamma", "beta");

        // Act
        var decision = new PhaseEngine().Start(state, "beta", NoGates);

        // Assert
        decision.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPending);

        decision.HasChanges
            .Should().BeFalse();
    }

    [Fact]
    public void Start_StateChangingRequest_LeavesGivenStateUnchanged()
    {
        // Arrange
        var state = CreateState(Greek, "gamma");
        var phasesBefore = state.Phases.ToArray();

        // Act
        var requested = new PhaseEngine().Start(state, "delta", NoGates);
        var performed = new PhaseEngine().Start(state, "delta",
            new Dictionary<string, TransitionGate> { ["delta"] = new(false, null) });
        var reopened = new PhaseEngine().Start(state, "alpha", NoGates);

        // Assert
        new[] { requested, performed, reopened }
            .Should().AllSatisfy(x => x.HasChanges.Should().BeTrue());

        state.Phases
            .Should().Equal(phasesBefore);

        state.PendingTransition
            .Should().BeNull();

        state.CurrentPhase
            .Should().Be("gamma");
    }

    [Fact]
    [Trait("Requirement", "FP04-AC12")]
    public void Start_PendingReopenTarget_ReturnsDefaultGateWithoutChangeWhateverTheGatesSay()
    {
        // Arrange
        var gates = new Dictionary<string, TransitionGate> { ["alpha"] = new(false, "checklist.md") };
        var state = CreateAwaitingState(Greek, "gamma", "alpha");

        // Act
        var decision = new PhaseEngine().Start(state, "alpha", gates);

        // Assert
        decision.Outcome
            .Should().Be(PhaseStartOutcome.TransitionPending);

        decision.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Reopen, "gamma", "alpha"));

        decision.Gate
            .Should().Be(TransitionGate.Default);

        decision.Skills
            .Should().BeEquivalentTo(SkillsOf(Greek.Phases[0]));

        decision.HasChanges
            .Should().BeFalse();
    }

    [Theory]
    [Trait("Requirement", "FP04-AC11")]
    [MemberData(nameof(BuiltInProfileNames), MemberType = typeof(PhaseEngineTestData))]
    public void Start_CurrentPhaseInProgress_ReturnsAlreadyInProgressWithoutGateAndChange(string profileName)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);

        foreach (var current in profile.Phases)
        {
            var state = CreateState(profile, current.Name);

            // Act
            var decision = new PhaseEngine().Start(state, current.Name, NoGates);

            // Assert
            decision.Outcome
                .Should().Be(PhaseStartOutcome.AlreadyInProgress);

            decision.Transition
                .Should().BeNull();

            decision.Gate
                .Should().BeNull();

            decision.Skills
                .Should().BeEquivalentTo(SkillsOf(current));

            decision.HasChanges
                .Should().BeFalse();

            decision.State
                .Should().BeSameAs(state);
        }
    }

    [Theory]
    [Trait("Requirement", "FP04-AC13")]
    [MemberData(nameof(BuiltInProfilePhasesWithNext), MemberType = typeof(PhaseEngineTestData))]
    public void Start_CurrentPhaseAwaitingApproval_ReturnsResumePendingWithPendingTransitionWithoutGate(
        string profileName, int currentIndex)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var current = profile.Phases[currentIndex];
        var next = profile.Phases[currentIndex + 1].Name;
        var state = CreateAwaitingState(profile, current.Name, next);

        // Act
        var decision = new PhaseEngine().Start(state, current.Name, NoGates);

        // Assert
        decision.Outcome
            .Should().Be(PhaseStartOutcome.ResumePending);

        decision.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Forward, current.Name, next));

        decision.Gate
            .Should().BeNull();

        decision.Skills
            .Should().BeEquivalentTo(SkillsOf(current));

        decision.HasChanges
            .Should().BeFalse();

        decision.State
            .Should().BeSameAs(state);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC13")]
    public void Start_CurrentPhaseAwaitingReopen_ReturnsResumePendingWithReopenTransition()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "delta", "gamma");

        // Act
        var decision = new PhaseEngine().Start(state, "delta", NoGates);

        // Assert
        decision.Outcome
            .Should().Be(PhaseStartOutcome.ResumePending);

        decision.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Reopen, "delta", "gamma"));

        decision.HasChanges
            .Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(BuiltInProfilePhasesWithReopenableEarlierPhase), MemberType = typeof(PhaseEngineTestData))]
    public void Start_EarlierReopenablePhaseWhileCurrentInProgress_RequestsReopenWithDefaultGate(string profileName,
        int currentIndex)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var current = profile.Phases[currentIndex].Name;
        var earlier = profile.Phases.Take(currentIndex).First(x => x.CanReopen);
        var gates = profile.Phases.ToDictionary(x => x.Name, _ => new TransitionGate(false, "checklist.md"));
        var state = CreateState(profile, current);

        // Act
        var decision = new PhaseEngine().Start(state, earlier.Name, gates);

        // Assert
        decision.Outcome
            .Should().Be(PhaseStartOutcome.TransitionRequested);

        decision.Transition
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Reopen, current, earlier.Name));

        decision.Gate
            .Should().Be(TransitionGate.Default);

        decision.Skills
            .Should().BeEquivalentTo(SkillsOf(earlier));

        decision.Change!.History
            .Should().Be(new PhaseHistoryRecord(PhaseHistoryAction.Reopen, current, earlier.Name, null));

        decision.State.CurrentPhase
            .Should().Be(current);

        decision.State.CurrentPhaseState
            .Should().Be(PhaseState.AwaitingApproval);

        decision.State.PendingTransition
            .Should().Be(earlier.Name);

        decision.State.GetPhaseState(earlier.Name)
            .Should().Be(PhaseState.Approved);
    }

    [Theory]
    [MemberData(nameof(BuiltInProfileNames), MemberType = typeof(PhaseEngineTestData))]
    public void Start_FirstPhaseOfBuiltInProfileFromLastPhase_ThrowsPhaseCannotBeReopened(string profileName)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var first = profile.Phases[0].Name;
        var last = profile.Phases[^1].Name;
        var state = CreateState(profile, last);

        // Act
        var act = () => new PhaseEngine().Start(state, first, NoGates);

        // Assert
        act.Should().Throw<PhaseTransitionNotAllowedException>()
            .Which.Should().BeEquivalentTo(new
            {
                FeatureId,
                Phase = first,
                CurrentPhase = last,
                CurrentState = PhaseState.InProgress,
                PendingTransition = (string?)null,
                Reason = PhaseTransitionRefusalReason.PhaseCannotBeReopened
            });
    }

    [Theory]
    [Trait("Requirement", "FP04-AC3")]
    [MemberData(nameof(BuiltInProfileNames), MemberType = typeof(PhaseEngineTestData))]
    public void Start_PhaseAfterNextPhase_ThrowsSkipsPhases(string profileName)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);

        for (var currentIndex = 0; currentIndex + 2 < profile.Phases.Count; currentIndex++)
        {
            var current = profile.Phases[currentIndex].Name;
            var state = CreateState(profile, current);

            foreach (var later in profile.Phases.Skip(currentIndex + 2))
            {
                // Act
                var act = () => new PhaseEngine().Start(state, later.Name, NoGates);

                // Assert
                act.Should().Throw<PhaseTransitionNotAllowedException>()
                    .Which.Should().BeEquivalentTo(new
                    {
                        Phase = later.Name,
                        CurrentPhase = current,
                        CurrentState = PhaseState.InProgress,
                        Reason = PhaseTransitionRefusalReason.SkipsPhases
                    });
            }
        }
    }

    [Fact]
    public void Start_LaterPhaseOtherThanPendingTarget_ThrowsOtherTransitionPending()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "beta", "gamma");

        // Act
        var act = () => new PhaseEngine().Start(state, "delta", NoGates);

        // Assert
        act.Should().Throw<PhaseTransitionNotAllowedException>()
            .Which.Should().BeEquivalentTo(new
            {
                Phase = "delta",
                CurrentPhase = "beta",
                CurrentState = PhaseState.AwaitingApproval,
                PendingTransition = "gamma",
                Reason = PhaseTransitionRefusalReason.OtherTransitionPending
            });
    }

    [Fact]
    public void Start_NextPhaseWhileReopenPending_ThrowsOtherTransitionPending()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "gamma", "alpha");

        // Act
        var act = () => new PhaseEngine().Start(state, "delta", NoGates);

        // Assert
        act.Should().Throw<PhaseTransitionNotAllowedException>()
            .Which.Reason.Should().Be(PhaseTransitionRefusalReason.OtherTransitionPending);
    }

    [Theory]
    [InlineData("alpha")]
    [InlineData("beta")]
    public void Start_EarlierPhaseWhileTransitionPending_ThrowsReopenWhilePending(string earlier)
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "gamma", "delta");

        // Act
        var act = () => new PhaseEngine().Start(state, earlier, NoGates);

        // Assert
        act.Should().Throw<PhaseTransitionNotAllowedException>()
            .Which.Should().BeEquivalentTo(new
            {
                Phase = earlier,
                CurrentPhase = "gamma",
                CurrentState = PhaseState.AwaitingApproval,
                PendingTransition = "delta",
                Reason = PhaseTransitionRefusalReason.ReopenWhilePending
            });
    }

    [Theory]
    [InlineData("refine")]
    [InlineData("Diagnose")]
    [InlineData(" diagnose")]
    [InlineData("")]
    public void Start_PhaseNotInProfile_ThrowsPhaseNotInProfileListingProfilePhases(string phase)
    {
        // Arrange
        var state = CreateState(BuiltInWorkflowProfiles.Bugfix, WorkflowPhaseNames.Diagnose);

        // Act
        var act = () => new PhaseEngine().Start(state, phase, NoGates);

        // Assert
        act.Should().Throw<PhaseNotInProfileException>()
            .Which.Should().BeEquivalentTo(new
            {
                Phase = phase,
                Profile = WorkflowProfileNames.Bugfix,
                ProfilePhases = BuiltInWorkflowProfiles.Bugfix.Phases.Select(x => x.Name).ToArray()
            });
    }

    [Theory]
    [InlineData("alpha")]
    [InlineData("delta")]
    [InlineData("unknown")]
    public void Start_DoneFeature_ThrowsFeatureDoneBeforeCheckingPhase(string phase)
    {
        // Arrange
        var state = CreateDoneState(Greek);

        // Act
        var act = () => new PhaseEngine().Start(state, phase, NoGates);

        // Assert
        act.Should().Throw<FeatureDoneException>()
            .Which.FeatureId.Should().Be(FeatureId);
    }

    [Fact]
    public void Start_UnknownPhaseWhileTransitionPending_ThrowsPhaseNotInProfileBeforeRule()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "alpha", "beta");

        // Act
        var act = () => new PhaseEngine().Start(state, "omega", NoGates);

        // Assert
        act.Should().Throw<PhaseNotInProfileException>();
    }

    [Fact]
    public void Start_NullArguments_ThrowArgumentNullException()
    {
        // Arrange
        var engine = new PhaseEngine();
        var state = CreateState(Greek, "alpha");

        // Act
        var withoutState = () => engine.Start(null!, "beta", NoGates);
        var withoutPhase = () => engine.Start(state, null!, NoGates);
        var withoutGates = () => engine.Start(state, "beta", null!);

        // Assert
        withoutState.Should().Throw<ArgumentNullException>();
        withoutPhase.Should().Throw<ArgumentNullException>();
        withoutGates.Should().Throw<ArgumentNullException>();
    }
}
