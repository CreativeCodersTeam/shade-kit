using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Represents the base class of every versioned JSON document that SHADE kit stores in a file.
/// </summary>
/// <remarks>
/// Derived classes add the known fields of their file type. Fields the engine does not know are collected in
/// <see cref="ExtensionData" /> and written back unchanged. A nested class of a document must declare its own
/// <see cref="JsonExtensionDataAttribute" /> property of type <see cref="Dictionary{TKey,TValue}" /> with
/// <see cref="JsonElement" /> values to preserve unknown fields at its level. A <c>JsonObject</c> property is not
/// suitable: System.Text.Json writes it as a nested object instead of flattening its fields, which produces invalid
/// JSON.
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
