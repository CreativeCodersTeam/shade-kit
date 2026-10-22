using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public class CurrentFeatureDocumentTests
{
    private const string FilePath = ".shadekit/features/current-feature.json";

    [Fact]
    public void Serialize_Override_WritesContractShape()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var json = serializer.Serialize(new CurrentFeatureDocument { FeatureId = "gh-142-login-sso" },
            FormatIds.CurrentFeature);

        // Assert
        json
            .Should().Be(
                """
                {
                  "schemaVersion": 1,
                  "minEngineVersion": "0.1.0",
                  "featureId": "gh-142-login-sso"
                }

                """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Deserialize_Override_ReadsFeatureId()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<CurrentFeatureDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "featureId": "gh-142-login-sso" }""",
            FormatIds.CurrentFeature, FilePath, false).Document;

        // Assert
        document.FeatureId
            .Should().Be("gh-142-login-sso");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC3")]
    public void SerializeAfterDeserialize_OverrideWithUnknownFields_KeepsThem()
    {
        // Arrange
        const string original =
            """
            {
              "schemaVersion": 1,
              "minEngineVersion": "0.1.0",
              "featureId": "gh-142-login-sso",
              "setBy": { "name": "Jane Doe", "at": [ 1, null, 2.50 ] }
            }
            """;

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<CurrentFeatureDocument>(original, FormatIds.CurrentFeature, FilePath,
            false).Document;
        var json = serializer.Serialize(document, FormatIds.CurrentFeature);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(original))
            .Should().BeTrue($"the written override must keep every field, but was:{Environment.NewLine}{json}");
    }

    [Theory]
    [InlineData("")]
    [InlineData(""", "featureId": null""")]
    [InlineData(""", "featureId": 142""")]
    public void Deserialize_FeatureIdMissingOrInvalid_ThrowsInvalidKitFileException(string featureIdField)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var act = () => serializer.Deserialize<CurrentFeatureDocument>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0"{{featureIdField}} }""",
            FormatIds.CurrentFeature, FilePath, false);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(FilePath);
    }
}
