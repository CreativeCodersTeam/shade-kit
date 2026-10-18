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
    /// Represents the name of the gate configuration file in the SHADE kit root directory.
    /// </summary>
    public const string GatesFileName = "gates.json";

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
}
