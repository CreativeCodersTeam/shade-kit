namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the exception that is thrown when a workflow profile is not one of the names in
/// <see cref="WorkflowProfiles.All" />.
/// </summary>
/// <param name="profile">The name of the workflow profile that is unknown.</param>
public class UnknownProfileException(string profile)
    : Exception($"The workflow profile '{profile}' is unknown. Known profiles: " +
                $"{string.Join(", ", WorkflowProfiles.All)}.")
{
    /// <summary>
    /// Gets the name of the workflow profile that is unknown.
    /// </summary>
    /// <value>The name as it was passed, for example the value of <c>--profile</c>.</value>
    public string Profile { get; } = profile;
}
