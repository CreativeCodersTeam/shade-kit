using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the git user who created a feature, stored as the <c>createdBy</c> field of <c>feature.json</c>.
/// </summary>
/// <remarks>
/// <see cref="Name" /> and <see cref="Email" /> are required: a file in which one of them is missing or
/// <see langword="null" /> cannot be read. Fields the engine does not know are kept in <see cref="ExtensionData" />
/// and written back unchanged.
/// </remarks>
public sealed class FeatureCreator
{
    /// <summary>
    /// Gets the name of the git user who created the feature.
    /// </summary>
    /// <value>The configured git <c>user.name</c>.</value>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the email address of the git user who created the feature.
    /// </summary>
    /// <value>The configured git <c>user.email</c>.</value>
    public required string Email { get; init; }

    /// <summary>
    /// Gets or sets the fields of the creator that the engine does not know.
    /// </summary>
    /// <value>
    /// The unknown fields and their values, kept as read, or <see langword="null" /> if the creator has none.
    /// </value>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
