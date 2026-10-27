using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a workflow profile is not one of the profiles of the
/// <see cref="IWorkflowProfileCatalog" />.
/// </summary>
/// <param name="profile">The name of the workflow profile that is unknown.</param>
/// <param name="knownProfiles">The names of the known workflow profiles, for example <c>feature</c>.</param>
/// <param name="featureFilePath">
/// The repository-relative path of the <c>feature.json</c> file whose <c>profile</c> field names the unknown profile,
/// or <see langword="null" /> if the profile was requested for a new feature, for example with <c>--profile</c>.
/// </param>
public class UnknownProfileException(
    string profile,
    IReadOnlyList<string> knownProfiles,
    string? featureFilePath = null)
    : Exception(CreateMessage(profile, knownProfiles, featureFilePath))
{
    /// <summary>
    /// Gets the name of the workflow profile that is unknown.
    /// </summary>
    /// <value>
    /// The name as it was passed, for example the value of <c>--profile</c> or of the <c>profile</c> field of a
    /// <c>feature.json</c> file.
    /// </value>
    public string Profile { get; } = profile;

    /// <summary>
    /// Gets the names of the known workflow profiles.
    /// </summary>
    /// <value>
    /// A copy of the known profile names in the order they were passed, for example <c>feature</c>, <c>bugfix</c>, and
    /// <c>quick</c>.
    /// </value>
    public IReadOnlyList<string> KnownProfiles { get; } = [.. knownProfiles];

    /// <summary>
    /// Gets the path of the <c>feature.json</c> file whose <c>profile</c> field names the unknown profile.
    /// </summary>
    /// <value>
    /// The repository-relative path, for example <c>.shadekit/features/gh-142-login-sso/feature.json</c>, or
    /// <see langword="null" /> if the profile was requested for a new feature.
    /// </value>
    public string? FeatureFilePath { get; } = featureFilePath;

    private static string CreateMessage(string profile, IReadOnlyList<string> knownProfiles, string? featureFilePath)
    {
        var knownProfilesText = Ensure.NotNull(knownProfiles).Count > 0
            ? $"Known profiles: {string.Join(", ", knownProfiles)}."
            : "No profiles are known.";

        var locationText = featureFilePath is null ? string.Empty : $" in '{featureFilePath}'";

        return $"The workflow profile '{profile}'{locationText} is unknown. {knownProfilesText}";
    }
}
