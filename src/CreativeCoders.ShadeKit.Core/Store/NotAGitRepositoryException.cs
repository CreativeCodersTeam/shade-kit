namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Represents the exception that is thrown when a directory is not inside a git repository, so no SHADE kit root can
/// exist for it.
/// </summary>
/// <param name="path">The directory at which the search for the repository root started.</param>
public class NotAGitRepositoryException(string path)
    : Exception($"The directory '{path}' is not inside a git repository.")
{
    /// <summary>
    /// Gets the directory at which the search for the repository root started.
    /// </summary>
    /// <value>The start directory as it was passed to the search.</value>
    public string StartPath { get; } = path;
}
