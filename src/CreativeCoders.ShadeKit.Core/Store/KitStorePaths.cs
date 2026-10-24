using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Defines the names of the directories and files of a SHADE kit store.
/// </summary>
public static class KitStorePaths
{
    /// <summary>
    /// Represents the name of the SHADE kit root directory at the root of a git repository.
    /// </summary>
    public const string RootPath = ".shadekit";

    /// <summary>
    /// Represents the name of the repository configuration file in the SHADE kit root directory.
    /// </summary>
    public const string ConfigFileName = "config.json";

    /// <summary>
    /// Represents the name of the directory that contains one directory per feature.
    /// </summary>
    public const string FeaturesDirName = "features";

    /// <summary>
    /// Represents the name of the file in the <see cref="FeaturesDirName" /> directory that records the current
    /// feature of a local clone. The file is local-only and not committed.
    /// </summary>
    public const string CurrentFeatureFileName = "current-feature.json";

    /// <summary>
    /// Represents the path of the local override <see cref="CurrentFeatureFileName" /> relative to the SHADE kit root
    /// directory, with <c>/</c> as separator.
    /// </summary>
    public const string CurrentFeatureFilePath = $"{FeaturesDirName}/{CurrentFeatureFileName}";

    /// <summary>
    /// Represents the name of the file in a feature directory that records the id, title, workflow profile, source,
    /// creation, and status of the feature.
    /// </summary>
    public const string FeatureFileName = "feature.json";

    /// <summary>
    /// Represents the name of the file in a feature directory that records the original request of the feature.
    /// </summary>
    public const string RequestFileName = "request.md";

    /// <summary>
    /// Represents the name of the gate configuration file in the SHADE kit root directory.
    /// </summary>
    public const string GatesFileName = "gates.json";

    /// <summary>
    /// Represents the name of the local configuration file in the SHADE kit root directory. The file is local-only
    /// and not committed.
    /// </summary>
    public const string LocalFileName = "local.json";

    /// <summary>
    /// Represents the name of the directory in the SHADE kit root directory that contains the templates of the
    /// repository.
    /// </summary>
    public const string TemplatesDirName = "templates";

    /// <summary>
    /// Represents the name of the file in the SHADE kit root directory that records the kit version and hash of every
    /// template written by SHADE kit.
    /// </summary>
    public const string TemplatesLockFileName = "templates.lock.json";

    /// <summary>
    /// Gets the path relative to the repository root of a path relative to the SHADE kit root directory.
    /// </summary>
    /// <param name="kitRelativePath">
    /// The path relative to the SHADE kit root directory, with <c>/</c> as separator, for example
    /// <c>templates/spec.md</c>.
    /// </param>
    /// <returns>
    /// The path prefixed with <see cref="RootPath" /> and <c>/</c>, for example <c>.shadekit/templates/spec.md</c>.
    /// </returns>
    public static string GetRepositoryPath(string kitRelativePath)
    {
        return $"{RootPath}/{kitRelativePath}";
    }

    /// <summary>
    /// Gets the path relative to the repository root of a normalised path relative to the SHADE kit root directory, as
    /// it is shown in messages and in the file path of exceptions.
    /// </summary>
    /// <remarks>
    /// The store builds the file path of every exception about a file with this method, so code that tells files
    /// apart by that file path uses this method as well. The path is not normalised beyond the separator: callers pass
    /// a path without <c>.</c> or <c>..</c> segments, as the store does after it resolves a path against its root.
    /// </remarks>
    /// <param name="kitRelativePath">
    /// The normalised path relative to the SHADE kit root directory, with <c>/</c> or the directory separator of the
    /// platform as separator, for example <c>features/current-feature.json</c>.
    /// </param>
    /// <returns>
    /// The path with every directory separator of the platform replaced by <c>/</c> and prefixed with
    /// <see cref="RootPath" /> and <c>/</c>, for example <c>.shadekit/features/current-feature.json</c>.
    /// </returns>
    public static string GetDisplayPath(string kitRelativePath)
    {
        return GetRepositoryPath(RepositoryFilePath.ToForwardSlashes(kitRelativePath));
    }

    /// <summary>
    /// Gets the path of the folder of a feature relative to the SHADE kit root directory.
    /// </summary>
    /// <remarks>
    /// The name is not checked. Callers that build the path from user input check the name first, for example with
    /// <c>FeatureIds.EnsureValid</c>, so that it is a single path segment.
    /// </remarks>
    /// <param name="featureName">The name of the feature folder, which is the feature id for a valid feature.</param>
    /// <returns>
    /// The path <c>features/&lt;name&gt;</c> with <c>/</c> as separator and without a trailing separator, for example
    /// <c>features/gh-142-login-sso</c>.
    /// </returns>
    public static string GetFeatureDirPath(string featureName)
    {
        return $"{FeaturesDirName}/{featureName}";
    }

    /// <summary>
    /// Gets the path of the <see cref="FeatureFileName" /> of a feature relative to the SHADE kit root directory.
    /// </summary>
    /// <remarks>
    /// The name is not checked, as with <see cref="GetFeatureDirPath" />.
    /// </remarks>
    /// <param name="featureName">The name of the feature folder, which is the feature id for a valid feature.</param>
    /// <returns>
    /// The path <c>features/&lt;name&gt;/feature.json</c> with <c>/</c> as separator, for example
    /// <c>features/gh-142-login-sso/feature.json</c>.
    /// </returns>
    public static string GetFeatureFilePath(string featureName)
    {
        return $"{GetFeatureDirPath(featureName)}/{FeatureFileName}";
    }

    /// <summary>
    /// Gets the path of the folder of a feature relative to the repository root, as it is shown to users.
    /// </summary>
    /// <remarks>
    /// The name is not checked, as with <see cref="GetFeatureDirPath" />, so the path can also be built for a name that
    /// is not a valid feature id, for example in the message of an exception about that name.
    /// </remarks>
    /// <param name="featureName">The name of the feature folder, which is the feature id for a valid feature.</param>
    /// <returns>
    /// The path <c>.shadekit/features/&lt;name&gt;/</c> with <c>/</c> as separator and with a trailing separator, for
    /// example <c>.shadekit/features/gh-142-login-sso/</c>.
    /// </returns>
    public static string GetRepositoryFeatureDirPath(string featureName)
    {
        return $"{GetRepositoryPath(GetFeatureDirPath(featureName))}/";
    }
}
