using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Defines the <c>.gitignore</c> entries that an <see cref="IGitIgnorePlanner" /> plans for the local-only files of
/// SHADE kit.
/// </summary>
public static class KitGitIgnoreEntries
{
    /// <summary>
    /// Gets the <c>.gitignore</c> entries for the local-only files of SHADE kit.
    /// </summary>
    /// <value>
    /// The entries <c>.shadekit/local.json</c> and <c>.shadekit/features/current-feature.json</c>, in this order,
    /// relative to the repository root and without a leading <c>/</c>.
    /// </value>
    public static IReadOnlyList<string> All { get; } =
    [
        KitStorePaths.GetRepositoryPath(KitStorePaths.LocalFileName),
        KitStorePaths.GetRepositoryPath($"{KitStorePaths.FeaturesDirName}/{KitStorePaths.CurrentFeatureFileName}")
    ];
}
