using System.Diagnostics.CodeAnalysis;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles;

/// <summary>
/// Describes a workflow profile: its name and the ordered list of phases a feature of the profile runs through.
/// </summary>
/// <remarks>
/// Profile and phase names are compared ordinally and exactly, so <c>Refine</c> or <c> refine</c> is not the phase
/// <c>refine</c>.
/// </remarks>
public sealed class WorkflowProfile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowProfile" /> class.
    /// </summary>
    /// <param name="name">The name of the profile, for example <see cref="WorkflowProfileNames.Feature" />.</param>
    /// <param name="phases">The phases of the profile in the order a feature runs through them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="phases" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name" /> is <see langword="null" />, empty, or whitespace; <paramref name="phases" /> is empty
    /// or contains <see langword="null" />; or two phases have the same name.
    /// </exception>
    public WorkflowProfile(string name, IEnumerable<WorkflowPhase> phases)
    {
        Name = Ensure.IsNotNullOrWhitespace(name);
        Phases = [.. Ensure.NotNull(phases)];

        ValidatePhases(name, Phases);
    }

    /// <summary>
    /// Gets the name of the profile.
    /// </summary>
    /// <value>The name of the profile, for example <see cref="WorkflowProfileNames.Feature" />.</value>
    public string Name { get; }

    /// <summary>
    /// Gets the phases of the profile.
    /// </summary>
    /// <value>A copy of the phases in the order a feature runs through them; never empty.</value>
    public IReadOnlyList<WorkflowPhase> Phases { get; }

    /// <summary>
    /// Gets the position of the specified phase in the profile.
    /// </summary>
    /// <param name="phaseName">The name of the phase to look up.</param>
    /// <returns>
    /// The zero-based index of the phase in <see cref="Phases" />; <c>-1</c> if the profile has no phase with the name
    /// <paramref name="phaseName" />, also if <paramref name="phaseName" /> is <see langword="null" />.
    /// </returns>
    public int IndexOf(string? phaseName)
    {
        for (var index = 0; index < Phases.Count; index++)
        {
            if (string.Equals(Phases[index].Name, phaseName, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Gets the phase with the specified name.
    /// </summary>
    /// <param name="phaseName">The name of the phase to look up.</param>
    /// <param name="phase">
    /// When this method returns, contains the phase with the name <paramref name="phaseName" />, if the profile has
    /// one; otherwise, <see langword="null" />.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if the profile has a phase with the name <paramref name="phaseName" />; otherwise,
    /// <see langword="false" />, also if <paramref name="phaseName" /> is <see langword="null" />.
    /// </returns>
    public bool TryGetPhase(string? phaseName, [NotNullWhen(true)] out WorkflowPhase? phase)
    {
        var index = IndexOf(phaseName);

        phase = index >= 0 ? Phases[index] : null;

        return phase is not null;
    }

    private static void ValidatePhases(string name, IReadOnlyList<WorkflowPhase> phases)
    {
        if (phases.Count == 0)
        {
            throw new ArgumentException($"The workflow profile '{name}' has no phases.", nameof(phases));
        }

        if (phases.Any(x => x is null))
        {
            throw new ArgumentException($"The phases of the workflow profile '{name}' contain null.", nameof(phases));
        }

        var duplicateName = phases
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .FirstOrDefault(x => x.Count() > 1)?.Key;

        if (duplicateName is not null)
        {
            throw new ArgumentException(
                $"The workflow profile '{name}' has the phase '{duplicateName}' more than once.", nameof(phases));
        }
    }
}
