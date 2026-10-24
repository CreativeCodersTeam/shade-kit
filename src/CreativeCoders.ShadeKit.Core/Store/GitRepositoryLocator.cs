using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Locates the root directory of a git repository in the file system.
/// </summary>
public class GitRepositoryLocator : IGitRepositoryLocator
{
    private const string GitEntryName = ".git";

    /// <inheritdoc />
    public string GetRepositoryRootPath(string path)
    {
        Ensure.IsNotNullOrWhitespace(path);

        var startPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
        {
            if (Path.Exists(Path.Combine(directory.FullName, GitEntryName)))
            {
                return Path.TrimEndingDirectorySeparator(directory.FullName);
            }
        }

        throw new NotAGitRepositoryException(path);
    }
}
