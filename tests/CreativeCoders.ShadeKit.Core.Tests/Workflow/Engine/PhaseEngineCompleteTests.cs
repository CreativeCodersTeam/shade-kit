using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseEngineCompleteTests
{
    [Theory]
    [Trait("Requirement", "FP04-AC8")]
    [MemberData(nameof(BuiltInProfileNames), MemberType = typeof(PhaseEngineTestData))]
    public void Complete_LastPhaseInProgress_ApprovesLastPhaseAndMakesFeatureDone(string profileName)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var last = profile.Phases[^1].Name;
        var state = CreateState(profile, last);

        // Act
        var decision = new PhaseEngine().Complete(state);

        // Assert
        decision.Phase
            .Should().Be(last);

        decision.Change.History
            .Should().Be(new PhaseHistoryRecord(PhaseHistoryAction.Complete, last, null, null));

        decision.Change.State.Status
            .Should().Be(FeatureStatus.Done);

        decision.Change.State.CurrentPhase
            .Should().Be(last);

        decision.Change.State.Phases
            .Should().AllSatisfy(x => x.Value.Should().Be(PhaseState.Approved));

        decision.Change.State.PendingTransition
            .Should().BeNull();

        state.Status
            .Should().Be(FeatureStatus.Active, "the given state is immutable");
    }

    [Theory]
    [Trait("Requirement", "FP04-AC8")]
    [MemberData(nameof(BuiltInProfileNames), MemberType = typeof(PhaseEngineTestData))]
    public void Complete_PhaseOtherThanLastPhase_ThrowsCompleteNotAllowed(string profileName)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);

        foreach (var current in profile.Phases.SkipLast(1))
        {
            var state = CreateState(profile, current.Name);

            // Act
            var act = () => new PhaseEngine().Complete(state);

            // Assert
            act.Should().Throw<PhaseCompleteNotAllowedException>()
                .Which.Should().BeEquivalentTo(new
                {
                    FeatureId,
                    CurrentPhase = current.Name,
                    CurrentState = PhaseState.InProgress,
                    LastPhase = profile.Phases[^1].Name
                });
        }
    }

    [Fact]
    [Trait("Requirement", "FP04-AC8")]
    public void Complete_LastPhaseAwaitingReopen_ThrowsCompleteNotAllowed()
    {
        // Arrange
        var state = CreateAwaitingState(Greek, "delta", "gamma");

        // Act
        var act = () => new PhaseEngine().Complete(state);

        // Assert
        act.Should().Throw<PhaseCompleteNotAllowedException>()
            .Which.Should().BeEquivalentTo(new
            {
                CurrentPhase = "delta",
                CurrentState = PhaseState.AwaitingApproval,
                LastPhase = "delta"
            });
    }

    [Fact]
    public void Complete_DoneFeature_ThrowsFeatureDone()
    {
        // Arrange
        var state = CreateDoneState(Greek);

        // Act
        var act = () => new PhaseEngine().Complete(state);

        // Assert
        act.Should().Throw<FeatureDoneException>()
            .Which.FeatureId.Should().Be(FeatureId);
    }

    [Fact]
    public void Complete_NullState_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PhaseEngine().Complete(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
