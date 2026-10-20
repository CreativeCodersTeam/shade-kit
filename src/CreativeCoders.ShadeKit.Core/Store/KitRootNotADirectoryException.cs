namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Represents the exception that is thrown when the SHADE kit root path (<c>.shadekit</c>) at the root of a git
/// repository is a file instead of a directory.
/// </summary>
/// <param name="kitRootPath">The path of the <c>.shadekit</c> file.</param>
public class KitRootNotADirectoryException(string kitRootPath)
    : Exception($"The SHADE kit root '{kitRootPath}' is a file, not a directory.")
{
    /// <summary>
    /// Gets the path of the SHADE kit root that is a file.
    /// </summary>
    /// <value>The full path of the <c>.shadekit</c> file at the repository root.</value>
    public string KitRootPath { get; } = kitRootPath;
}
