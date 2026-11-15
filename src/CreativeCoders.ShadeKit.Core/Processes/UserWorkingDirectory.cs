namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Provides the directory outside any repository in which external tools run that act for the user, not for the
/// current repository, such as <c>claude</c> or <c>git ls-remote</c>.
/// </summary>
internal static class UserWorkingDirectory
{
    /// <summary>
    /// Returns the home directory of the user, or the temporary directory if the home directory is not known or does
    /// not exist.
    /// </summary>
    /// <returns>The absolute path of the home directory or of the temporary directory.</returns>
    public static string Get()
    {
        return Get(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Directory.Exists);
    }

    /// <summary>
    /// Returns the specified home directory if it exists, or the temporary directory otherwise.
    /// </summary>
    /// <param name="homeDirectory">The home directory of the user, or an empty string if it is not known.</param>
    /// <param name="directoryExists">
    /// The function that returns <see langword="true" /> if the specified directory exists.
    /// </param>
    /// <returns>
    /// <paramref name="homeDirectory" /> if it is not empty or whitespace and exists; otherwise, the temporary
    /// directory.
    /// </returns>
    public static string Get(string? homeDirectory, Func<string, bool> directoryExists)
    {
        return !string.IsNullOrWhiteSpace(homeDirectory) && directoryExists(homeDirectory)
            ? homeDirectory
            : Path.GetTempPath();
    }
}
