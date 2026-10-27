using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the git user who caused a state change of a phase, stored as the <c>by</c> field of a
/// <c>history</c> entry of <c>feature.json</c>.
/// </summary>
/// <remarks>
/// <see cref="Name" /> and <see cref="Email" /> are required. Unknown fields are kept in <see cref="ExtensionData" />.
/// </remarks>
public sealed class PhaseHistoryActor
{
    /// <summary>
    /// Gets the name of the git user who caused the state change.
    /// </summary>
    /// <value>The configured git <c>user.name</c>.</value>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the email address of the git user who caused the state change.
    /// </summary>
    /// <value>The configured git <c>user.email</c>.</value>
    public required string Email { get; init; }

    /// <summary>
    /// Gets or sets the fields of the actor that the engine does not know.
    /// </summary>
    /// <value>
    /// The unknown fields and their values, kept as read, or <see langword="null" /> if the actor has none.
    /// </value>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
