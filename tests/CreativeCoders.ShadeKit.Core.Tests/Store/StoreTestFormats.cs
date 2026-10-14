using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Tests.Formats;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

/// <summary>
/// Provides registries and file contents for store tests: the test-only sample file type and a config file type
/// whose current version can be raised to produce an outdated <c>config.json</c>.
/// </summary>
public static class StoreTestFormats
{
    public const string SampleV1 = """{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "name": "Login" }""";

    public const string SampleV2 = """{ "schemaVersion": 2, "minEngineVersion": "0.2.0", "title": "Login" }""";

    public static FormatRegistry CreateRegistry(int sampleVersion = 2, int configVersion = 1)
    {
        var configMigrations = Enumerable.Range(1, configVersion - 1)
            .Select(version => new NoOpMigration(FormatIds.Config, version));

        return new FormatRegistry(
        [
            SampleFormat.CreateRegistry(sampleVersion).GetFormat(SampleFormat.Id),
            new FormatDefinition(FormatIds.Config, SampleFormat.CreateMinEngineVersions(configVersion),
                configMigrations)
        ]);
    }

    public static string Config(int schemaVersion, string? outdatedFormatOnRead)
    {
        var policyField = outdatedFormatOnRead is null
            ? string.Empty
            : $""", "outdatedFormatOnRead": "{outdatedFormatOnRead}" """;

        return $$"""{ "schemaVersion": {{schemaVersion}}, "minEngineVersion": "0.{{schemaVersion}}.0"{{policyField}} }""";
    }
}
