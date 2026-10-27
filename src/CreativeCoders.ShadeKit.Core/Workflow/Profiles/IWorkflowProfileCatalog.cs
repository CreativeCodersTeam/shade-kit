using System.Diagnostics.CodeAnalysis;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles;

/// <summary>
/// Provides the workflow profiles that a feature can follow.
/// </summary>
/// <remarks>Profile names are compared ordinally and exactly, so <c>Feature</c> or <c> feature</c> is not known.</remarks>
public interface IWorkflowProfileCatalog
{
    /// <summary>
    /// Gets all known workflow profiles.
    /// </summary>
    /// <value>The known profiles in the order the catalog offers them; their names are unique.</value>
    IReadOnlyList<WorkflowProfile> Profiles { get; }

    /// <summary>
    /// Gets the workflow profile with the specified name.
    /// </summary>
    /// <param name="name">The name of the profile to look up.</param>
    /// <param name="profile">
    /// When this method returns, contains the profile with the name <paramref name="name" />, if it is known;
    /// otherwise, <see langword="null" />.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if a profile with the name <paramref name="name" /> is known; otherwise,
    /// <see langword="false" />, also if <paramref name="name" /> is <see langword="null" />.
    /// </returns>
    bool TryGetProfile(string? name, [NotNullWhen(true)] out WorkflowProfile? profile);

    /// <summary>
    /// Gets the workflow profile with the specified name.
    /// </summary>
    /// <param name="name">The name of the profile to look up.</param>
    /// <returns>The profile with the name <paramref name="name" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name" /> is <see langword="null" />.</exception>
    /// <exception cref="UnknownProfileException">
    /// No profile with the name <paramref name="name" /> is known; the exception lists the names of the known
    /// profiles.
    /// </exception>
    WorkflowProfile GetProfile(string name);
}
