using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Formats;

public class VersionedJsonSerializerTests
{
    private const string UnknownFieldsJson =
        """
        {
          "schemaVersion": 1,
          "minEngineVersion": "0.1.0",
          "title": "Login",
          "mode": "default",
          "futureText": "kept",
          "futureNumber": 1.50,
          "futureFlag": false,
          "futureNull": null,
          "futureList": [1, "two", { "three": 3 }],
          "futureObject": { "deep": { "deeper": [true] } },
          "nested": { "name": "inner", "futureNestedField": { "x": 1 } },
          "items": [ { "name": "first", "futureItemField": "a" }, { "name": "second" } ]
        }
        """;

    [Fact]
    public void Deserialize_CurrentVersion_ReturnsDocumentWithoutWarnings()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var result = serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "title": "Login", "mode": "longer-value" }""",
            SampleFormat.Id, SampleFormat.FilePath, false);

        // Assert
        result.Warnings
            .Should().BeEmpty();

        result.Document.SchemaVersion
            .Should().Be(1);

        result.Document.MinEngineVersion
            .Should().Be("0.1.0");

        result.Document.Title
            .Should().Be("Login");

        result.Document.Mode
            .Should().Be(SampleMode.LongerValue);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC3")]
    public void SerializeAfterDeserialize_UnknownFieldsOnEveryLevel_KeepsEveryFieldAndValue()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var document = serializer.Deserialize<SampleDocument>(UnknownFieldsJson, SampleFormat.Id,
            SampleFormat.FilePath, false).Document;

        var json = serializer.Serialize(document, SampleFormat.Id);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(UnknownFieldsJson))
            .Should().BeTrue($"the written JSON must equal the read JSON, but was:{Environment.NewLine}{json}");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC3")]
    public void SerializeAfterDeserialize_UnknownNumber_KeepsItsTextualRepresentation()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var document = serializer.Deserialize<SampleDocument>(UnknownFieldsJson, SampleFormat.Id,
            SampleFormat.FilePath, false).Document;

        var json = serializer.Serialize(document, SampleFormat.Id);

        // Assert
        json.Should().Contain("\"futureNumber\": 1.50");
    }

    [Theory]
    [Trait("Requirement", "FP01-AC11")]
    [InlineData("{}")]
    [InlineData("""{ "minEngineVersion": "0.1.0", "title": "Login" }""")]
    [InlineData("""{ "schemaVersion": null, "minEngineVersion": "0.1.0" }""")]
    public void Deserialize_MissingSchemaVersion_ThrowsInvalidKitFileException(string json)
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(json, SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        var exception = act.Should().Throw<InvalidKitFileException>().Which;

        exception.FilePath
            .Should().Be(SampleFormat.FilePath);

        exception.Reason
            .Should().Contain("schemaVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"schemaVersion\": 1, ")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"text\"")]
    [InlineData("""{ "schemaVersion": "1", "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": 1.5, "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": 1.0, "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": true, "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": [1], "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": 0, "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": -1, "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": 99999999999, "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": 1, "schemaVersion": 1, "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "nested": { "name": "a", "name": "b" } }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "items": [ { "x": 1, "x": 2 } ] }""")]
    [InlineData("""{ "schemaVersion": 1 }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": null }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": 1 }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "" }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "  " }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "title": 5 }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "mode": "unknown-mode" }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "mode": 1 }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "nested": "text" }""")]
    public void Deserialize_InvalidContent_ThrowsInvalidKitFileException(string json)
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(json, SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.FilePath.Should().Be(SampleFormat.FilePath);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC4")]
    public void Deserialize_NewerVersion_ThrowsFormatTooNewExceptionWithMinEngineVersion()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 2, "minEngineVersion": "2.4.0", "title": "Login" }""",
            SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        var exception = act.Should().Throw<FormatTooNewException>().Which;

        exception.FilePath
            .Should().Be(SampleFormat.FilePath);

        exception.FileVersion
            .Should().Be(2);

        exception.SupportedVersion
            .Should().Be(1);

        exception.MinEngineVersion
            .Should().Be("2.4.0");
    }

    [Theory]
    [Trait("Requirement", "FP01-AC4")]
    [InlineData("""{ "schemaVersion": 5 }""")]
    [InlineData("""{ "schemaVersion": 5, "minEngineVersion": "" }""")]
    [InlineData("""{ "schemaVersion": 5, "minEngineVersion": 7 }""")]
    [InlineData("""{ "schemaVersion": 5, "minEngineVersion": "  " }""")]
    public void Deserialize_NewerVersionWithoutUsableMinEngineVersion_ThrowsFormatTooNewExceptionWithoutMinEngineVersion(
        string json)
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(json, SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        act.Should().Throw<FormatTooNewException>()
            .Which.MinEngineVersion.Should().BeNull();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC4")]
    public void Deserialize_NewerVersionWithFieldsOfUnknownShape_ThrowsFormatTooNewException()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 2, "minEngineVersion": "2.0.0", "title": { "changed": "shape" } }""",
            SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        act.Should().Throw<FormatTooNewException>();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC5")]
    public void Deserialize_OlderVersionWithoutMigration_ThrowsUpgradeNeededException()
    {
        // Arrange
        var serializer = CreateSerializer(2);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "name": "Login" }""",
            SampleFormat.Id, SampleFormat.FilePath, false);

        // Assert
        var exception = act.Should().Throw<UpgradeNeededException>().Which;

        exception.FilePath
            .Should().Be(SampleFormat.FilePath);

        exception.FileVersion
            .Should().Be(1);

        exception.CurrentVersion
            .Should().Be(2);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC6")]
    public void Deserialize_Version1FixtureWithVersion2Step_MigratesInMemoryAndWarns()
    {
        // Arrange
        var serializer = CreateSerializer(2);

        // Act
        var result = serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "name": "Login", "futureField": "kept" }""",
            SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        result.Document.Title
            .Should().Be("Login");

        result.Document.SchemaVersion
            .Should().Be(2);

        result.Document.MinEngineVersion
            .Should().Be("0.2.0");

        result.Document.ExtensionData
            .Should().NotBeNull();

        result.Document.ExtensionData["futureField"].GetString()
            .Should().Be("kept");

        result.Document.ExtensionData
            .Should().NotContainKey("name");

        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, SampleFormat.FilePath, 1, 2));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC6")]
    public void Deserialize_Version1FixtureWithTwoSteps_AppliesStepsInOrder()
    {
        // Arrange
        var serializer = CreateSerializer(3);

        // Act
        var result = serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "name": "Login" }""",
            SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        result.Document.Title
            .Should().Be("Login");

        result.Document.Mode
            .Should().Be(SampleMode.LongerValue);

        result.Document.SchemaVersion
            .Should().Be(3);

        result.Document.MinEngineVersion
            .Should().Be("0.3.0");

        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, SampleFormat.FilePath, 1, 3));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC6")]
    public void Deserialize_Version2FixtureWithThreeVersions_AppliesOnlyRemainingStep()
    {
        // Arrange
        var serializer = CreateSerializer(3);

        // Act
        var result = serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 2, "minEngineVersion": "0.2.0", "title": "Login", "name": "not renamed" }""",
            SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        result.Document.Title
            .Should().Be("Login");

        result.Document.ExtensionData
            .Should().NotBeNull();

        result.Document.ExtensionData["name"].GetString()
            .Should().Be("not renamed");

        result.Document.Mode
            .Should().Be(SampleMode.LongerValue);

        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, SampleFormat.FilePath, 2, 3));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC5")]
    public void Deserialize_Version2FixtureWithThreeVersionsWithoutMigration_ThrowsUpgradeNeededException()
    {
        // Arrange
        var serializer = CreateSerializer(3);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 2, "minEngineVersion": "0.2.0", "title": "Login" }""",
            SampleFormat.Id, SampleFormat.FilePath, false);

        // Assert
        var exception = act.Should().Throw<UpgradeNeededException>().Which;

        exception.FileVersion
            .Should().Be(2);

        exception.CurrentVersion
            .Should().Be(3);
    }

    [Fact]
    public void Deserialize_CurrentVersionWithMigrationAllowed_ReturnsDocumentWithoutWarnings()
    {
        // Arrange
        var serializer = CreateSerializer(2);

        // Act
        var result = serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 2, "minEngineVersion": "0.2.0", "title": "Login", "name": "not renamed" }""",
            SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        result.Warnings
            .Should().BeEmpty();

        result.Document.ExtensionData
            .Should().NotBeNull();

        result.Document.ExtensionData["name"].GetString()
            .Should().Be("not renamed");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Deserialize_OlderVersionWithoutMinEngineVersion_ThrowsInvalidKitFileException(bool migrateOutdated)
    {
        // Arrange
        var serializer = CreateSerializer(2);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "name": "Login" }""", SampleFormat.Id, SampleFormat.FilePath, migrateOutdated);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.Reason.Should().Contain("minEngineVersion");
    }

    [Fact]
    public void Deserialize_MigrationThrows_ThrowsInvalidKitFileExceptionWithInnerException()
    {
        // Arrange
        var failure = new InvalidOperationException("unexpected shape");
        var migration = CreateMigration(_ => throw failure);
        var serializer = CreateSerializer(migration);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""", SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        var exception = act.Should().Throw<InvalidKitFileException>().Which;

        exception.FilePath
            .Should().Be(SampleFormat.FilePath);

        exception.InnerException
            .Should().BeSameAs(failure);
    }

    [Fact]
    public void Deserialize_MigrationProducesUnexpectedValue_ThrowsInvalidKitFileException()
    {
        // Arrange
        var migration = CreateMigration(document => document["mode"] = 1);
        var serializer = CreateSerializer(migration);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""", SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .WithInnerException<JsonException>();
    }

    [Fact]
    public void Deserialize_MigrationWritesVersionFields_EngineVersionFieldsWin()
    {
        // Arrange
        var migration = CreateMigration(document =>
        {
            document["schemaVersion"] = 99;
            document["minEngineVersion"] = "9.9.9";
        });
        var serializer = CreateSerializer(migration);

        // Act
        var result = serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""", SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        result.Document.SchemaVersion
            .Should().Be(2);

        result.Document.MinEngineVersion
            .Should().Be("0.2.0");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC4")]
    public void Deserialize_MaximumIntegerVersion_ThrowsFormatTooNewException()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 2147483647, "minEngineVersion": "9.0.0" }""",
            SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        act.Should().Throw<FormatTooNewException>()
            .Which.FileVersion.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Deserialize_InvalidJson_KeepsReaderExceptionAsInnerException()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>("{ broken", SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .WithInnerException<JsonException>();
    }

    [Fact]
    public void Deserialize_NullFormatId_ThrowsArgumentNullException()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""", null!, SampleFormat.FilePath, true);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formatId");
    }

    [Fact]
    public void Deserialize_UnknownFormatId_ThrowsArgumentException()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""", "unknown", SampleFormat.FilePath, true);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("formatId");
    }

    [Fact]
    public void Deserialize_NullJson_ThrowsArgumentNullException()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(null!, SampleFormat.Id, SampleFormat.FilePath, true);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("json");
    }

    [Fact]
    public void Deserialize_NullFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Deserialize<SampleDocument>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""", SampleFormat.Id, null!, true);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("filePath");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC9")]
    public void Serialize_DocumentWithOtherVersionValues_StampsCurrentVersionAndItsMinEngineVersion()
    {
        // Arrange
        var serializer = CreateSerializer(2);
        var document = new SampleDocument { SchemaVersion = 7, MinEngineVersion = "9.9.9", Title = "Login" };

        // Act
        var json = serializer.Serialize(document, SampleFormat.Id);

        // Assert
        var root = JsonNode.Parse(json);

        root
            .Should().NotBeNull();

        var schemaVersionNode = root["schemaVersion"];

        schemaVersionNode
            .Should().NotBeNull();

        schemaVersionNode.GetValue<int>()
            .Should().Be(2);

        var minEngineVersionNode = root["minEngineVersion"];

        minEngineVersionNode
            .Should().NotBeNull();

        minEngineVersionNode.GetValue<string>()
            .Should().Be("0.2.0");
    }

    [Fact]
    public void Serialize_Document_DoesNotChangeTheDocument()
    {
        // Arrange
        var serializer = CreateSerializer(1);
        var document = new SampleDocument { SchemaVersion = 7, MinEngineVersion = "9.9.9" };

        // Act
        serializer.Serialize(document, SampleFormat.Id);

        // Assert
        document.SchemaVersion
            .Should().Be(7);

        document.MinEngineVersion
            .Should().Be("9.9.9");
    }

    [Fact]
    public void Serialize_Document_WritesTwoSpaceIndentedLfTextEndingWithLineBreak()
    {
        // Arrange
        var serializer = CreateSerializer(1);
        var document = new SampleDocument { Title = "Login", Nested = new SampleNested { Name = "inner" } };

        // Act
        var json = serializer.Serialize(document, SampleFormat.Id);

        // Assert
        json.Should().Be(
            "{\n" +
            "  \"schemaVersion\": 1,\n" +
            "  \"minEngineVersion\": \"0.1.0\",\n" +
            "  \"title\": \"Login\",\n" +
            "  \"mode\": \"default\",\n" +
            "  \"nested\": {\n" +
            "    \"name\": \"inner\"\n" +
            "  }\n" +
            "}\n");
    }

    [Fact]
    public void Serialize_DocumentWithUnknownFields_WritesVersionFieldsFirstAndUnknownFieldsLast()
    {
        // Arrange
        var serializer = CreateSerializer(1);
        var document = serializer.Deserialize<SampleDocument>(
            """{ "zeta": 1, "title": "Login", "minEngineVersion": "0.1.0", "alpha": 2, "schemaVersion": 1 }""",
            SampleFormat.Id, SampleFormat.FilePath, false).Document;

        // Act
        var json = serializer.Serialize(document, SampleFormat.Id);

        // Assert
        var root = JsonNode.Parse(json);

        root
            .Should().NotBeNull();

        root.AsObject().Select(x => x.Key)
            .Should().Equal("schemaVersion", "minEngineVersion", "title", "mode", "zeta", "alpha");
    }

    [Fact]
    public void Serialize_TextWithNonAsciiAndHtmlCharacters_WritesThemUnescaped()
    {
        // Arrange
        var serializer = CreateSerializer(1);
        var document = new SampleDocument { Title = "Grüße <b> & 'quotes'" };

        // Act
        var json = serializer.Serialize(document, SampleFormat.Id);

        // Assert
        json.Should().Contain("\"title\": \"Grüße <b> & 'quotes'\"");
    }

    [Fact]
    public void Serialize_EnumValue_WritesKebabCaseText()
    {
        // Arrange
        var serializer = CreateSerializer(1);
        var document = new SampleDocument { Mode = SampleMode.LongerValue };

        // Act
        var json = serializer.Serialize(document, SampleFormat.Id);

        // Assert
        json.Should().Contain("\"mode\": \"longer-value\"");
    }

    [Fact]
    public void Serialize_NullKnownFields_OmitsThem()
    {
        // Arrange
        var serializer = CreateSerializer(1);
        var document = new SampleDocument();

        // Act
        var json = serializer.Serialize(document, SampleFormat.Id);

        // Assert
        var root = JsonNode.Parse(json);

        root
            .Should().NotBeNull();

        root.AsObject().Select(x => x.Key)
            .Should().Equal("schemaVersion", "minEngineVersion", "mode");
    }

    [Fact]
    public void Serialize_DerivedDocumentTypedAsBase_WritesDerivedFields()
    {
        // Arrange
        var serializer = CreateSerializer(1);
        VersionedDocument document = new SampleDocument { Title = "Login" };

        // Act
        var json = serializer.Serialize(document, SampleFormat.Id);

        // Assert
        var root = JsonNode.Parse(json);

        root
            .Should().NotBeNull();

        var titleNode = root["title"];

        titleNode
            .Should().NotBeNull();

        titleNode.GetValue<string>()
            .Should().Be("Login");
    }

    [Fact]
    public void Serialize_NullFormatId_ThrowsArgumentNullException()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Serialize(new SampleDocument(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formatId");
    }

    [Fact]
    public void Serialize_UnknownFormatId_ThrowsArgumentException()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Serialize(new SampleDocument(), "unknown");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("formatId");
    }

    [Fact]
    public void Serialize_NullDocument_ThrowsArgumentNullException()
    {
        // Arrange
        var serializer = CreateSerializer(1);

        // Act
        var act = () => serializer.Serialize<SampleDocument>(null!, SampleFormat.Id);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("document");
    }

    [Fact]
    public void Ctor_NullRegistry_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionedJsonSerializer(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formatRegistry");
    }

    private static VersionedJsonSerializer CreateSerializer(int currentVersion)
    {
        return new VersionedJsonSerializer(SampleFormat.CreateRegistry(currentVersion));
    }

    private static VersionedJsonSerializer CreateSerializer(IFormatMigration migration)
    {
        return new VersionedJsonSerializer(new FormatRegistry(
            [new FormatDefinition(SampleFormat.Id, SampleFormat.CreateMinEngineVersions(2), [migration])]));
    }

    private static IFormatMigration CreateMigration(Action<JsonObject> migrate)
    {
        var migration = A.Fake<IFormatMigration>();
        A.CallTo(() => migration.FormatId).Returns(SampleFormat.Id);
        A.CallTo(() => migration.FromVersion).Returns(1);
        A.CallTo(() => migration.Migrate(A<JsonObject>._)).Invokes(migrate);

        return migration;
    }
}
