using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a phase command names a phase that is not a phase of the workflow
/// profile of the feature.
/// </summary>
/// <param name="phase">The name of the phase that is not in the profile.</param>
/// <param name="profile">The name of the workflow profile of the feature.</param>
/// <param name="profilePhases">The names of the phases of the profile, in profile order.</param>
public class PhaseNotInProfileException(string phase, string profile, IReadOnlyList<string> profilePhases)
    : Exception($"The phase '{phase}' is not a phase of the workflow profile '{profile}'. Phases: " +
                $"{string.Join(", ", Ensure.NotNull(profilePhases))}.")
{
    /// <summary>
    /// Gets the name of the phase that is not in the profile.
    /// </summary>
    /// <value>The phase name as it was passed, for example the argument of <c>shade-kit phase start</c>.</value>
    public string Phase { get; } = phase;

    /// <summary>
    /// Gets the name of the workflow profile of the feature.
    /// </summary>
    /// <value>The profile name, for example <c>bugfix</c>.</value>
    public string Profile { get; } = profile;

    /// <summary>
    /// Gets the names of the phases of the profile.
    /// </summary>
    /// <value>A copy of the phase names in profile order.</value>
    public IReadOnlyList<string> ProfilePhases { get; } = [.. profilePhases];
}
