using System.Diagnostics.CodeAnalysis;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles;

/// <summary>
/// Provides the workflow profiles that are built into the engine, as defined in
/// <see cref="BuiltInWorkflowProfiles" />.
/// </summary>
public sealed class BuiltInWorkflowProfileCatalog : IWorkflowProfileCatalog
{
    private readonly string[] _profileNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuiltInWorkflowProfileCatalog" /> class with the profiles in
    /// <see cref="BuiltInWorkflowProfiles.All" />.
    /// </summary>
    public BuiltInWorkflowProfileCatalog()
        : this(BuiltInWorkflowProfiles.All)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuiltInWorkflowProfileCatalog" /> class with the specified
    /// profiles.
    /// </summary>
    /// <param name="profiles">The profiles of the catalog in the order it offers them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profiles" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="profiles" /> contains <see langword="null" />, or two profiles have the same name.
    /// </exception>
    internal BuiltInWorkflowProfileCatalog(IEnumerable<WorkflowProfile> profiles)
    {
        Profiles = [.. Ensure.NotNull(profiles)];

        ValidateProfiles(Profiles);

        _profileNames = [.. Profiles.Select(x => x.Name)];
    }

    /// <inheritdoc />
    public IReadOnlyList<WorkflowProfile> Profiles { get; }

    /// <inheritdoc />
    public bool TryGetProfile(string? name, [NotNullWhen(true)] out WorkflowProfile? profile)
    {
        profile = Profiles.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

        return profile is not null;
    }

    /// <inheritdoc />
    public WorkflowProfile GetProfile(string name)
    {
        Ensure.NotNull(name);

        return TryGetProfile(name, out var profile)
            ? profile
            : throw new UnknownProfileException(name, _profileNames);
    }

    private static void ValidateProfiles(IReadOnlyList<WorkflowProfile> profiles)
    {
        if (profiles.Any(x => x is null))
        {
            throw new ArgumentException("The workflow profiles contain null.", nameof(profiles));
        }

        var duplicateName = profiles
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .FirstOrDefault(x => x.Count() > 1)?.Key;

        if (duplicateName is not null)
        {
            throw new ArgumentException($"The workflow profile '{duplicateName}' is defined more than once.",
                nameof(profiles));
        }
    }
}
