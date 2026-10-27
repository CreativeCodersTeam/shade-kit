using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

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
    [InlineData("Active", FeatureStatus.Active)]
    [InlineData("ACTIVE", FeatureStatus.Active)]
    [InlineData("Done", FeatureStatus.Done)]
    [InlineData("DONE", FeatureStatus.Done)]
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

    [Theory]
    [InlineData(""" "status": "active, done" """, null)]
    [InlineData(""" "status": "active,done" """, null)]
    [InlineData(""" "status": "active, active" """, null)]
    [InlineData(""" "status": "Active, Done" """, null)]
    [InlineData(null, """{ "kind": "text, text" }""")]
    [InlineData(null, """{ "kind": "url,url", "value": "https://github.com/acme/shop/issues/142" }""")]
    public void Deserialize_CommaSeparatedEnumValue_ThrowsInvalidKitFileException(string? statusField, string? source)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var act = () => serializer.Deserialize<FeatureDocument>(CreateJson(statusField, source), FormatIds.Feature,
            FilePath, false);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(FilePath);
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

    private const string PhaseStateJson =
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
          "status": "active",
          "currentPhase": "refine",
          "phases": {
            "new": "approved",
            "refine": "awaiting-approval",
            "map": "pending",
            "plan": "pending",
            "implement": "pending",
            "verify": "pending",
            "finish": "pending"
          },
          "pendingTransition": "map",
          "history": [
            {
              "at": "2026-10-20T16:00:00+02:00",
              "by": {
                "name": "Jane Doe",
                "email": "jane@example.com"
              },
              "action": "start",
              "from": "new",
              "to": "refine"
            },
            {
              "at": "2026-10-20T16:05:00+02:00",
              "by": {
                "name": "John Roe",
                "email": "john@example.com"
              },
              "action": "approve",
              "from": "new",
              "to": "refine",
              "note": "approval off"
            },
            {
              "at": "2026-10-21T09:00:00+02:00",
              "by": {
                "name": "Jane Doe",
                "email": "jane@example.com"
              },
              "action": "start",
              "from": "refine",
              "to": "map"
            }
          ]
        }

        """;

    [Fact]
    public void Serialize_FeatureWithPhaseState_WritesContractShape()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var document = CreatePhaseStateDocument();

        // Act
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        json
            .Should().Be(PhaseStateJson.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Deserialize_FeatureWithPhaseState_ReadsAllFields()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<FeatureDocument>(PhaseStateJson, FormatIds.Feature, FilePath, false)
            .Document;

        // Assert
        document
            .Should().BeEquivalentTo(CreatePhaseStateDocument(),
                options => options.Excluding(x => x.SchemaVersion).Excluding(x => x.MinEngineVersion)
                    .WithStrictOrdering());
    }

    [Fact]
    public void SerializeAfterDeserialize_FeatureWithPhaseState_WritesSameText()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<FeatureDocument>(PhaseStateJson, FormatIds.Feature, FilePath, false)
            .Document;
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        json
            .Should().Be(PhaseStateJson.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void SerializeAfterDeserialize_PhasesInNonAlphabeticalOrder_KeepsKeyOrder()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var json = CreateJson(
            """ "phases": { "zeta": "pending", "alpha": "in-progress", "mu": "approved", "beta": "pending" } """);

        // Act
        var document = serializer.Deserialize<FeatureDocument>(json, FormatIds.Feature, FilePath, false).Document;
        var writtenJson = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        document.Phases!.Keys
            .Should().Equal("zeta", "alpha", "mu", "beta");

        JsonNode.Parse(writtenJson)!["phases"]!.AsObject().Select(x => x.Key)
            .Should().Equal("zeta", "alpha", "mu", "beta");
    }

    [Fact]
    public void SerializeAfterDeserialize_StateOfReadPhaseChanged_KeepsKeyPosition()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var json = CreateJson(""" "phases": { "new": "approved", "quick": "in-progress", "finish": "pending" } """);
        var document = serializer.Deserialize<FeatureDocument>(json, FormatIds.Feature, FilePath, false).Document;

        // Act
        document.Phases![WorkflowPhaseNames.Quick] = PhaseState.Approved;
        document.Phases[WorkflowPhaseNames.Finish] = PhaseState.InProgress;
        var writtenJson = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.Parse(writtenJson)!["phases"]!.AsObject().Select(x => (x.Key, x.Value!.GetValue<string>()))
            .Should().Equal(("new", "approved"), ("quick", "approved"), ("finish", "in-progress"));
    }

    [Fact]
    public void SerializeAfterDeserialize_EmptyPhasesAndHistory_ReadsEmptyCollectionsAndWritesThemBack()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var json = CreateJson(""" "phases": {}, "history": [] """);

        // Act
        var document = serializer.Deserialize<FeatureDocument>(json, FormatIds.Feature, FilePath, false).Document;
        var writtenJson = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        document.Phases
            .Should().NotBeNull().And.BeEmpty();

        document.History
            .Should().NotBeNull().And.BeEmpty();

        var root = JsonNode.Parse(writtenJson)!.AsObject();

        root["phases"]!.AsObject()
            .Should().BeEmpty();

        root["history"]!.AsArray()
            .Should().BeEmpty();
    }

    [Fact]
    public void Serialize_PhasesSetInInsertionOrder_WritesThatOrder()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var document = CreateDocument(new FeatureSource { Kind = FeatureSourceKind.Text });
        document.Phases = new OrderedDictionary<string, PhaseState>
        {
            ["new"] = PhaseState.InProgress,
            ["diagnose"] = PhaseState.Pending,
            ["finish"] = PhaseState.Pending,
            ["archived"] = PhaseState.Approved
        };

        // Act
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.Parse(json)!["phases"]!.AsObject().Select(x => x.Key)
            .Should().Equal("new", "diagnose", "finish", "archived");
    }

    [Fact]
    public void Serialize_PendingTransitionNull_OmitsField()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var document = CreatePhaseStateDocument();
        document.PendingTransition = null;

        // Act
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.Parse(json)!.AsObject()
            .Should().NotContainKey("pendingTransition")
            .And.ContainKeys("currentPhase", "phases", "history");
    }

    [Fact]
    public void Serialize_EmptyPhasesAndHistory_WritesEmptyObjectAndArray()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var document = CreateDocument(new FeatureSource { Kind = FeatureSourceKind.Text });
        document.Phases = [];
        document.History = [];

        // Act
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        var root = JsonNode.Parse(json)!.AsObject();

        root["phases"]!.AsObject()
            .Should().BeEmpty();

        root["history"]!.AsArray()
            .Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_FeatureWithoutPhaseFields_ReadsNulls()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<FeatureDocument>(ContractJson, FormatIds.Feature, FilePath, false)
            .Document;

        // Assert
        document.CurrentPhase
            .Should().BeNull();

        document.Phases
            .Should().BeNull();

        document.PendingTransition
            .Should().BeNull();

        document.History
            .Should().BeNull();
    }

    [Fact]
    public void SerializeAfterDeserialize_FeatureWithoutPhaseFields_WritesNoPhaseFields()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<FeatureDocument>(ContractJson, FormatIds.Feature, FilePath, false)
            .Document;
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        json
            .Should().Be(ContractJson.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Deserialize_PhaseFieldsExplicitlyNull_ReadsNullsAndWritesNoPhaseFields()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var json = CreateJson(""" "currentPhase": null, "phases": null, "pendingTransition": null, "history": null """);

        // Act
        var document = serializer.Deserialize<FeatureDocument>(json, FormatIds.Feature, FilePath, false).Document;
        var writtenJson = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        document.CurrentPhase
            .Should().BeNull();

        document.Phases
            .Should().BeNull();

        document.History
            .Should().BeNull();

        JsonNode.Parse(writtenJson)!.AsObject()
            .Should().NotContainKeys("currentPhase", "phases", "pendingTransition", "history");
    }

    [Fact]
    public void SerializeAfterDeserialize_FeatureWithPhaseStateAndUnknownFields_KeepsUnknownFields()
    {
        // Arrange
        var root = JsonNode.Parse(PhaseStateJson)!.AsObject();
        root["gates"] = JsonNode.Parse("""{ "plan": { "checklist": "dor" } }""");
        root["history"]![0]!["checklist"] = JsonNode.Parse("""[ "DOR-01", null, 2.0 ]""");
        root["history"]![1]!["by"]!["signingKey"] = "ABC";
        var original = root.ToJsonString();

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var document = serializer.Deserialize<FeatureDocument>(original, FormatIds.Feature, FilePath, false).Document;
        var json = serializer.Serialize(document, FormatIds.Feature);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(original))
            .Should().BeTrue($"the written feature file must keep every field, but was:{Environment.NewLine}{json}");

        document.ExtensionData
            .Should().ContainKey("gates");
    }

    [Theory]
    [InlineData(""" "currentPhase": 3 """)]
    [InlineData(""" "currentPhase": { "name": "plan" } """)]
    [InlineData(""" "phases": [ "new", "plan" ] """)]
    [InlineData(""" "phases": "new" """)]
    [InlineData(""" "phases": { "new": "done" } """)]
    [InlineData(""" "phases": { "new": "pending", "new": "approved" } """)]
    [InlineData(""" "pendingTransition": true """)]
    [InlineData(""" "history": { "action": "start" } """)]
    [InlineData(""" "history": "start" """)]
    public void Deserialize_PhaseFieldWithInvalidValue_ThrowsInvalidKitFileException(string field)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var json = CreateJson(field);

        // Act
        var act = () => serializer.Deserialize<FeatureDocument>(json, FormatIds.Feature, FilePath, false);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(FilePath);
    }

    private static FeatureDocument CreatePhaseStateDocument()
    {
        var jane = new PhaseHistoryActor { Name = "Jane Doe", Email = "jane@example.com" };
        var document = CreateDocument(new FeatureSource
        {
            Kind = FeatureSourceKind.Url,
            Value = "https://github.com/acme/shop/issues/142"
        });

        document.CurrentPhase = WorkflowPhaseNames.Refine;
        document.Phases = new OrderedDictionary<string, PhaseState>
        {
            [WorkflowPhaseNames.New] = PhaseState.Approved,
            [WorkflowPhaseNames.Refine] = PhaseState.AwaitingApproval,
            [WorkflowPhaseNames.Map] = PhaseState.Pending,
            [WorkflowPhaseNames.Plan] = PhaseState.Pending,
            [WorkflowPhaseNames.Implement] = PhaseState.Pending,
            [WorkflowPhaseNames.Verify] = PhaseState.Pending,
            [WorkflowPhaseNames.Finish] = PhaseState.Pending
        };
        document.PendingTransition = WorkflowPhaseNames.Map;
        document.History =
        [
            new PhaseHistoryEntry
            {
                At = new DateTimeOffset(2026, 10, 20, 16, 0, 0, TimeSpan.FromHours(2)),
                By = jane,
                Action = PhaseHistoryAction.Start,
                From = WorkflowPhaseNames.New,
                To = WorkflowPhaseNames.Refine
            },
            new PhaseHistoryEntry
            {
                At = new DateTimeOffset(2026, 10, 20, 16, 5, 0, TimeSpan.FromHours(2)),
                By = new PhaseHistoryActor { Name = "John Roe", Email = "john@example.com" },
                Action = PhaseHistoryAction.Approve,
                From = WorkflowPhaseNames.New,
                To = WorkflowPhaseNames.Refine,
                Note = "approval off"
            },
            new PhaseHistoryEntry
            {
                At = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(2)),
                By = jane,
                Action = PhaseHistoryAction.Start,
                From = WorkflowPhaseNames.Refine,
                To = WorkflowPhaseNames.Map
            }
        ];

        return document;
    }

    private static FeatureDocument CreateDocument(FeatureSource source)
    {
        return new FeatureDocument
        {
            Id = "gh-142-login-sso",
            Title = "Login SSO",
            Profile = WorkflowProfileNames.Feature,
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
