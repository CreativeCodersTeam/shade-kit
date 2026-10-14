namespace CreativeCoders.ShadeKit.Core.Tests.Store;

/// <summary>
/// Creates a temporary directory tree for SHADE kit store tests and deletes it on disposal.
/// </summary>
internal sealed class TempKitDirectory : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("shade-kit-tests-");

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
