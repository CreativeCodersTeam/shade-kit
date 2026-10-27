using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseEngineNextTests
{
    [Theory]
    [Trait("Requirement", "FP04-AC14")]
    [MemberData(nameof(BuiltInProfilePhasesWithNext), MemberType = typeof(PhaseEngineTestData))]
    public void GetNext_PhaseInProgressBeforeLastPhase_ReturnsContinueForwardAndReopen(string profileName,
        int currentIndex)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var current = profile.Phases[currentIndex];
        var next = profile.Phases[currentIndex + 1];
        var state = CreateState(profile, current.Name);

        // Act
        var steps = new PhaseEngine().GetNext(state, NoGates);

        // Assert
        steps.State
            .Should().BeSameAs(state);

        steps.Continue
            .Should().Be(new PhaseStep(current.Name, current.PhaseSkill));

        steps.Pending
            .Should().BeNull();

        steps.Forward
            .Should().Be(new GatedPhaseStep(next.Name, TransitionGate.Default, next.PhaseSkill));

        steps.Reopen
            .Should().Equal(profile.Phases.Take(currentIndex)
                .Where(x => x.Name != WorkflowPhaseNames.New)
                .Select(x => new PhaseStep(x.Name, x.PhaseSkill)));
    }

    [Theory]
    [Trait("Requirement", "FP04-AC14")]
    [MemberData(nameof(BuiltInProfileNames), MemberType = typeof(PhaseEngineTestData))]
    public void GetNext_LastPhaseInProgress_ReturnsNoForward(string profileName)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var last = profile.Phases[^1];
        var state = CreateState(profile, last.Name);

        // Act
        var steps = new PhaseEngine().GetNext(state, NoGates);

        // Assert
        steps.Continue
            .Should().Be(new PhaseStep(last.Name, last.PhaseSkill));

        steps.Forward
            .Should().BeNull();

        steps.Reopen
            .Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public void GetNext_FirstPhaseInProgress_ReturnsEmptyReopen()
    {
        // Arrange
        var state = CreateState(BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.New);

        // Act
        var steps = new PhaseEngine().GetNext(state, NoGates);

        // Assert
        steps.Reopen
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public void GetNext_ForwardWithConfiguredGate_ReturnsGateOfNextPhase()
    {
        // Arrange
        var gate = new TransitionGate(false, ".shadekit/checks/dod.md");
        var state = CreateState(Greek, "gamma");

        // Act
        var steps = new PhaseEngine().GetNext(state, new Dictionary<string, TransitionGate> { ["delta"] = gate });

        // Assert
        steps.Forward
            .Should().Be(new GatedPhaseStep("delta", gate, "skill-delta"));

        steps.Reopen
            .Should().Equal(new PhaseStep("alpha", "skill-alpha"));
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public void GetNext_ForwardTransitionPending_ReturnsOnlyPendingWithGate()
    {
        // Arrange
        var gate = new TransitionGate(true, ".shadekit/checks/dor.md");
        var state = CreateAwaitingState(BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.Refine,
            WorkflowPhaseNames.Map);

        // Act
        var steps = new PhaseEngine().GetNext(state,
            new Dictionary<string, TransitionGate> { [WorkflowPhaseNames.Map] = gate });

        // Assert
        steps.Should().BeEquivalentTo(new PhaseNextSteps(state, null,
            new GatedPhaseStep(WorkflowPhaseNames.Map, gate, "shade-kit-map"), null, null));
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public void GetNext_ForwardTransitionPendingWhoseGateWasSwitchedOff_ReturnsPendingGateWithApproval()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "alpha", "beta");

        // Act
        var steps = new PhaseEngine().GetNext(state,
            new Dictionary<string, TransitionGate> { ["beta"] = new(false, "checklist.md") });

        // Assert
        steps.Pending
            .Should().Be(new GatedPhaseStep("beta", new TransitionGate(true, "checklist.md"), "skill-beta"));
    }

    [Fact]
    public void GetNext_GateValuesNull_UseDefaultGate()
    {
        // Arrange
        var gates = new Dictionary<string, TransitionGate> { ["beta"] = null!, ["gamma"] = null! };
        var engine = new PhaseEngine();

        // Act
        var forward = engine.GetNext(CreateState(Greek, "alpha"), gates).Forward;
        var pending = engine.GetNext(CreateAwaitingState(Greek, "beta", "gamma"), gates).Pending;

        // Assert
        forward!.Gate
            .Should().Be(TransitionGate.Default);

        pending!.Gate
            .Should().Be(TransitionGate.Default);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC14")]
    public void GetNext_ReopenPending_ReturnsPendingWithDefaultGate()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "delta", "gamma");

        // Act
        var steps = new PhaseEngine().GetNext(state,
            new Dictionary<string, TransitionGate> { ["gamma"] = new(false, "checklist.md") });

        // Assert
        steps.Pending
            .Should().Be(new GatedPhaseStep("gamma", TransitionGate.Default, "skill-gamma"));

        steps.Continue
            .Should().BeNull();

        steps.Forward
            .Should().BeNull();

        steps.Reopen
            .Should().BeNull();
    }

    [Theory]
    [Trait("Requirement", "FP04-AC14")]
    [MemberData(nameof(BuiltInProfileNames), MemberType = typeof(PhaseEngineTestData))]
    public void GetNext_DoneFeature_ReturnsOnlyCurrentState(string profileName)
    {
        // Arrange
        var state = CreateDoneState(GetBuiltInProfile(profileName));

        // Act
        var steps = new PhaseEngine().GetNext(state, NoGates);

        // Assert
        steps.Should().Be(new PhaseNextSteps(state, null, null, null, null));
    }

    [Fact]
    public void GetNext_NullArguments_ThrowArgumentNullException()
    {
        // Arrange
        var engine = new PhaseEngine();

        // Act
        var withoutState = () => engine.GetNext(null!, NoGates);
        var withoutGates = () => engine.GetNext(CreateState(Greek, "alpha"), null!);

        // Assert
        withoutState.Should().Throw<ArgumentNullException>();
        withoutGates.Should().Throw<ArgumentNullException>();
    }
}
