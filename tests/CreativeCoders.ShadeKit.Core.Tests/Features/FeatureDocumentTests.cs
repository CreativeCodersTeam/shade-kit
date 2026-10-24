using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public class FeatureDocumentTests
{
    private const string FilePath = ".shadekit/features/gh-142-login-sso/feature.json";

    private const string ContractJson =
        """
        {
          "schemaVersion": 1,
          "minEngineVersion": "0.1.0",
          "id": "gh-142-login-sso",
          "title": "Login SSO",
          "profile": "feature",
          "source": {
            "kind": "url",
            "value": "https://github.com/acme/shop/issues/142"
          },
          "createdAt": "2026-10-20T15:30:00+02:00",
          "createdBy": {
            "name": "Jane Doe",
            "email": "jane@example.com"
          },
          "status": "active"
        }

        """;

    [Fact]
    public void Serialize_IssueFeature_WritesContractShape()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var document = CreateDocument(new FeatureSource
        {
            Kind = FeatureSourceKind.Url,
            Value = "https://github.com/acme/shop/issues/142"
        });

        // Act
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        json
            .Should().Be(ContractJson.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Serialize_TextSource_OmitsValue()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var document = CreateDocument(new FeatureSource { Kind = FeatureSourceKind.Text });

        // Act
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        var source = JsonNode.Parse(json)!["source"]!.AsObject();

        source.Select(x => x.Key)
            .Should().Equal("kind");

        source["kind"]!.GetValue<string>()
            .Should().Be("text");
    }

    [Theory]
    [InlineData(FeatureSourceKind.Text, "text")]
    [InlineData(FeatureSourceKind.Url, "url")]
    [InlineData(FeatureSourceKind.File, "file")]
    public void Serialize_SourceKind_WritesLowerCaseName(FeatureSourceKind kind, string expectedKind)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var document = CreateDocument(new FeatureSource { Kind = kind, Value = "docs/request.md" });

        // Act
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.Parse(json)!["source"]!["kind"]!.GetValue<string>()
            .Should().Be(expectedKind);
    }

    [Theory]
    [InlineData(FeatureStatus.Active, "active")]
    [InlineData(FeatureStatus.Done, "done")]
    public void Serialize_Status_WritesLowerCaseName(FeatureStatus status, string expectedStatus)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var document = CreateDocument(new FeatureSource { Kind = FeatureSourceKind.Text });
        document.Status = status;

        // Act
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.Parse(json)!["status"]!.GetValue<string>()
            .Should().Be(expectedStatus);
    }

    [Fact]
    public void Deserialize_ContractJson_ReadsAllFields()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<FeatureDocument>(ContractJson, FormatIds.Feature, FilePath, false)
            .Document;

        // Assert
        document
            .Should().BeEquivalentTo(CreateDocument(new FeatureSource
            {
                Kind = FeatureSourceKind.Url,
                Value = "https://github.com/acme/shop/issues/142"
            }), options => options.Excluding(x => x.SchemaVersion).Excluding(x => x.MinEngineVersion));

        document.CreatedAt.Offset
            .Should().Be(TimeSpan.FromHours(2));
    }

    [Theory]
    [InlineData("active", FeatureStatus.Active)]
    [InlineData("done", FeatureStatus.Done)]
    public void Deserialize_Status_ReadsStatus(string status, FeatureStatus expectedStatus)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<FeatureDocument>(CreateJson($$""" "status": "{{status}}" """),
            FormatIds.Feature, FilePath, false).Document;

        // Assert
        document.Status
            .Should().Be(expectedStatus);
    }

    [Fact]
    public void Deserialize_StatusMissing_ReadsActive()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<FeatureDocument>(CreateJson(), FormatIds.Feature, FilePath, false)
            .Document;

        // Assert
        document.Status
            .Should().Be(FeatureStatus.Active);
    }

    [Theory]
    [InlineData("text", FeatureSourceKind.Text)]
    [InlineData("url", FeatureSourceKind.Url)]
    [InlineData("file", FeatureSourceKind.File)]
    public void Deserialize_SourceKind_ReadsKind(string kind, FeatureSourceKind expectedKind)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var json = CreateJson(source: $$"""{ "kind": "{{kind}}", "value": "docs/request.md" }""");

        // Act
        var document = serializer.Deserialize<FeatureDocument>(json, FormatIds.Feature, FilePath, false).Document;

        // Assert
        document.Source.Kind
            .Should().Be(expectedKind);
    }

    [Fact]
    public void Deserialize_TextSourceWithoutValue_ReadsNullValue()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<FeatureDocument>(CreateJson(source: """{ "kind": "text" }"""),
            FormatIds.Feature, FilePath, false).Document;

        // Assert
        document.Source.Value
            .Should().BeNull();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC3")]
    public void SerializeAfterDeserialize_FeatureWithUnknownFields_KeepsThemAtTopLevelAndInSourceAndCreatedBy()
    {
        // Arrange
        const string original =
            """
            {
              "schemaVersion": 1,
              "minEngineVersion": "0.1.0",
              "id": "gh-142-login-sso",
              "title": "Login SSO",
              "profile": "feature",
              "source": {
                "kind": "url",
                "value": "https://github.com/acme/shop/issues/142",
                "fetchedAt": "2026-10-20T15:29:00+02:00"
              },
              "createdAt": "2026-10-20T15:30:00+02:00",
              "createdBy": {
                "name": "Jane Doe",
                "email": "jane@example.com",
                "signingKey": { "id": "ABC", "weight": 1.50 }
              },
              "status": "done",
              "phase": { "current": "plan", "history": [ "new", null, 2.0 ] }
            }
            """;

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<FeatureDocument>(original, FormatIds.Feature, FilePath, false).Document;
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(original))
            .Should().BeTrue($"the written feature file must keep every field, but was:{Environment.NewLine}{json}");

        document.ExtensionData
            .Should().ContainKey("phase");

        document.Source.ExtensionData
            .Should().ContainKey("fetchedAt");

        document.CreatedBy.ExtensionData
            .Should().ContainKey("signingKey");

        json.Should().Contain("\"weight\": 1.50");
    }

    [Theory]
    [InlineData("2026-10-20T15:30:00+02:00")]
    [InlineData("2026-10-20T08:30:00-05:00")]
    [InlineData("2026-10-20T13:30:00+00:00")]
    public void SerializeAfterDeserialize_CreatedAtWithOffset_KeepsLocalTimeAndOffset(string createdAt)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var json = CreateJson(createdAt: createdAt);

        // Act
        var document = serializer.Deserialize<FeatureDocument>(json, FormatIds.Feature, FilePath, false).Document;
        var writtenJson = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.Parse(writtenJson)!["createdAt"]!.GetValue<string>()
            .Should().Be(createdAt);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("title")]
    [InlineData("profile")]
    [InlineData("source")]
    [InlineData("createdAt")]
    [InlineData("createdBy")]
    public void Deserialize_RequiredFieldMissing_ThrowsInvalidKitFileException(string field)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var root = JsonNode.Parse(CreateJson())!.AsObject();
        root.Remove(field);

        // Act
        var act = () => serializer.Deserialize<FeatureDocument>(root.ToJsonString(), FormatIds.Feature, FilePath,
            false);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(FilePath);
    }

    [Theory]
    [InlineData(""" "id": null """)]
    [InlineData(""" "id": 142 """)]
    [InlineData(""" "title": null """)]
    [InlineData(""" "profile": null """)]
    [InlineData(""" "source": null """)]
    [InlineData(""" "source": "url" """)]
    [InlineData(""" "source": { "value": "https://github.com/acme/shop/issues/142" } """)]
    [InlineData(""" "source": { "kind": null } """)]
    [InlineData(""" "source": { "kind": "issue", "value": "https://github.com/acme/shop/issues/142" } """)]
    [InlineData(""" "source": { "kind": 1, "value": "https://github.com/acme/shop/issues/142" } """)]
    [InlineData(""" "source": { "kind": "url" } """)]
    [InlineData(""" "source": { "kind": "url", "value": " " } """)]
    [InlineData(""" "source": { "kind": "file", "value": null } """)]
    [InlineData(""" "source": { "kind": "file", "value": "" } """)]
    [InlineData(""" "createdAt": null """)]
    [InlineData(""" "createdAt": "yesterday" """)]
    [InlineData(""" "createdBy": null """)]
    [InlineData(""" "createdBy": { "email": "jane@example.com" } """)]
    [InlineData(""" "createdBy": { "name": "Jane Doe" } """)]
    [InlineData(""" "createdBy": { "name": "Jane Doe", "email": null } """)]
    [InlineData(""" "createdBy": { "name": null, "email": "jane@example.com" } """)]
    [InlineData(""" "status": null """)]
    [InlineData(""" "status": "closed" """)]
    [InlineData(""" "status": 0 """)]
    public void Deserialize_FieldWithInvalidValue_ThrowsInvalidKitFileException(string field)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var root = JsonNode.Parse(CreateJson())!.AsObject();
        var replacement = JsonNode.Parse($"{{ {field} }}")!.AsObject().Single();
        root[replacement.Key] = replacement.Value?.DeepClone();

        // Act
        var act = () => serializer.Deserialize<FeatureDocument>(root.ToJsonString(), FormatIds.Feature, FilePath,
            false);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(FilePath);
    }

    private static FeatureDocument CreateDocument(FeatureSource source)
    {
        return new FeatureDocument
        {
            Id = "gh-142-login-sso",
            Title = "Login SSO",
            Profile = WorkflowProfiles.Feature,
            Source = source,
            CreatedAt = new DateTimeOffset(2026, 10, 20, 15, 30, 0, TimeSpan.FromHours(2)),
            CreatedBy = new FeatureCreator { Name = "Jane Doe", Email = "jane@example.com" }
        };
    }

    private static string CreateJson(string? extraField = null, string? source = null,
        string createdAt = "2026-10-20T15:30:00+02:00")
    {
        var extra = extraField is null ? string.Empty : $",{extraField}";

        return $$"""
                 {
                   "schemaVersion": 1,
                   "minEngineVersion": "0.1.0",
                   "id": "gh-142-login-sso",
                   "title": "Login SSO",
                   "profile": "feature",
                   "source": {{source ?? """{ "kind": "url", "value": "https://github.com/acme/shop/issues/142" }"""}},
                   "createdAt": "{{createdAt}}",
                   "createdBy": { "name": "Jane Doe", "email": "jane@example.com" }{{extra}}
                 }
                 """;
    }
}
