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
              "defaultProfile": "feature",
              "outdatedFormatOnRead": "stop",
              "trackers": { "github.com": "gh" },
              "report": "ask",
              "laterPackageThreshold": 80.0,
              "laterPackageSettings": { "github.com": [ "gh", null, true ] }
            }
            """;

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>(original, FormatIds.Config, FilePath, false).Document;
        var json = serializer.Serialize(config, FormatIds.Config);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(original))
            .Should().BeTrue($"the written config must keep every field, but was:{Environment.NewLine}{json}");

        json.Should().Contain("\"laterPackageThreshold\": 80.0");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC3")]
    public void SerializeAfterDeserialize_KnownFieldsChanged_KeepsUnknownFields()
    {
        // Arrange
        const string original =
            """
            {
              "schemaVersion": 1,
              "minEngineVersion": "0.1.0",
              "kitVersion": "1.0.0",
              "laterPackageSettings": { "mode": "strict", "limit": 1.50 }
            }
            """;

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var config = serializer.Deserialize<KitConfig>(original, FormatIds.Config, FilePath, false).Document;

        config.KitVersion = "2.0.0";
        config.Report = ReportPolicy.Never;

        // Act
        var json = serializer.Serialize(config, FormatIds.Config);

        // Assert
        var written = JsonNode.Parse(json);

        written
            .Should().NotBeNull();

        var kitVersionNode = written["kitVersion"];

        kitVersionNode
            .Should().NotBeNull();

        kitVersionNode.GetValue<string>()
            .Should().Be("2.0.0");

        var reportNode = written["report"];

        reportNode
            .Should().NotBeNull();

        reportNode.GetValue<string>()
            .Should().Be("never");

        json.Should().Contain("\"laterPackageSettings\": {\n    \"mode\": \"strict\",\n    \"limit\": 1.50\n  }");
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
        var root = JsonNode.Parse(json);

        root
            .Should().NotBeNull();

        var outdatedFormatOnReadNode = root["outdatedFormatOnRead"];

        outdatedFormatOnReadNode
            .Should().NotBeNull();

        outdatedFormatOnReadNode.GetValue<string>()
            .Should().Be("stop");
    }

    [Fact]
    public void Serialize_InitContent_WritesV1FieldsInContractOrder()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var config = new KitConfig
        {
            KitVersion = "1.2.3",
            DefaultProfile = "feature",
            OutdatedFormatOnRead = OutdatedFormatOnReadPolicy.MigrateAndWarn,
            Trackers = new Dictionary<string, string> { ["github.com"] = "gh" },
            Report = ReportPolicy.Ask
        };

        // Act
        var json = serializer.Serialize(config, FormatIds.Config);

        // Assert
        json
            .Should().Be(
                """
                {
                  "schemaVersion": 1,
                  "minEngineVersion": "0.1.0",
                  "kitVersion": "1.2.3",
                  "defaultProfile": "feature",
                  "outdatedFormatOnRead": "migrate-and-warn",
                  "trackers": {
                    "github.com": "gh"
                  },
                  "report": "ask"
                }

                """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Serialize_AllV1FieldsSet_WritesCoverageThresholdLast()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var config = new KitConfig
        {
            KitVersion = "1.2.3",
            Trackers = new Dictionary<string, string> { ["github.com"] = "gh" },
            Report = ReportPolicy.Never,
            CoverageThreshold = 80.5
        };

        // Act
        var json = serializer.Serialize(config, FormatIds.Config);

        // Assert
        var root = JsonNode.Parse(json);

        root
            .Should().NotBeNull();

        root.AsObject().Select(field => field.Key)
            .Should().Equal("schemaVersion", "minEngineVersion", "kitVersion", "defaultProfile",
                "outdatedFormatOnRead", "trackers", "report", "coverageThreshold");
    }

    [Fact]
    public void Serialize_DefaultConfig_WritesDefaultsAndOmitsOptionalFields()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var config = new KitConfig();

        // Act
        var json = serializer.Serialize(config, FormatIds.Config);

        // Assert
        var root = JsonNode.Parse(json);

        root
            .Should().NotBeNull();

        root.AsObject().Select(field => field.Key)
            .Should().Equal("schemaVersion", "minEngineVersion", "defaultProfile", "outdatedFormatOnRead", "trackers",
                "report");
    }

    [Fact]
    public void Deserialize_OnlyVersionFields_UsesDefaults()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0" }""",
            FormatIds.Config, FilePath, false).Document;

        // Assert
        config.KitVersion
            .Should().BeNull();

        config.DefaultProfile
            .Should().Be("feature");

        config.Trackers
            .Should().Equal(new Dictionary<string, string> { ["github.com"] = "gh" });

        config.Report
            .Should().Be(ReportPolicy.Ask);

        config.CoverageThreshold
            .Should().BeNull();

        config.ExtensionData
            .Should().BeNull();
    }

    [Fact]
    public void Deserialize_AllV1Fields_ReadsValues()
    {
        // Arrange
        const string json =
            """
            {
              "schemaVersion": 1,
              "minEngineVersion": "0.1.0",
              "kitVersion": "1.4.0",
              "defaultProfile": "bugfix",
              "outdatedFormatOnRead": "stop",
              "trackers": { "github.com": "gh", "jira.example.com": "proj" },
              "report": "always",
              "coverageThreshold": 75.5
            }
            """;

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>(json, FormatIds.Config, FilePath, false).Document;

        // Assert
        config.KitVersion
            .Should().Be("1.4.0");

        config.DefaultProfile
            .Should().Be("bugfix");

        config.OutdatedFormatOnRead
            .Should().Be(OutdatedFormatOnReadPolicy.Stop);

        config.Trackers
            .Should().BeEquivalentTo(new Dictionary<string, string>
            {
                ["github.com"] = "gh",
                ["jira.example.com"] = "proj"
            });

        config.Report
            .Should().Be(ReportPolicy.Always);

        config.CoverageThreshold
            .Should().Be(75.5);

        config.ExtensionData
            .Should().BeNull();
    }

    [Fact]
    public void SerializeAfterDeserialize_TrackerHostsWithUpperCaseAndDots_KeepsKeysVerbatim()
    {
        // Arrange
        const string original =
            """
            {
              "schemaVersion": 1,
              "minEngineVersion": "0.1.0",
              "trackers": { "GitHub.com": "gh", "Jira.Example.com": "proj" }
            }
            """;

        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>(original, FormatIds.Config, FilePath, false).Document;
        var json = serializer.Serialize(config, FormatIds.Config);

        // Assert
        var root = JsonNode.Parse(json);

        root
            .Should().NotBeNull();

        var trackersNode = root["trackers"];

        trackersNode
            .Should().NotBeNull();

        trackersNode.AsObject().Select(tracker => tracker.Key)
            .Should().Equal("GitHub.com", "Jira.Example.com");
    }

    [Theory]
    [InlineData(""", "report": "ask" """, ReportPolicy.Ask)]
    [InlineData(""", "report": "always" """, ReportPolicy.Always)]
    [InlineData(""", "report": "never" """, ReportPolicy.Never)]
    [InlineData(""", "report": "Always" """, ReportPolicy.Always)]
    [InlineData(""", "report": "NEVER" """, ReportPolicy.Never)]
    [InlineData("", ReportPolicy.Ask)]
    public void Deserialize_Report_ReadsPolicy(string reportField, ReportPolicy expected)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0"{{reportField}} }""", FormatIds.Config, FilePath,
            false).Document;

        // Assert
        config.Report
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ReportPolicy.Ask, "ask")]
    [InlineData(ReportPolicy.Always, "always")]
    [InlineData(ReportPolicy.Never, "never")]
    public void Serialize_Report_WritesKebabCaseValue(ReportPolicy report, string expected)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());
        var config = new KitConfig { Report = report };

        // Act
        var json = serializer.Serialize(config, FormatIds.Config);

        // Assert
        var root = JsonNode.Parse(json);

        root
            .Should().NotBeNull();

        var reportNode = root["report"];

        reportNode
            .Should().NotBeNull();

        reportNode.GetValue<string>()
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("\"sometimes\"")]
    [InlineData("\"\"")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("null")]
    [InlineData("true")]
    public void Deserialize_InvalidReport_ThrowsInvalidKitFileException(string value)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var act = () => serializer.Deserialize<KitConfig>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "report": {{value}} }""",
            FormatIds.Config, FilePath, false);

        // Assert
        act.Should().Throw<InvalidKitFileException>();
    }

    [Theory]
    [InlineData(""" "defaultProfile": null """)]
    [InlineData(""" "trackers": null """)]
    [InlineData(""" "defaultProfile": 1 """)]
    [InlineData(""" "kitVersion": 1 """)]
    [InlineData(""" "trackers": [ "github.com" ] """)]
    [InlineData(""" "trackers": { "github.com": 1 } """)]
    [InlineData(""" "coverageThreshold": "80" """)]
    [InlineData(""" "coverageThreshold": 1e400 """)]
    [InlineData(""" "coverageThreshold": -1e400 """)]
    [InlineData(""" "trackers": { "github.com": "gh", "github.com": "gh2" } """)]
    public void Deserialize_V1FieldWithInvalidValue_ThrowsInvalidKitFileException(string field)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var act = () => serializer.Deserialize<KitConfig>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", {{field}} }""", FormatIds.Config, FilePath,
            false);

        // Assert
        act.Should().Throw<InvalidKitFileException>();
    }

    [Theory]
    [InlineData(""" "kitVersion": null """)]
    [InlineData(""" "coverageThreshold": null """)]
    public void Deserialize_OptionalFieldNull_ReadsNull(string field)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", {{field}} }""", FormatIds.Config, FilePath,
            false).Document;

        // Assert
        new object?[] { config.KitVersion, config.CoverageThreshold }
            .Should().AllSatisfy(value => value.Should().BeNull());
    }

    [Fact]
    public void Deserialize_TrackerWithNullPrefix_ThrowsInvalidKitFileException()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var act = () => serializer.Deserialize<KitConfig>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "trackers": { "github.com": null } }""",
            FormatIds.Config, FilePath, false);

        // Assert
        act.Should().Throw<InvalidKitFileException>();
    }

    [Theory]
    [InlineData("GH")]
    [InlineData("gh_")]
    [InlineData("")]
    [InlineData("-gh")]
    [InlineData("gh-")]
    [InlineData("g--h")]
    [InlineData(" gh")]
    public void Deserialize_TrackerWithInvalidPrefix_ThrowsInvalidKitFileExceptionNamingHostAndPrefix(string prefix)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var act = () => serializer.Deserialize<KitConfig>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "trackers": { "github.com": "{{prefix}}" } }""",
            FormatIds.Config, FilePath, false);

        // Assert
        var exception = act.Should().Throw<InvalidKitFileException>().Which;

        exception.FilePath
            .Should().Be(FilePath);

        exception.Reason
            .Should().Contain(
                $"The id prefix '{prefix}' of host 'github.com' in the 'trackers' field must consist of lowercase " +
                "letters a-z and digits joined by single hyphens.");
    }

    [Fact]
    public void Deserialize_InvalidPrefixAfterValidPrefix_ThrowsInvalidKitFileExceptionNamingInvalidHost()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var act = () => serializer.Deserialize<KitConfig>(
            """
            { "schemaVersion": 1, "minEngineVersion": "0.1.0",
              "trackers": { "github.com": "gh", "jira.example.com": "PROJ" } }
            """,
            FormatIds.Config, FilePath, false);

        // Assert
        act.Should().Throw<InvalidKitFileException>()
            .Which.Reason
            .Should().Contain("The id prefix 'PROJ' of host 'jira.example.com'");
    }

    [Theory]
    [InlineData("gh")]
    [InlineData("gl")]
    [InlineData("jira-1")]
    [InlineData("a1")]
    public void Deserialize_TrackerWithValidPrefix_ReadsPrefix(string prefix)
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>(
            $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "trackers": { "github.com": "{{prefix}}" } }""",
            FormatIds.Config, FilePath, false).Document;

        // Assert
        config.Trackers
            .Should().Equal(new Dictionary<string, string> { ["github.com"] = prefix });
    }

    [Fact]
    public void Deserialize_EmptyTrackers_ReadsEmptyMapping()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "trackers": {} }""", FormatIds.Config, FilePath,
            false).Document;

        // Assert
        config.Trackers
            .Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_TrackersWithoutGitHub_ReadsOnlyTheGivenHosts()
    {
        // Arrange
        var serializer = new VersionedJsonSerializer(FormatRegistry.CreateDefault());

        // Act
        var config = serializer.Deserialize<KitConfig>(
            """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "trackers": { "jira.example.com": "jira" } }""",
            FormatIds.Config, FilePath, false).Document;

        // Assert
        config.Trackers
            .Should().Equal(new Dictionary<string, string> { ["jira.example.com"] = "jira" });
    }

    [Fact]
    public void Constructor_TwoInstances_DoNotShareTheDefaultTrackers()
    {
        // Arrange
        var first = new KitConfig();
        var second = new KitConfig();

        // Act
        first.Trackers["jira.example.com"] = "jira";

        // Assert
        second.Trackers
            .Should().Equal(new Dictionary<string, string> { ["github.com"] = "gh" });
    }
}
