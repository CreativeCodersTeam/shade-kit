using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseHistoryActionTests
{
    [Theory]
    [InlineData(PhaseHistoryAction.Start, "start")]
    [InlineData(PhaseHistoryAction.Approve, "approve")]
    [InlineData(PhaseHistoryAction.Reject, "reject")]
    [InlineData(PhaseHistoryAction.Reopen, "reopen")]
    [InlineData(PhaseHistoryAction.Complete, "complete")]
    public void ToJsonName_Action_ReturnsLowerCaseName(PhaseHistoryAction action, string expectedName)
    {
        // Act
        var name = action.ToJsonName();

        // Assert
        name
            .Should().Be(expectedName);
    }

    [Theory]
    [InlineData(PhaseHistoryAction.Start, "start")]
    [InlineData(PhaseHistoryAction.Approve, "approve")]
    [InlineData(PhaseHistoryAction.Reject, "reject")]
    [InlineData(PhaseHistoryAction.Reopen, "reopen")]
    [InlineData(PhaseHistoryAction.Complete, "complete")]
    public void Serialize_Action_WritesLowerCaseName(PhaseHistoryAction action, string expectedName)
    {
        // Arrange
        var document = PhaseJsonTestData.CreateDocument();
        document.History =
        [
            new PhaseHistoryEntry
            {
                At = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(2)),
                By = new PhaseHistoryActor { Name = "Jane Doe", Email = "jane@example.com" },
                Action = action
            }
        ];

        // Act
        var json = PhaseJsonTestData.CreateSerializer().Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.Parse(json)!["history"]![0]!["action"]!.GetValue<string>()
            .Should().Be(expectedName);
    }

    [Theory]
    [InlineData("start", PhaseHistoryAction.Start)]
    [InlineData("approve", PhaseHistoryAction.Approve)]
    [InlineData("reject", PhaseHistoryAction.Reject)]
    [InlineData("reopen", PhaseHistoryAction.Reopen)]
    [InlineData("complete", PhaseHistoryAction.Complete)]
    public void Deserialize_Action_ReadsAction(string action, PhaseHistoryAction expectedAction)
    {
        // Arrange
        var json = PhaseJsonTestData.CreateHistoryJson(
            $$"""
              { "at": "2026-10-21T09:00:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": "{{action}}" }
              """);

        // Act
        var document = PhaseJsonTestData.Deserialize(json);

        // Assert
        document.History!.Single().Action
            .Should().Be(expectedAction);
    }

    [Theory]
    [InlineData("\"close\"")]
    [InlineData("\"started\"")]
    [InlineData("\"\"")]
    [InlineData("\"1\"")]
    [InlineData("0")]
    [InlineData("null")]
    public void Deserialize_InvalidAction_ThrowsInvalidKitFileException(string action)
    {
        // Arrange
        var json = PhaseJsonTestData.CreateHistoryJson(
            $$"""
              { "at": "2026-10-21T09:00:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
                "action": {{action}} }
              """);

        // Act
        var act = () => PhaseJsonTestData.Deserialize(json);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(PhaseJsonTestData.FilePath);
    }

    [Fact]
    public void Deserialize_CommaSeparatedActions_ThrowsInvalidKitFileException()
    {
        // Arrange
        var json = PhaseJsonTestData.CreateHistoryJson(
            """
            { "at": "2026-10-21T09:00:00+02:00", "by": { "name": "Jane Doe", "email": "jane@example.com" },
              "action": "start, approve" }
            """);

        // Act
        var act = () => PhaseJsonTestData.Deserialize(json);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(PhaseJsonTestData.FilePath);
    }
}
