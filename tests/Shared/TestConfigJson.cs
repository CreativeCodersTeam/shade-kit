namespace CreativeCoders.ShadeKit.Tests.Shared;

/// <summary>
/// Provides the JSON text of <c>config.json</c> files of format version 1 for tests.
/// </summary>
internal static class TestConfigJson
{
    /// <summary>
    /// Creates a document of format version 1 with the format header followed by the specified fields.
    /// </summary>
    /// <param name="fields">
    /// The JSON text that follows the format header inside the braces, starting with a comma, for example
    /// <c>, "coverageThreshold": 80</c>; an empty string for a document with only the format header.
    /// </param>
    /// <returns>The JSON text of the document.</returns>
    public static string WithFields(string fields)
    {
        return $$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0"{{fields}} }""";
    }
}
