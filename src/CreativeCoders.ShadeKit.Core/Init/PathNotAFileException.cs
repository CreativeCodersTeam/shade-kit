namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Represents the exception that is thrown when a path at which the initialisation reads and writes a file is an
/// existing directory.
/// </summary>
/// <param name="path">The path that is a directory.</param>
public class PathNotAFileException(string path)
    : Exception($"The path '{path}' is a directory, but a file is expected.")
{
    /// <summary>
    /// Gets the path that is a directory instead of a file.
    /// </summary>
    /// <value>The path as it was passed to the reader.</value>
    public string Path { get; } = path;
}
