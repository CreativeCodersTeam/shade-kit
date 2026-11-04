using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Represents the gate of one transition in <c>.shadekit/gates.json</c>, identified by the target phase of the
/// transition.
/// </summary>
/// <remarks>
/// The two switches <see cref="Approval" /> and <see cref="Checklist" /> are independent. Unknown fields are kept in
/// <see cref="ExtensionData" />.
/// </remarks>
public sealed class GateConfig
{
    /// <summary>
    /// Gets or sets a value that indicates whether a human must approve the transition.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if the transition needs an approval; otherwise, <see langword="false" />. The default is
    /// <see langword="true" />, which also applies when the field is missing. A <see langword="null" /> value makes the
    /// file invalid.
    /// </value>
    public bool Approval { get; set; } = true;

    /// <summary>
    /// Gets or sets the path of the checklist that the agent must check before the transition.
    /// </summary>
    /// <value>
    /// A path relative to <c>.shadekit/templates/</c> that is also the path of the feature's copy relative to the
    /// feature folder, for example <c>checks/definition-of-ready.md</c>; or <see langword="null" /> if the field is
    /// missing or <see langword="null" />, which means that the transition has no checklist. A <see langword="null" />
    /// value is not written. A path that is not a <c>/</c>-separated relative path under <c>checks/</c> whose file
    /// name ends in <c>.md</c>, or that contains a <c>:</c> or a control character, makes the file invalid.
    /// </value>
    public string? Checklist { get; set; }

    /// <summary>
    /// Gets or sets the fields of the gate that the engine does not know.
    /// </summary>
    /// <value>
    /// The unknown fields and their values, kept as read, or <see langword="null" /> if the gate has none.
    /// </value>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
