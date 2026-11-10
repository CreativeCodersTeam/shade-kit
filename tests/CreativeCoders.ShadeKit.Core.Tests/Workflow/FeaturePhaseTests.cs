using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow;

public class FeaturePhaseTests
{
    private static readonly WorkflowProfile Documented = new WorkflowProfile("documented",
    [
        new WorkflowPhase("first", "do-first", [], artifacts: [new PhaseArtifact("a.md")]),
        new WorkflowPhase("second", "do-second", [],
            artifacts: [new PhaseArtifact("b.md"), new PhaseArtifact("c.md", onDemand: true)])
    ]);

    [Fact]
    public void ListOf_StateWithExtraPhase_ReturnsProfilePhasesWithArtifactsThenExtraPhaseWithout()
    {
        // Arrange
        var state = new PhaseEngine().ReadState(Documented, CreateStored("second",
            Phases(("legacy", PhaseState.Approved), ("first", PhaseState.Approved),
                ("second", PhaseState.InProgress))));

        // Act
        var phases = FeaturePhase.ListOf(state);

        // Assert
        phases.Select(x => (x.Name, x.State))
            .Should().Equal(
                ("first", PhaseState.Approved),
                ("second", PhaseState.InProgress),
                ("legacy", PhaseState.Approved));

        phases[0].Artifacts
            .Should().Equal(new PhaseArtifact("a.md"));

        phases[1].Artifacts
            .Should().Equal(new PhaseArtifact("b.md"), new PhaseArtifact("c.md", onDemand: true));

        phases[2].Artifacts
            .Should().BeEmpty();
    }

    [Fact]
    public void ListOf_InitialState_ReturnsEveryProfilePhase()
    {
        // Arrange
        var state = new PhaseEngine().CreateInitialState(Documented, FeatureId, FeatureFilePath);

        // Act
        var phases = FeaturePhase.ListOf(state);

        // Assert
        phases.Select(x => (x.Name, x.State))
            .Should().Equal(("first", PhaseState.InProgress), ("second", PhaseState.Pending));
    }

    [Fact]
    public void ListOf_NullState_ThrowsArgumentNull()
    {
        // Act
        var act = () => FeaturePhase.ListOf(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
