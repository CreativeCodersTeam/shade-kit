namespace CreativeCoders.ShadeKit.Tests.Shared;

/// <summary>
/// Creates a temporary directory tree for SHADE kit tests and deletes it on disposal.
/// </summary>
/// <param name="prefix">The prefix of the name of the temporary directory.</param>
internal sealed class TempKitDirectory(string prefix = "shade-kit-tests-") : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory(prefix);

    public string RootPath => _directory.FullName;

    public string CreateDirectory(params string[] relativePathParts)
    {
        var path = Path.Combine([RootPath, .. relativePathParts]);

        Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>
    /// Marks the temporary directory, or the specified subdirectory, as a git repository root by creating a
    /// <c>.git</c> directory in it.
    /// </summary>
    public string CreateGitRoot(params string[] relativePathParts)
    {
        CreateDirectory([.. relativePathParts, ".git"]);

        return Path.Combine([RootPath, .. relativePathParts]);
    }

    public void CreateFile(params string[] relativePathParts)
    {
        File.WriteAllText(Path.Combine([RootPath, .. relativePathParts]), string.Empty);
    }

    public string WriteFile(string content, params string[] relativePathParts)
    {
        var path = Path.Combine([RootPath, .. relativePathParts]);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        return path;
    }

    public string WriteFileBytes(byte[] content, params string[] relativePathParts)
    {
        var path = Path.Combine([RootPath, .. relativePathParts]);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);

        return path;
    }

    public byte[] ReadFileBytes(params string[] relativePathParts)
    {
        return File.ReadAllBytes(Path.Combine([RootPath, .. relativePathParts]));
    }

    public string ReadFileText(params string[] relativePathParts)
    {
        return File.ReadAllText(Path.Combine([RootPath, .. relativePathParts]));
    }

    /// <summary>
    /// Skips the current test if a git repository root exists above the temporary directory, because a "not inside
    /// a git repository" scenario cannot be produced there.
    /// </summary>
    public void SkipWhenGitRootExistsAbove()
    {
        Assert.SkipWhen(HasGitRootAbove(),
            $"A '.git' entry exists above '{RootPath}'; the not-in-a-repository scenario cannot be tested here.");
    }

    /// <summary>
    /// Creates a symbolic link in the temporary directory, or skips the current test if the file system or the
    /// process cannot create one, for example on Windows without the privilege to create symbolic links.
    /// </summary>
    /// <param name="linkTarget">The target of the link, relative to the directory of the link.</param>
    /// <param name="relativePathParts">The parts of the link path, relative to the temporary directory.</param>
    /// <returns>The full path of the link.</returns>
    public string CreateSymbolicLinkOrSkip(string linkTarget, params string[] relativePathParts)
    {
        var linkPath = Path.Combine([RootPath, .. relativePathParts]);

        try
        {
            File.CreateSymbolicLink(linkPath, linkTarget);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"A symbolic link cannot be created at '{linkPath}': {exception.Message}");
        }

        return linkPath;
    }

    /// <summary>
    /// Creates a symbolic link to a directory in the temporary directory, or skips the current test if the file
    /// system or the process cannot create one, for example on Windows without the privilege to create symbolic
    /// links.
    /// </summary>
    /// <param name="linkTarget">The full path of the directory the link points to.</param>
    /// <param name="relativePathParts">The parts of the link path, relative to the temporary directory.</param>
    /// <returns>The full path of the link.</returns>
    public string CreateDirectorySymbolicLinkOrSkip(string linkTarget, params string[] relativePathParts)
    {
        var linkPath = Path.Combine([RootPath, .. relativePathParts]);

        try
        {
            Directory.CreateSymbolicLink(linkPath, linkTarget);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"A symbolic link cannot be created at '{linkPath}': {exception.Message}");
        }

        return linkPath;
    }

    /// <summary>
    /// Captures every directory and the bytes of every file below the temporary directory, keyed by relative path.
    /// </summary>
    public Dictionary<string, string> TakeSnapshot()
    {
        return Directory.EnumerateFileSystemEntries(RootPath, "*", SearchOption.AllDirectories)
            .ToDictionary(
                x => Path.GetRelativePath(RootPath, x),
                x => Directory.Exists(x) ? "<directory>" : Convert.ToBase64String(File.ReadAllBytes(x)));
    }

    private bool HasGitRootAbove()
    {
        for (var directory = _directory.Parent; directory is not null; directory = directory.Parent)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");

            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        _directory.Delete(true);
    }
}
