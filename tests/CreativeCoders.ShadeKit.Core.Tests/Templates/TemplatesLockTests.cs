using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Templates;

namespace CreativeCoders.ShadeKit.Core.Tests.Templates;

public class TemplatesLockTests
{
    private const string FilePath = ".shadekit/templates.lock.json";

    private const string SpecHash = "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string PlanHash = "sha256:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

    [Fact]
    public void Serialize_LockWithEntries_WritesFieldsInContractOrder()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var templatesLock = new TemplatesLock
        {
            Templates =
            [
                new TemplatesLockEntry { Path = ".shadekit/templates/plan.md", KitVersion = "1.2.3", Hash = PlanHash },
                new TemplatesLockEntry { Path = ".shadekit/templates/spec.md", KitVersion = "1.2.3", Hash = SpecHash }
            ]
        };

        // Act
        var json = serializer.Serialize(templatesLock, FormatIds.TemplatesLock);

        // Assert
        json
            .Should().Be(
                $$"""
                  {
                    "schemaVersion": 1,
                    "minEngineVersion": "0.1.0",
                    "templates": [
                      {
                        "path": ".shadekit/templates/plan.md",
                        "kitVersion": "1.2.3",
                        "hash": "{{PlanHash}}"
                      },
                      {
                        "path": ".shadekit/templates/spec.md",
                        "kitVersion": "1.2.3",
                        "hash": "{{SpecHash}}"
                      }
                    ]
                  }

                  """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Serialize_NewLock_WritesEmptyTemplates()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var json = serializer.Serialize(new TemplatesLock(), FormatIds.TemplatesLock);

        // Assert
        JsonNode.Parse(json)!["templates"]!.AsArray()
            .Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_LockWithEntries_ReadsEntries()
    {
        // Arrange
        const string json =
            $$"""
              {
                "schemaVersion": 1,
                "minEngineVersion": "0.1.0",
                "templates": [
                  { "path": ".shadekit/templates/spec.md", "kitVersion": "1.2.3", "hash": "{{SpecHash}}" }
                ]
              }
              """;

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var templatesLock = serializer
            .Deserialize<TemplatesLock>(json, FormatIds.TemplatesLock, FilePath, false).Document;

        // Assert
        templatesLock.Templates
            .Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new TemplatesLockEntry
            {
                Path = ".shadekit/templates/spec.md",
                KitVersion = "1.2.3",
                Hash = SpecHash
            });
    }

    [Fact]
    public void Deserialize_TemplatesMissing_ReturnsEmptyList()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var templatesLock = serializer.Deserialize<TemplatesLock>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""", FormatIds.TemplatesLock, FilePath,
            false).Document;

        // Assert
        templatesLock.Templates
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC3")]
    public void SerializeAfterDeserialize_LockWithUnknownFields_KeepsThemAtTopLevelAndInEntries()
    {
        // Arrange
        const string original =
            $$"""
              {
                "schemaVersion": 1,
                "minEngineVersion": "0.1.0",
                "templates": [
                  {
                    "path": ".shadekit/templates/spec.md",
                    "kitVersion": "1.2.3",
                    "hash": "{{SpecHash}}",
                    "owner": { "package": "10", "size": 1.50 }
                  }
                ],
                "generatedBy": [ "init", null, 2.0 ]
              }
              """;

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var templatesLock = serializer
            .Deserialize<TemplatesLock>(original, FormatIds.TemplatesLock, FilePath, false).Document;
        var json = serializer.Serialize(templatesLock, FormatIds.TemplatesLock);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(original))
            .Should().BeTrue($"the written lock file must keep every field, but was:{Environment.NewLine}{json}");

        templatesLock.Templates.Single().ExtensionData
            .Should().ContainKey("owner");

        json.Should().Contain("\"size\": 1.50");
    }

    [Theory]
    [InlineData(""" "kitVersion": "1.2.3", "hash": "sha256:00" """)]
    [InlineData(""" "path": null, "kitVersion": "1.2.3", "hash": "sha256:00" """)]
    [InlineData(""" "path": ".shadekit/templates/spec.md", "kitVersion": "1.2.3" """)]
    [InlineData(""" "path": ".shadekit/templates/spec.md", "kitVersion": "1.2.3", "hash": null """)]
    [InlineData(""" "path": ".shadekit/templates/spec.md", "hash": "sha256:00" """)]
    [InlineData(""" "path": ".shadekit/templates/spec.md", "kitVersion": null, "hash": "sha256:00" """)]
    [InlineData(""" "path": 1, "kitVersion": "1.2.3", "hash": "sha256:00" """)]
    [InlineData(""" "path": ".shadekit/templates/spec.md", "kitVersion": 1, "hash": "sha256:00" """)]
    [InlineData(""" "path": ".shadekit/templates/spec.md", "kitVersion": "1.2.3", "hash": true """)]
    public void Deserialize_EntryWithMissingOrInvalidRequiredField_ThrowsInvalidKitFileException(string entryFields)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var act = () => serializer.Deserialize<TemplatesLock>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "templates": [ { {{entryFields}} } ] }""",
            FormatIds.TemplatesLock, FilePath, false);

        // Assert
        act.Should().Throw<InvalidKitFileException>();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[ null ]")]
    [InlineData("[ \".shadekit/templates/spec.md\" ]")]
    public void Deserialize_InvalidTemplates_ThrowsInvalidKitFileException(string templates)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var act = () => serializer.Deserialize<TemplatesLock>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "templates": {{templates}} }""",
            FormatIds.TemplatesLock, FilePath, false);

        // Assert
        act.Should().Throw<InvalidKitFileException>();
    }
}
