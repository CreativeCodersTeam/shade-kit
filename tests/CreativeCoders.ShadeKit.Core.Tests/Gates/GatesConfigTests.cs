using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Gates;

namespace CreativeCoders.ShadeKit.Core.Tests.Gates;

public class GatesConfigTests
{
    private const string FilePath = ".shadekit/gates.json";

    [Fact]
    public void Serialize_NewGatesConfig_WritesOnlySchemaVersionAndMinEngineVersion()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var json = serializer.Serialize(new GatesConfig(), FormatIds.Gates);

        // Assert
        json
            .Should().Be(
                """
                {
                  "schemaVersion": 1,
                  "minEngineVersion": "0.1.0"
                }

                """.ReplaceLineEndings("\n"));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC3")]
    public void SerializeAfterDeserialize_GatesWithFieldsOfLaterPackages_KeepsThemUnchanged()
    {
        // Arrange
        const string original =
            """
            {
              "schemaVersion": 1,
              "minEngineVersion": "0.1.0",
              "gates": { "plan": { "approval": false, "checklist": "checks/definition-of-ready.md" } },
              "threshold": 80.0
            }
            """;

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var gates = serializer.Deserialize<GatesConfig>(original, FormatIds.Gates, FilePath, false).Document;
        var json = serializer.Serialize(gates, FormatIds.Gates);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(original))
            .Should().BeTrue($"the written gates file must keep every field, but was:{Environment.NewLine}{json}");

        json.Should().Contain("\"threshold\": 80.0");
    }
}
