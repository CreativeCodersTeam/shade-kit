using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Represents the base class of every versioned JSON document that SHADE kit stores in a file.
/// </summary>
/// <remarks>
/// Unknown fields are kept in <see cref="ExtensionData" /> and written back. A nested class needs its own
/// <see cref="JsonExtensionDataAttribute" /> dictionary with <see cref="JsonElement" /> values; a <c>JsonObject</c>
/// property would be written as a nested object and produce invalid JSON.
/// </remarks>
public abstract class VersionedDocument
{
    /// <summary>
    /// Gets or sets the format version of the document.
    /// </summary>
    /// <value>The format version of the file type, starting at <c>1</c>. The engine sets it when it writes the document.</value>
    [JsonPropertyOrder(-2)]
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the first engine version that supports the format version of the document.
    /// </summary>
    /// <value>A semantic version string. The engine sets it when it writes the document.</value>
    [JsonPropertyOrder(-1)]
    public string MinEngineVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fields of the document that the engine does not know.
    /// </summary>
    /// <value>
    /// The unknown fields and their values, kept as read, or <see langword="null" /> if the document has none.
    /// </value>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
