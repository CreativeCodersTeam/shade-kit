namespace CreativeCoders.ShadeKit.Core.Store.Exceptions;

/// <summary>
/// Represents the exception that is thrown when no SHADE kit root (<c>.shadekit</c> directory) exists at the root of
/// the git repository that contains a directory.
/// </summary>
/// <param name="path">The directory at which the search started.</param>
/// <param name="gitRootPath">The root directory of the git repository that contains <paramref name="path" />.</param>
public class KitRootPathNotFoundException(string path, string gitRootPath)
    : Exception($"The git repository root '{gitRootPath}' contains no '{KitStorePaths.RootPath}' directory.")
{
    /// <summary>
    /// Gets the directory at which the search started.
    /// </summary>
    /// <value>The start directory as it was passed to the search.</value>
    public string StartPath { get; } = path;

    /// <summary>
    /// Gets the root directory of the git repository that contains <see cref="StartPath" />.
    /// </summary>
    /// <value>The full path of the repository root.</value>
    public string GitRootPath { get; } = gitRootPath;
}
