using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseEngineValueTypesTests
{
    [Fact]
    public void TransitionGate_Default_RequiresApprovalWithoutChecklist()
    {
        // Assert
        TransitionGate.Default
            .Should().Be(new TransitionGate(true, null));
    }

    [Fact]
    public void ToEntry_TimeAndActor_CreatesHistoryEntryWithRecordValues()
    {
        // Arrange
        var at = new DateTimeOffset(2026, 10, 26, 14, 30, 0, TimeSpan.FromHours(1));
        var by = new PhaseHistoryActor { Name = "Ada", Email = "ada@example.com" };
        var record = new PhaseHistoryRecord(PhaseHistoryAction.Reject, "refine", "map", "DOR-04 failed");

        // Act
        var entry = record.ToEntry(at, by);

        // Assert
        entry.Should().BeEquivalentTo(new
        {
            At = at,
            By = by,
            Action = PhaseHistoryAction.Reject,
            From = "refine",
            To = "map",
            Note = "DOR-04 failed",
            ExtensionData = (Dictionary<string, System.Text.Json.JsonElement>?)null
        });
    }

    [Fact]
    public void ToEntry_ActorNull_ThrowsArgumentNullException()
    {
        // Arrange
        var record = new PhaseHistoryRecord(PhaseHistoryAction.Complete, "finish", null, null);

        // Act
        var act = () => record.ToEntry(DateTimeOffset.UnixEpoch, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetPhaseState_PhaseWithoutEntry_ReturnsNull()
    {
        // Arrange
        var state = CreateState(Greek, "alpha");

        // Act
        var phaseState = state.GetPhaseState("omega");

        // Assert
        phaseState
            .Should().BeNull();

        state.GetPhaseState(null)
            .Should().BeNull();
    }

    [Fact]
    public void Phases_State_CannotBeChangedThroughTheList()
    {
        // Arrange
        var state = CreateState(Greek, "alpha");

        // Act
        var act = () => ((IList<KeyValuePair<string, PhaseState>>)state.Phases)[0] =
            KeyValuePair.Create("alpha", PhaseState.Approved);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Phases_State_CanBeCopiedIntoOrderedDictionaryForFeatureDocument()
    {
        // Arrange
        var state = CreateState(Greek, "gamma");

        // Act
        var phases = new OrderedDictionary<string, PhaseState>(state.Phases);

        // Assert
        phases.Keys
            .Should().Equal("alpha", "beta", "gamma", "delta");
    }

    [Fact]
    public void Pending_StateWithoutPendingTransition_IsNull()
    {
        // Arrange
        var state = CreateState(Greek, "alpha");

        // Assert
        state.Pending
            .Should().BeNull();
    }
}
