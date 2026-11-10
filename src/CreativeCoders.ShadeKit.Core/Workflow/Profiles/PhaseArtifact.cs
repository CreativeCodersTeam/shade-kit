using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles;

/// <summary>
/// Describes an artifact that a phase of a <see cref="WorkflowProfile" /> writes into the feature folder.
/// </summary>
/// <remarks>
/// A gate check uses the artifacts of the phases of a profile to decide whether a checklist item applies: an item about
/// an artifact that no phase of the profile writes does not apply, and so does an item about an artifact written only
/// on demand that does not exist.
/// </remarks>
public sealed record PhaseArtifact
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseArtifact" /> class.
    /// </summary>
    /// <param name="file">
    /// The path of the artifact relative to the feature folder, for example <see cref="WorkflowArtifactNames.Spec" />.
    /// </param>
    /// <param name="onDemand">
    /// <see langword="true" /> if the phase writes the artifact only when it is needed, for example a playbook only for
    /// manual acceptance criteria; <see langword="false" /> if the phase always writes it. The default is
    /// <see langword="false" />.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="file" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    public PhaseArtifact(string file, bool onDemand = false)
    {
        File = Ensure.IsNotNullOrWhitespace(file);
        OnDemand = onDemand;
    }

    /// <summary>
    /// Gets the path of the artifact relative to the feature folder.
    /// </summary>
    /// <value>The path of the artifact, for example <see cref="WorkflowArtifactNames.Spec" />.</value>
    public string File { get; }

    /// <summary>
    /// Gets a value that indicates whether the phase writes the artifact only when it is needed.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if the artifact is written only on demand; <see langword="false" /> if the phase always
    /// writes it.
    /// </value>
    public bool OnDemand { get; }
}
