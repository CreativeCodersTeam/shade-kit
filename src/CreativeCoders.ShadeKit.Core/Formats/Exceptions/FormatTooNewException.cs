namespace CreativeCoders.ShadeKit.Core.Formats.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a file uses a format version that is newer than the engine supports.
/// </summary>
/// <param name="filePath">The path of the file.</param>
/// <param name="fileVersion">The format version of the file.</param>
/// <param name="supportedVersion">The highest format version of the file type that the engine supports.</param>
/// <param name="minEngineVersion">
/// The first engine version that supports the format version of the file, as stated in the file, or
/// <see langword="null" /> if the file does not state a usable one.
/// </param>
public class FormatTooNewException(string filePath, int fileVersion, int supportedVersion, string? minEngineVersion)
    : KitFileFormatException(filePath,
        $"The file '{filePath}' uses format version {fileVersion}, but this engine supports only up to version " +
        $"{supportedVersion}.")
{
    /// <summary>
    /// Gets the format version of the file.
    /// </summary>
    /// <value>The format version stated in the <c>schemaVersion</c> field of the file.</value>
    public int FileVersion { get; } = fileVersion;

    /// <summary>
    /// Gets the highest format version of the file type that the engine supports.
    /// </summary>
    /// <value>The current format version of the file type.</value>
    public int SupportedVersion { get; } = supportedVersion;

    /// <summary>
    /// Gets the first engine version that supports the format version of the file.
    /// </summary>
    /// <value>
    /// The <c>minEngineVersion</c> field of the file, or <see langword="null" /> if the field is missing, empty, or not
    /// a string.
    /// </value>
    public string? MinEngineVersion { get; } = minEngineVersion;
}
