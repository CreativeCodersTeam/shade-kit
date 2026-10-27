using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseHistoryEntryTests
{
    private const string ValidBy = """ "by": { "name": "Jane Doe", "email": "jane@example.com" } """;

    [Fact]
    public void Serialize_EntryWithAllFields_WritesFieldsInContractOrder()
    {
        // Arrange
        var document = PhaseJsonTestData.CreateDocument();
        document.History =
        [
            new PhaseHistoryEntry
            {
                At = new DateTimeOffset(2026, 10, 21, 9, 15, 30, TimeSpan.FromHours(2)),
                By = new PhaseHistoryActor { Name = "Jane Doe", Email = "jane@example.com" },
                Action = PhaseHistoryAction.Reject,
                From = "refine",
                To = "map",
                Note = "DOR-04 failed"
            }
        ];

        // Act
        var json = PhaseJsonTestData.CreateSerializer().Serialize(document, FormatIds.Feature);

        // Assert
        var entry = JsonNode.Parse(json)!["history"]![0]!.AsObject();

        entry.Select(x => x.Key)
            .Should().Equal("at", "by", "action", "from", "to", "note");

        JsonNode.DeepEquals(entry, JsonNode.Parse(
                """
                {
                  "at": "2026-10-21T09:15:30+02:00",
                  "by": { "name": "Jane Doe", "email": "jane@example.com" },
                  "action": "reject",
                  "from": "refine",
                  "to": "map",
                  "note": "DOR-04 failed"
                }
                """))
            .Should().BeTrue(entry.ToJsonString());
    }

    [Fact]
    public void Serialize_EntryWithoutToAndNote_OmitsToAndNote()
    {
        // Arrange
        var document = PhaseJsonTestData.CreateDocument();
        document.History =
        [
            new PhaseHistoryEntry
            {
                At = new DateTimeOffset(2026, 10, 21, 9, 15, 30, TimeSpan.FromHours(2)),
                By = new PhaseHistoryActor { Name = "Jane Doe", Email = "jane@example.com" },
                Action = PhaseHistoryAction.Complete,
                From = "finish"
            }
        ];

        // Act
        var json = PhaseJsonTestData.CreateSerializer().Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.Parse(json)!["history"]![0]!.AsObject().Select(x => x.Key)
            .Should().Equal("at", "by", "action", "from");
    }

    [Fact]
    public void Deserialize_EntryWithAllFields_ReadsAllFields()
    {
        // Arrange
        var json = PhaseJsonTestData.CreateHistoryJson(
            $$"""
              { "at": "2026-10-21T09:15:30-05:00", {{ValidBy}}, "action": "reject", "from": "refine", "to": "map",
                "note": "DOR-04 failed\nDOR-07 failed" }
              """);

        // Act
        var document = PhaseJsonTestData.Deserialize(json);

        // Assert
        document.History!.Single()
            .Should().BeEquivalentTo(new PhaseHistoryEntry
            {
                At = new DateTimeOffset(2026, 10, 21, 9, 15, 30, TimeSpan.FromHours(-5)),
                By = new PhaseHistoryActor { Name = "Jane Doe", Email = "jane@example.com" },
                Action = PhaseHistoryAction.Reject,
                From = "refine",
                To = "map",
                Note = "DOR-04 failed\nDOR-07 failed"
            });

        document.History!.Single().At.Offset
            .Should().Be(TimeSpan.FromHours(-5));
    }

    [Fact]
    public void Deserialize_EntryWithoutOptionalFields_ReadsNulls()
    {
        // Arrange
        var json = PhaseJsonTestData.CreateHistoryJson(
            $$"""{ "at": "2026-10-21T09:15:30+02:00", {{ValidBy}}, "action": "complete" }""");

        // Act
        var entry = PhaseJsonTestData.Deserialize(json).History!.Single();

        // Assert
        entry.From
            .Should().BeNull();

        entry.To
            .Should().BeNull();

        entry.Note
            .Should().BeNull();
    }

    [Fact]
    public void SerializeAfterDeserialize_OptionalFieldsExplicitlyNull_ReadsNullsAndOmitsThem()
    {
        // Arrange
        var json = PhaseJsonTestData.CreateHistoryJson(
            $$"""{ "at": "2026-10-21T09:15:30+02:00", {{ValidBy}}, "action": "complete", "from": null, "to": null, "note": null }""");

        // Act
        var document = PhaseJsonTestData.Deserialize(json);
        var writtenJson = PhaseJsonTestData.CreateSerializer().Serialize(document, FormatIds.Feature);

        // Assert
        document.History!.Single()
            .Should().BeEquivalentTo(new { From = (string?)null, To = (string?)null, Note = (string?)null });

        JsonNode.Parse(writtenJson)!["history"]![0]!.AsObject().Select(x => x.Key)
            .Should().Equal("at", "by", "action");
    }

    [Fact]
    public void SerializeAfterDeserialize_EntryAndActorWithUnknownFields_KeepsThem()
    {
        // Arrange
        var original = PhaseJsonTestData.CreateJson(
            """
            "status": "active",
            "history": [
              {
                "at": "2026-10-21T09:15:30+02:00",
                "by": { "name": "Jane Doe", "email": "jane@example.com", "signingKey": { "id": "ABC", "weight": 1.50 } },
                "action": "approve",
                "from": "refine",
                "to": "map",
                "checklist": { "passed": [ "DOR-01", null, 2.0 ] }
              }
            ]
            """);

        var serializer = PhaseJsonTestData.CreateSerializer();

        // Act
        var document = PhaseJsonTestData.Deserialize(original);
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(original))
            .Should().BeTrue($"the written feature file must keep every field, but was:{Environment.NewLine}{json}");

        document.History!.Single().ExtensionData
            .Should().ContainKey("checklist");

        document.History!.Single().By.ExtensionData
            .Should().ContainKey("signingKey");

        json.Should().Contain("\"weight\": 1.50");
    }

    [Theory]
    [InlineData($$"""{ {{ValidBy}}, "action": "start" }""")]
    [InlineData("""{ "at": "2026-10-21T09:15:30+02:00", "action": "start" }""")]
    [InlineData($$"""{ "at": "2026-10-21T09:15:30+02:00", {{ValidBy}} }""")]
    [InlineData($$"""{ "at": null, {{ValidBy}}, "action": "start" }""")]
    [InlineData($$"""{ "at": "yesterday", {{ValidBy}}, "action": "start" }""")]
    [InlineData("""{ "at": "2026-10-21T09:15:30+02:00", "by": null, "action": "start" }""")]
    [InlineData("""{ "at": "2026-10-21T09:15:30+02:00", "by": "Jane Doe", "action": "start" }""")]
    [InlineData("""{ "at": "2026-10-21T09:15:30+02:00", "by": { "name": "Jane Doe" }, "action": "start" }""")]
    [InlineData("""{ "at": "2026-10-21T09:15:30+02:00", "by": { "email": "jane@example.com" }, "action": "start" }""")]
    [InlineData("""{ "at": "2026-10-21T09:15:30+02:00", "by": { "name": null, "email": "jane@example.com" }, "action": "start" }""")]
    [InlineData($$"""{ "at": "2026-10-21T09:15:30+02:00", {{ValidBy}}, "action": "start", "from": 1 }""")]
    [InlineData($$"""{ "at": "2026-10-21T09:15:30+02:00", {{ValidBy}}, "action": "start", "note": [] }""")]
    [InlineData("\"start\"")]
    [InlineData("null")]
    public void Deserialize_InvalidEntry_ThrowsInvalidKitFileException(string entry)
    {
        // Arrange
        var json = PhaseJsonTestData.CreateHistoryJson(entry);

        // Act
        var act = () => PhaseJsonTestData.Deserialize(json);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(PhaseJsonTestData.FilePath);
    }
}
