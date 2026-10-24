using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the input from which a feature was created, stored as the <c>source</c> field of
/// <c>feature.json</c>.
/// </summary>
/// <remarks>
/// <see cref="Kind" /> is required; a <c>url</c> or <c>file</c> source also needs a non-empty <see cref="Value" />.
/// Unknown fields are kept in <see cref="ExtensionData" />.
/// </remarks>
public sealed class FeatureSource : IJsonOnDeserialized
{
    /// <summary>
    /// Gets the kind of input from which the feature was created.
    /// </summary>
    /// <value>One of the enumeration values, stored as <c>text</c>, <c>url</c>, or <c>file</c>.</value>
    public required FeatureSourceKind Kind { get; init; }

    /// <summary>
    /// Gets the reference to the input from which the feature was created.
    /// </summary>
    /// <value>
    /// For <see cref="FeatureSourceKind.Url" />, the URL as given; for <see cref="FeatureSourceKind.File" />, the path
    /// of the file relative to the repository root with <c>/</c> as separator if the file is inside the repository,
    /// otherwise its absolute full path; for <see cref="FeatureSourceKind.Text" />, <see langword="null" />. A
    /// <see langword="null" /> value is not written.
    /// </value>
    public string? Value { get; init; }

    /// <summary>
    /// Gets or sets the fields of the source that the engine does not know.
    /// </summary>
    /// <value>
    /// The unknown fields and their values, kept as read, or <see langword="null" /> if the source has none.
    /// </value>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    void IJsonOnDeserialized.OnDeserialized()
    {
        if (Kind is FeatureSourceKind.Url or FeatureSourceKind.File && string.IsNullOrWhiteSpace(Value))
        {
            throw new JsonException("The 'source.value' field is missing or empty for a 'url' or 'file' source.");
        }
    }
}
