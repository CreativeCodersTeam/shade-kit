using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature;

/// <summary>
/// Represents one entry of the <c>artifacts</c> array of a phase in the JSON output: an artifact the phase writes into
/// the feature folder, <c>{ file, onDemand }</c>.
/// </summary>
/// <param name="File">The path of the artifact relative to the feature folder, for example <c>spec.md</c>.</param>
/// <param name="OnDemand">
/// <see langword="true" /> if the phase writes the artifact only when it is needed, so a missing file does not mean
/// that the phase failed to write it; <see langword="false" /> if the phase always writes it.
/// </param>
public sealed record PhaseArtifactData(string File, bool OnDemand)
{
    /// <summary>
    /// Creates the data from an artifact of a phase.
    /// </summary>
    /// <param name="artifact">The artifact of the phase.</param>
    /// <returns>The data with the file and the on-demand flag of <paramref name="artifact" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artifact" /> is <see langword="null" />.</exception>
    public static PhaseArtifactData FromArtifact(PhaseArtifact artifact)
    {
        Ensure.NotNull(artifact);

        return new PhaseArtifactData(artifact.File, artifact.OnDemand);
    }
}
