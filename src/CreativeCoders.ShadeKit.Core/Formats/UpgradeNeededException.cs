namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Represents the exception that is thrown when a file uses an older format version and the command must not migrate
/// it in memory.
/// </summary>
/// <param name="filePath">The path of the file.</param>
/// <param name="fileVersion">The format version of the file.</param>
/// <param name="currentVersion">The current format version of the file type.</param>
public class UpgradeNeededException(string filePath, int fileVersion, int currentVersion)
    : KitFileFormatException(filePath,
        $"The file '{filePath}' uses format version {fileVersion}, but the current version is {currentVersion}. " +
        "Run 'shade-kit upgrade'.")
{
    /// <summary>
    /// Gets the format version of the file.
    /// </summary>
    /// <value>The format version stated in the <c>schemaVersion</c> field of the file.</value>
    public int FileVersion { get; } = fileVersion;

    /// <summary>
    /// Gets the current format version of the file type.
    /// </summary>
    /// <value>The format version this engine writes.</value>
    public int CurrentVersion { get; } = currentVersion;
}
