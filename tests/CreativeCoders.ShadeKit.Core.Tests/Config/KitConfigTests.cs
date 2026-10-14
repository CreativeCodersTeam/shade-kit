using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Tests.Config;

public class KitConfigTests
{
    private const string FilePath = ".shadekit/config.json";

    [Theory]
    [InlineData(""", "outdatedFormatOnRead": "migrate-and-warn" """, OutdatedFormatOnReadPolicy.MigrateAndWarn)]
    [InlineData(""", "outdatedFormatOnRead": "stop" """, OutdatedFormatOnReadPolicy.Stop)]
    [InlineData(""", "outdatedFormatOnRead": "Stop" """, OutdatedFormatOnReadPolicy.Stop)]
    [InlineData(""", "outdatedFormatOnRead": "STOP" """, OutdatedFormatOnReadPolicy.Stop)]
    [InlineData(""", "outdatedFormatOnRead": "MigrateAndWarn" """, OutdatedFormatOnReadPolicy.MigrateAndWarn)]
    [InlineData("", OutdatedFormatOnReadPolicy.MigrateAndWarn)]
    public void Deserialize_OutdatedFormatOnRead_ReadsPolicy(string policyField, OutdatedFormatOnReadPolicy expected)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0"{{policyField}} }""", FormatIds.Config, FilePath,
            false).Document;

        // Assert
        config.OutdatedFormatOnRead
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("\"sometimes\"")]
    [InlineData("\"MIGRATE-AND-WARN\"")]
    [InlineData("\"Migrate-And-Warn\"")]
    [InlineData("1")]
    [InlineData("null")]
    public void Deserialize_InvalidOutdatedFormatOnRead_ThrowsInvalidKitFileException(string value)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var act = () => serializer.Deserialize<KitConfig>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "outdatedFormatOnRead": {{value}} }""",
            FormatIds.Config, FilePath, false);

        // Assert
        act.Should().Throw<InvalidKitFileException>();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC3")]
    public void SerializeAfterDeserialize_ConfigWithFieldsOfLaterPackages_KeepsThemUnchanged()
    {
        // Arrange
        const string original =
            """
            {
              "schemaVersion": 1,
              "minEngineVersion": "0.1.0",
              "kitVersion": "1.4.0",
              "defaultProfile": "feature",
              "outdatedFormatOnRead": "stop",
              "trackers": { "github.com": "gh" },
              "coverageThreshold": 80.0
            }
            """;

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>(original, FormatIds.Config, FilePath, false).Document;
        var json = serializer.Serialize(config, FormatIds.Config);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(original))
            .Should().BeTrue($"the written config must keep every field, but was:{Environment.NewLine}{json}");

        json.Should().Contain("\"coverageThreshold\": 80.0");
    }

    [Fact]
    public void Serialize_StopPolicy_WritesKebabCaseValue()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var config = new KitConfig { OutdatedFormatOnRead = OutdatedFormatOnReadPolicy.Stop };

        // Act
        var json = serializer.Serialize(config, FormatIds.Config);

        // Assert
        JsonNode.Parse(json)!["outdatedFormatOnRead"]!.GetValue<string>()
            .Should().Be("stop");
    }
}
