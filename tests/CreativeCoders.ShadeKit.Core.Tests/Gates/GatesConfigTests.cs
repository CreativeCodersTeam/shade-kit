using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Templates;

namespace CreativeCoders.ShadeKit.Core.Tests.Gates;

public class GatesConfigTests
{
    private const string FilePath = ".shadekit/gates.json";

    /// <summary>
    /// The default <c>gates.json</c> of feature package 05, as the serializer writes it.
    /// </summary>
    internal const string DefaultGatesJsonBody =
        """
          "profiles": {
            "feature": {
              "refine": {
                "approval": true
              },
              "map": {
                "approval": true
              },
              "plan": {
                "approval": true,
                "checklist": "checks/definition-of-ready.md"
              },
              "implement": {
                "approval": true
              },
              "verify": {
                "approval": true
              },
              "finish": {
                "approval": true,
                "checklist": "checks/definition-of-done.md"
              }
            },
            "bugfix": {
              "diagnose": {
                "approval": true
              },
              "plan": {
                "approval": true,
                "checklist": "checks/definition-of-ready.md"
              },
              "implement": {
                "approval": true
              },
              "verify": {
                "approval": true
              },
              "finish": {
                "approval": true,
                "checklist": "checks/definition-of-done.md"
              }
            },
            "quick": {
              "quick": {
                "approval": false
              },
              "finish": {
                "approval": true,
                "checklist": "checks/definition-of-done.md"
              }
            }
          }
        }

        """;

    private static readonly VersionedJsonSerializer Serializer =
        new VersionedJsonSerializer(FormatRegistry.CreateDefault());

    public static TheoryData<string> InvalidChecklists { get; } = new()
    {
        "",
        " ",
        "definition-of-ready.md",
        "/checks/definition-of-ready.md",
        "./checks/definition-of-ready.md",
        "../checks/definition-of-ready.md",
        "templates/checks/definition-of-ready.md",
        "C:/checks/definition-of-ready.md",
        "Checks/definition-of-ready.md",
        " checks/definition-of-ready.md",
        "checks\\definition-of-ready.md",
        "checks/team\\definition-of-ready.md",
        "checks//definition-of-ready.md",
        "checks/./definition-of-ready.md",
        "checks/../definition-of-ready.md",
        "checks/team/../definition-of-ready.md",
        "checks/..",
        "checks/definition-of-ready.md/",
        "checks/definition-of-ready.txt",
        "checks/definition-of-ready.MD",
        "checks/definition-of-ready",
        "checks/",
        "checks/.md",
        "checks/team/.md",
        "checks/x.md:hidden.md",
        "checks/team:x/definition-of-ready.md",
        "checks/x\u001b]0;title\u0007.md",
        "checks/x\t.md",
        "checks/x\n.md",
        "checks/x\u007f.md",
        "checks/x\u0085.md"
    };

    [Fact]
    public void Serialize_NewGatesConfig_WritesOnlySchemaVersionAndMinEngineVersion()
    {
        // Act
        var json = Serializer.Serialize(new GatesConfig(), FormatIds.Gates);

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
    [Trait("Requirement", "FP05-AC1")]
    public void Serialize_DefaultGatesConfig_WritesPackageDefaultsInPackageOrderWithoutNullChecklists()
    {
        // Act
        var json = Serializer.Serialize(GatesConfig.CreateDefault(), FormatIds.Gates);

        // Assert
        json
            .Should().Be(
                ("{\n" +
                 "  \"schemaVersion\": 1,\n" +
                 "  \"minEngineVersion\": \"0.1.0\",\n" +
                 DefaultGatesJsonBody).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void CreateDefault_Checklists_AreShippedChecklistTemplates()
    {
        // Arrange
        var shippedRepositoryPaths = ShippedTemplates.All.Select(x => x.RepositoryPath).ToArray();

        // Act
        var checklists = GatesConfig.CreateDefault().Profiles!.Values
            .SelectMany(gates => gates.Values)
            .Select(gate => gate.Checklist)
            .OfType<string>()
            .Distinct()
            .ToArray();

        // Assert
        checklists
            .Should().BeEquivalentTo(GatesConfig.DefinitionOfReadyChecklist, GatesConfig.DefinitionOfDoneChecklist);

        checklists
            .Should().AllSatisfy(checklist => shippedRepositoryPaths
                .Should().Contain(".shadekit/templates/" + checklist));
    }

    [Fact]
    public void CreateDefault_CalledTwice_ReturnsIndependentConfigurations()
    {
        // Arrange
        var first = GatesConfig.CreateDefault();

        // Act
        first.Profiles!["feature"]["plan"].Approval = false;
        first.Profiles.Remove("quick");
        var second = GatesConfig.CreateDefault();

        // Assert
        second.Profiles!["feature"]["plan"].Approval
            .Should().BeTrue();

        second.Profiles
            .Should().ContainKey("quick");
    }

    [Fact]
    public void SerializeAfterDeserialize_DefaultGatesFile_WritesItUnchanged()
    {
        // Arrange
        var original = Serializer.Serialize(GatesConfig.CreateDefault(), FormatIds.Gates);

        // Act
        var gates = Serializer.Deserialize<GatesConfig>(original, FormatIds.Gates, FilePath, false).Document;
        var json = Serializer.Serialize(gates, FormatIds.Gates);

        // Assert
        json
            .Should().Be(original);
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

        // Act
        var gates = Serializer.Deserialize<GatesConfig>(original, FormatIds.Gates, FilePath, false).Document;
        var json = Serializer.Serialize(gates, FormatIds.Gates);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(original))
            .Should().BeTrue($"the written gates file must keep every field, but was:{Environment.NewLine}{json}");

        json.Should().Contain("\"threshold\": 80.0");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC3")]
    public void SerializeAfterDeserialize_UnknownFieldsProfilesAndPhasesAtEveryLevel_KeepsThemUnchanged()
    {
        // Arrange
        const string original =
            """
            {
              "schemaVersion": 1,
              "minEngineVersion": "0.1.0",
              "profiles": {
                "feature": {
                  "plan": {
                    "approval": true,
                    "checklist": "checks/definition-of-ready.md",
                    "commands": [ "dotnet build", null, { "timeout": 60 } ]
                  },
                  "review": { "approval": false, "owner": "team-a" }
                },
                "spike": {
                  "explore": { "approval": false, "checklist": "checks/team/explore.md" }
                }
              },
              "threshold": 80.0
            }
            """;

        // Act
        var gates = Serializer.Deserialize<GatesConfig>(original, FormatIds.Gates, FilePath, false).Document;
        var json = Serializer.Serialize(gates, FormatIds.Gates);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(original))
            .Should().BeTrue($"the written gates file must keep every field, but was:{Environment.NewLine}{json}");

        gates.Profiles!.Keys
            .Should().Equal("feature", "spike");

        gates.Profiles["feature"].Keys
            .Should().Equal("plan", "review");

        gates.Profiles["feature"]["plan"].ExtensionData
            .Should().ContainKey("commands");

        gates.Profiles["spike"]["explore"].Checklist
            .Should().Be("checks/team/explore.md");
    }

    [Fact]
    public void Deserialize_GateWithoutApproval_ApprovalIsTrue()
    {
        // Act
        var gates = Deserialize("""{ "feature": { "plan": { "checklist": "checks/definition-of-ready.md" } } }""");

        // Assert
        var gate = gates.Profiles!["feature"]["plan"];

        gate.Approval
            .Should().BeTrue();

        gate.Checklist
            .Should().Be("checks/definition-of-ready.md");
    }

    [Fact]
    public void Deserialize_EmptyGate_ApprovalIsTrueAndChecklistIsNull()
    {
        // Act
        var gates = Deserialize("""{ "feature": { "refine": {} } }""");

        // Assert
        var gate = gates.Profiles!["feature"]["refine"];

        gate.Approval
            .Should().BeTrue();

        gate.Checklist
            .Should().BeNull();

        gate.ExtensionData
            .Should().BeNull();
    }

    [Fact]
    public void Deserialize_ApprovalFalse_ApprovalIsFalse()
    {
        // Act
        var gates = Deserialize("""{ "quick": { "quick": { "approval": false } } }""");

        // Assert
        gates.Profiles!["quick"]["quick"].Approval
            .Should().BeFalse();
    }

    [Fact]
    public void SerializeAfterDeserialize_NullChecklist_ChecklistIsNullAndNotWritten()
    {
        // Arrange
        var gates = Deserialize("""{ "feature": { "plan": { "approval": true, "checklist": null } } }""");

        // Act
        var json = Serializer.Serialize(gates, FormatIds.Gates);

        // Assert
        gates.Profiles!["feature"]["plan"].Checklist
            .Should().BeNull();

        json
            .Should().NotContain("checklist");
    }

    [Fact]
    public void Deserialize_MissingProfiles_ProfilesIsNull()
    {
        // Act
        var gates = Serializer.Deserialize<GatesConfig>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""", FormatIds.Gates, FilePath, false).Document;

        // Assert
        gates.Profiles
            .Should().BeNull();
    }

    [Fact]
    public void SerializeAfterDeserialize_EmptyProfiles_ProfilesIsEmptyAndWrittenAsEmptyObject()
    {
        // Arrange
        var gates = Deserialize("{}");

        // Act
        var json = Serializer.Serialize(gates, FormatIds.Gates);

        // Assert
        gates.Profiles
            .Should().NotBeNull()
            .And.BeEmpty();

        JsonNode.Parse(json)!["profiles"]
            .Should().BeOfType<JsonObject>()
            .Which.Count
            .Should().Be(0);
    }

    [Fact]
    public void Deserialize_EmptyProfile_ProfileHasNoGates()
    {
        // Act
        var gates = Deserialize("""{ "quick": {} }""");

        // Assert
        gates.Profiles!["quick"]
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("checks/definition-of-ready.md")]
    [InlineData("checks/definition-of-done.md")]
    [InlineData("checks/verify.md")]
    [InlineData("checks/team/security.md")]
    [InlineData("checks/x.md")]
    public void Deserialize_ValidChecklist_KeepsChecklist(string checklist)
    {
        // Act
        var gates = Deserialize($$"""{ "feature": { "verify": { "checklist": {{JsonSerializer.Serialize(checklist)}} } } }""");

        // Assert
        gates.Profiles!["feature"]["verify"].Checklist
            .Should().Be(checklist);
    }

    [Theory]
    [MemberData(nameof(InvalidChecklists))]
    public void Deserialize_InvalidChecklist_ThrowsInvalidKitFileExceptionNamingGate(string checklist)
    {
        // Arrange
        var profiles = $$"""{ "bugfix": { "plan": { "checklist": {{JsonSerializer.Serialize(checklist)}} } } }""";

        // Act
        var act = () => Deserialize(profiles);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.Message
            .Should().Contain("'plan'")
            .And.Contain("'bugfix'")
            .And.Contain("checks/");
    }

    [Fact]
    public void Deserialize_ChecklistWithControlCharacters_ThrowsInvalidKitFileExceptionWithEscapedCharacters()
    {
        // Act
        var act = () => Deserialize("""{ "feature": { "plan": { "checklist": "checks/x\u001b]0;t\u0007.md" } } }""");

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.Message
            .Should().Contain(@"'checks/x\u001b]0;t\u0007.md'")
            .And.NotContain("\u001b")
            .And.NotContain("\u0007");
    }

    [Theory]
    [InlineData("""{ "feat\u001bure": { } }""", @"'feat\u001bure'")]
    [InlineData("""{ "release\n": { "ship": { } } }""", @"'release\u000a'")]
    [InlineData("""{ "\u0000": { } }""", @"'\u0000'")]
    public void Deserialize_ProfileNameWithControlCharacter_ThrowsInvalidKitFileExceptionWithEscapedName(
        string profiles, string expectedName)
    {
        // Act
        var act = () => Deserialize(profiles);

        // Assert
        var message = act.Should().Throw<InvalidKitFileException>().Which.Message;

        message
            .Should().Contain($"The profile name {expectedName}")
            .And.Contain("control character");

        message.Any(char.IsControl)
            .Should().BeFalse("the message must not carry a control character to the terminal");
    }

    [Theory]
    [InlineData("""{ "feature": { "pl\u001ban": { } } }""", @"'pl\u001ban'")]
    [InlineData("""{ "release": { "ship\r": { "approval": false } } }""", @"'ship\u000d'")]
    [InlineData("""{ "feature": { "plan": { }, "\t": null } }""", @"'\u0009'")]
    public void Deserialize_PhaseNameWithControlCharacter_ThrowsInvalidKitFileExceptionWithEscapedName(
        string profiles, string expectedName)
    {
        // Act
        var act = () => Deserialize(profiles);

        // Assert
        var message = act.Should().Throw<InvalidKitFileException>().Which.Message;

        message
            .Should().Contain($"The phase name {expectedName}")
            .And.Contain("control character");

        message.Any(char.IsControl)
            .Should().BeFalse("the message must not carry a control character to the terminal");
    }

    [Theory]
    [InlineData("""{ "feature": null }""", "'feature'")]
    [InlineData("""{ "feature": { "plan": null } }""", "'plan'")]
    public void Deserialize_NullProfileOrGate_ThrowsInvalidKitFileExceptionNamingEntry(string profiles,
        string expectedName)
    {
        // Act
        var act = () => Deserialize(profiles);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.Message
            .Should().Contain(expectedName)
            .And.Contain("null");
    }

    [Fact]
    public void Deserialize_NullProfiles_ThrowsInvalidKitFileException()
    {
        // Act
        var act = () => Deserialize("null");

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.Message
            .Should().Contain("'profiles'");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"feature\"")]
    [InlineData("""{ "feature": [] }""")]
    [InlineData("""{ "feature": { "plan": true } }""")]
    [InlineData("""{ "feature": { "plan": { "approval": null } } }""")]
    [InlineData("""{ "feature": { "plan": { "approval": "yes" } } }""")]
    [InlineData("""{ "feature": { "plan": { "approval": 1 } } }""")]
    [InlineData("""{ "feature": { "plan": { "checklist": 1 } } }""")]
    [InlineData("""{ "feature": { "plan": { "checklist": false } } }""")]
    [InlineData("""{ "feature": { "plan": { "approval": true }, "plan": { "approval": false } } }""")]
    [InlineData("""{ "feature": { "plan": {} }, "feature": { "refine": {} } }""")]
    [InlineData("""{ "feature": { "plan": { "approval": true, "approval": false } } }""")]
    public void Deserialize_UnexpectedTypeOrDuplicateKey_ThrowsInvalidKitFileException(string profiles)
    {
        // Act
        var act = () => Deserialize(profiles);

        // Assert
        act.Should().Throw<InvalidKitFileException>();
    }

    [Theory]
    [InlineData("""{ "feature": { "refine": {}, "plan": {} }, "bugfix": { "diagnose": {}, "plan": null } }""",
        "'plan'", "'bugfix'")]
    [InlineData("""{ "feature": {}, "bugfix": { "plan": {} }, "quick": null }""", "'quick'", "null")]
    [InlineData(
        """{ "feature": { "plan": { "checklist": "checks/definition-of-ready.md" }, "finish": { "checklist": "checks/x.txt" } } }""",
        "'finish'", "checks/x.txt")]
    public void Deserialize_InvalidEntryAfterValidEntries_ThrowsInvalidKitFileExceptionNamingIt(string profiles,
        string expectedName, string expectedDetail)
    {
        // Act
        var act = () => Deserialize(profiles);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.Message
            .Should().Contain(expectedName)
            .And.Contain(expectedDetail);
    }

    [Fact]
    public void Deserialize_ApprovalFieldInOtherCase_KeepsItAsUnknownFieldAndApprovalIsTrue()
    {
        // Act
        var gates = Deserialize("""{ "quick": { "quick": { "Approval": false } } }""");

        // Assert
        var gate = gates.Profiles!["quick"]["quick"];

        gate.Approval
            .Should().BeTrue("field names are matched case-sensitively, like in every SHADE kit file");

        gate.ExtensionData
            .Should().ContainKey("Approval");
    }

    private static GatesConfig Deserialize(string profiles)
    {
        return Serializer.Deserialize<GatesConfig>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "profiles": {{profiles}} }""", FormatIds.Gates,
            FilePath, false).Document;
    }
}
