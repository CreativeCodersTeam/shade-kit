using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Represents the exception that is thrown when the git identity that SHADE kit records as the creator of a feature is
/// not configured, because <c>user.name</c>, <c>user.email</c>, or both are not set.
/// </summary>
/// <param name="repositoryRootPath">The root directory of the repository in which the identity was read.</param>
/// <param name="missingKeys">The git configuration keys that are not set, for example <c>user.name</c>.</param>
public class GitIdentityNotConfiguredException(string repositoryRootPath, IReadOnlyList<string> missingKeys)
    : Exception(
        $"The git identity for the repository '{repositoryRootPath}' is not configured: " +
        $"{FormatKeys(missingKeys)} not set. Set each missing key with 'git config <key> <value>'.")
{
    /// <summary>
    /// Gets the root directory of the repository in which the identity was read.
    /// </summary>
    /// <value>The repository root path as it was passed to the identity lookup.</value>
    public string RepositoryRootPath { get; } = repositoryRootPath;

    /// <summary>
    /// Gets the git configuration keys that are not set.
    /// </summary>
    /// <value>A copy of the missing keys in the order they were passed, for example <c>user.name</c> and
    /// <c>user.email</c>.</value>
    public IReadOnlyList<string> MissingKeys { get; } = [.. missingKeys];

    private static string FormatKeys(IReadOnlyList<string> missingKeys)
    {
        Ensure.NotNull(missingKeys);

        var keys = string.Join(" and ", missingKeys);

        return missingKeys.Count == 1 ? $"{keys} is" : $"{keys} are";
    }
}
