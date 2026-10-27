using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseStateTests
{
    [Theory]
    [InlineData(PhaseState.Pending, "pending")]
    [InlineData(PhaseState.InProgress, "in-progress")]
    [InlineData(PhaseState.AwaitingApproval, "awaiting-approval")]
    [InlineData(PhaseState.Approved, "approved")]
    public void ToJsonName_PhaseState_ReturnsKebabCaseName(PhaseState state, string expectedName)
    {
        // Act
        var name = state.ToJsonName();

        // Assert
        name
            .Should().Be(expectedName);
    }

    [Theory]
    [InlineData(PhaseState.Pending, "pending")]
    [InlineData(PhaseState.InProgress, "in-progress")]
    [InlineData(PhaseState.AwaitingApproval, "awaiting-approval")]
    [InlineData(PhaseState.Approved, "approved")]
    public void Serialize_PhaseState_WritesKebabCaseName(PhaseState state, string expectedName)
    {
        // Arrange
        var document = PhaseJsonTestData.CreateDocument();
        document.Phases = new OrderedDictionary<string, PhaseState> { ["plan"] = state };

        // Act
        var json = PhaseJsonTestData.CreateSerializer().Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.Parse(json)!["phases"]!["plan"]!.GetValue<string>()
            .Should().Be(expectedName);
    }

    [Theory]
    [InlineData("pending", PhaseState.Pending)]
    [InlineData("in-progress", PhaseState.InProgress)]
    [InlineData("awaiting-approval", PhaseState.AwaitingApproval)]
    [InlineData("approved", PhaseState.Approved)]
    public void Deserialize_PhaseState_ReadsState(string state, PhaseState expectedState)
    {
        // Arrange
        var json = PhaseJsonTestData.CreateJson($$""" "phases": { "plan": "{{state}}" } """);

        // Act
        var document = PhaseJsonTestData.Deserialize(json);

        // Assert
        document.Phases
            .Should().Equal(new Dictionary<string, PhaseState> { ["plan"] = expectedState });
    }

    [Theory]
    [InlineData(""" "phases": { "plan": "skipped" } """)]
    [InlineData(""" "phases": { "plan": "in progress" } """)]
    [InlineData(""" "phases": { "plan": "" } """)]
    [InlineData(""" "phases": { "plan": "1" } """)]
    [InlineData(""" "phases": { "plan": "in_progress" } """)]
    [InlineData(""" "phases": { "plan": 1 } """)]
    [InlineData(""" "phases": { "plan": null } """)]
    public void Deserialize_InvalidPhaseState_ThrowsInvalidKitFileException(string field)
    {
        // Arrange
        var json = PhaseJsonTestData.CreateJson(field);

        // Act
        var act = () => PhaseJsonTestData.Deserialize(json);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(PhaseJsonTestData.FilePath);
    }

    [Fact]
    public void Deserialize_CommaSeparatedPhaseStates_ThrowsInvalidKitFileException()
    {
        // Arrange
        var json = PhaseJsonTestData.CreateJson(""" "phases": { "plan": "pending, approved" } """);

        // Act
        var act = () => PhaseJsonTestData.Deserialize(json);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(PhaseJsonTestData.FilePath);
    }
}
