using System.Text.Json;
using System.Text.Json.Serialization;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the identity and status of a feature, stored in <c>.shadekit/features/&lt;id&gt;/feature.json</c>.
/// </summary>
/// <remarks>
/// Format version <c>1</c>. <see cref="Id" />, <see cref="Title" />, <see cref="Profile" />, <see cref="Source" />,
/// <see cref="CreatedAt" />, and <see cref="CreatedBy" /> are required. Unknown fields, for example those of later
/// packages, are kept in <see cref="VersionedDocument.ExtensionData" />.
/// <para>
/// The phase fields <see cref="CurrentPhase" />, <see cref="Phases" />, <see cref="PendingTransition" />, and
/// <see cref="History" /> are optional, because features created before the phase engine do not have them. A missing
/// field is read as <see langword="null" />, and a <see langword="null" /> field is not written, so such a feature
/// is written back without phase fields as long as nobody sets them. A missing <see cref="CurrentPhase" /> means
/// the initial state of the profile; the phase engine interprets it, this document does not.
/// </para>
/// </remarks>
public sealed class FeatureDocument : VersionedDocument, IJsonOnDeserialized
{
    /// <summary>
    /// Gets the id of the feature.
    /// </summary>
    /// <value>The feature id, which is also the name of the feature directory, for example <c>gh-142-login-sso</c>.</value>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the title of the feature.
    /// </summary>
    /// <value>The human-readable title from which the slug of the id was built.</value>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the workflow profile of the feature.
    /// </summary>
    /// <value>The name of a workflow profile, for example <see cref="WorkflowProfileNames.Feature" />.</value>
    public required string Profile { get; init; }

    /// <summary>
    /// Gets the input from which the feature was created.
    /// </summary>
    /// <value>The kind of the input and the reference to it.</value>
    public required FeatureSource Source { get; init; }

    /// <summary>
    /// Gets the point in time at which the feature was created.
    /// </summary>
    /// <value>The local creation time with its offset from UTC, written in ISO 8601 format.</value>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the git user who created the feature.
    /// </summary>
    /// <value>The name and email address of the creator.</value>
    public required FeatureCreator CreatedBy { get; init; }

    /// <summary>
    /// Gets or sets whether work on the feature is still in progress.
    /// </summary>
    /// <value>
    /// One of the enumeration values. The default is <see cref="FeatureStatus.Active" />, which also applies when the
    /// field is missing.
    /// </value>
    public FeatureStatus Status { get; set; } = FeatureStatus.Active;

    /// <summary>
    /// Gets or sets the phase that is in progress or awaiting approval.
    /// </summary>
    /// <value>
    /// The name of a phase of the profile, or <see langword="null" /> if the field is missing, which the phase engine
    /// interprets as the initial state of the profile.
    /// </value>
    public string? CurrentPhase { get; set; }

    /// <summary>
    /// Gets or sets the state of each phase.
    /// </summary>
    /// <value>
    /// The phase names mapped to their states, in the order in which they are written to the file and were read
    /// from it, or <see langword="null" /> if the field is missing.
    /// </value>
    public OrderedDictionary<string, PhaseState>? Phases { get; set; }

    /// <summary>
    /// Gets or sets the target phase of the transition that waits for an approval.
    /// </summary>
    /// <value>
    /// The name of the target phase while <see cref="CurrentPhase" /> is <see cref="PhaseState.AwaitingApproval" />,
    /// otherwise <see langword="null" />, which omits the field from the file.
    /// </value>
    public string? PendingTransition { get; set; }

    /// <summary>
    /// Gets or sets the state changes of the phases.
    /// </summary>
    /// <value>
    /// The state changes in the order in which they happened, or <see langword="null" /> if the field is missing. A
    /// <see langword="null" /> entry makes the file invalid.
    /// </value>
    public List<PhaseHistoryEntry>? History { get; set; }

    void IJsonOnDeserialized.OnDeserialized()
    {
        // RespectNullableAnnotations does not cover list elements, so a null entry is rejected here.
        if (History?.Any(entry => entry is null) == true)
        {
            throw new JsonException("The 'history' field contains a null entry.");
        }
    }
}
