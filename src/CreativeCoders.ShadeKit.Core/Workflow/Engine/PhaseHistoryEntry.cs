using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents one state change of the phases of a feature, stored as an entry of the <c>history</c> field of
/// <c>feature.json</c>.
/// </summary>
/// <remarks>
/// <see cref="At" />, <see cref="By" />, and <see cref="Action" /> are required. <see cref="From" />,
/// <see cref="To" />, and <see cref="Note" /> are omitted from the file when they are <see langword="null" />.
/// Unknown fields are kept in <see cref="ExtensionData" />.
/// </remarks>
public sealed class PhaseHistoryEntry
{
    /// <summary>
    /// Gets the point in time of the state change.
    /// </summary>
    /// <value>The local time with its offset from UTC, written in ISO 8601 format.</value>
    public required DateTimeOffset At { get; init; }

    /// <summary>
    /// Gets the git user who caused the state change.
    /// </summary>
    /// <value>The name and email address of the git user.</value>
    public required PhaseHistoryActor By { get; init; }

    /// <summary>
    /// Gets the kind of the state change.
    /// </summary>
    /// <value>One of the enumeration values.</value>
    public required PhaseHistoryAction Action { get; init; }

    /// <summary>
    /// Gets the phase in which the state change started.
    /// </summary>
    /// <value>The name of the phase that was current before the state change, or <see langword="null" /> if none.</value>
    public string? From { get; init; }

    /// <summary>
    /// Gets the phase to which the state change leads.
    /// </summary>
    /// <value>
    /// The name of the target phase of the transition, or <see langword="null" /> if the state change has no target,
    /// for example <see cref="PhaseHistoryAction.Complete" />.
    /// </value>
    public string? To { get; init; }

    /// <summary>
    /// Gets the note that was given with the state change.
    /// </summary>
    /// <value>The note, for example the failed checklist items of a rejection, or <see langword="null" /> if none.</value>
    public string? Note { get; init; }

    /// <summary>
    /// Gets or sets the fields of the entry that the engine does not know.
    /// </summary>
    /// <value>
    /// The unknown fields and their values, kept as read, or <see langword="null" /> if the entry has none.
    /// </value>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
