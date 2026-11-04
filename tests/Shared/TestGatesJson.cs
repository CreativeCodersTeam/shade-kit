namespace CreativeCoders.ShadeKit.Tests.Shared;

/// <summary>
/// Provides the JSON text of <c>gates.json</c> files of format version 1 for tests.
/// </summary>
internal static class TestGatesJson
{
    /// <summary>
    /// Represents the fields of the format header of format version 1, without the enclosing braces.
    /// </summary>
    public const string Header = """ "schemaVersion": 1, "minEngineVersion": "0.1.0" """;

    /// <summary>
    /// Represents a document of format version 1 that has only the format header and no <c>profiles</c> field.
    /// </summary>
    public const string HeaderOnly = $$"""{ {{Header}} }""";

    /// <summary>
    /// Creates a document of format version 1 whose <c>profiles</c> field is the specified JSON text.
    /// </summary>
    /// <param name="profiles">The JSON text of the <c>profiles</c> field, for example <c>{}</c>.</param>
    /// <returns>The JSON text of the document.</returns>
    public static string WithProfiles(string profiles)
    {
        return $$"""{ {{Header}}, "profiles": {{profiles}} }""";
    }
}
