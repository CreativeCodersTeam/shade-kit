namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Defines the names of the workflow profiles that a feature can follow.
/// </summary>
/// <remarks>
/// The names are part of the agent contract (<c>feature.json</c>, <c>defaultProfile</c>, <c>--profile</c>).
/// </remarks>
public static class WorkflowProfiles
{
    /// <summary>
    /// Represents the profile of a new feature: new, refine, map, plan, implement, verify, finish.
    /// </summary>
    public const string Feature = "feature";

    /// <summary>
    /// Represents the profile of a bug fix: new, diagnose, plan, implement, verify, finish.
    /// </summary>
    public const string Bugfix = "bugfix";

    /// <summary>
    /// Represents the profile of a small change: new, quick, finish.
    /// </summary>
    public const string Quick = "quick";

    /// <summary>
    /// Gets the names of all known workflow profiles.
    /// </summary>
    /// <value>The names <see cref="Feature" />, <see cref="Bugfix" />, and <see cref="Quick" />, in this order.</value>
    public static IReadOnlyList<string> All { get; } = [Feature, Bugfix, Quick];

    /// <summary>
    /// Determines whether the specified name is the name of a known workflow profile.
    /// </summary>
    /// <remarks>The comparison is ordinal and exact, so <c>Feature</c> or <c> feature</c> is not known.</remarks>
    /// <param name="profile">The name to check.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="profile" /> is one of the names in <see cref="All" />; otherwise,
    /// <see langword="false" />, also if <paramref name="profile" /> is <see langword="null" />.
    /// </returns>
    public static bool IsKnown(string? profile)
    {
        return profile is not null && All.Contains(profile, StringComparer.Ordinal);
    }
}
