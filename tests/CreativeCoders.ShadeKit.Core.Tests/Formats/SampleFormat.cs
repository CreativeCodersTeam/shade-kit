using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Tests.Formats;

/// <summary>
/// Provides a test-only file type with up to three format versions and its migration chain.
/// </summary>
/// <remarks>
/// Version 1 stores the title in <c>name</c>. Version 2 renames it to <c>title</c>. Version 3 adds <c>mode</c> with
/// the value <c>longer-value</c> if it is missing.
/// </remarks>
public static class SampleFormat
{
    public const string Id = "sample";

    public const string FilePath = "sample.json";

    public static FormatRegistry CreateRegistry(int currentVersion)
    {
        IFormatMigration[] migrations = [new RenameNameToTitleMigration(), new AddModeMigration()];

        return new FormatRegistry(
            [new FormatDefinition(Id, CreateMinEngineVersions(currentVersion), migrations.Take(currentVersion - 1))]);
    }

    /// <summary>
    /// Creates a minimum engine version table that maps every version from 1 to <paramref name="currentVersion" />
    /// to <c>0.&lt;version&gt;.0</c>.
    /// </summary>
    public static Dictionary<int, string> CreateMinEngineVersions(int currentVersion)
    {
        return Enumerable.Range(1, currentVersion).ToDictionary(version => version, version => $"0.{version}.0");
    }

    public sealed class RenameNameToTitleMigration : IFormatMigration
    {
        public string FormatId => Id;

        public int FromVersion => 1;

        public void Migrate(JsonObject document)
        {
            var name = document["name"];

            document.Remove("name");
            document["title"] = name?.DeepClone();
        }
    }

    public sealed class AddModeMigration : IFormatMigration
    {
        public string FormatId => Id;

        public int FromVersion => 2;

        public void Migrate(JsonObject document)
        {
            document.TryAdd("mode", "longer-value");
        }
    }
}

public sealed class NoOpMigration(string formatId, int fromVersion) : IFormatMigration
{
    public string FormatId { get; } = formatId;

    public int FromVersion { get; } = fromVersion;

    public void Migrate(JsonObject document)
    {
    }
}

public sealed class SampleDocument : VersionedDocument
{
    public string? Title { get; set; }

    public SampleMode Mode { get; set; }

    public SampleNested? Nested { get; set; }

    public List<SampleNested>? Items { get; set; }
}

public enum SampleMode
{
    Default,
    LongerValue
}

public sealed class SampleNested
{
    public string? Name { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
