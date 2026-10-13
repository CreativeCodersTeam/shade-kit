using CreativeCoders.ShadeKit.Core.Store;

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

    public void CreateFile(params string[] relativePathParts)
    {
        File.WriteAllText(Path.Combine([RootPath, .. relativePathParts]), string.Empty);
    }

    /// <summary>
    /// Skips the current test if a SHADE kit root exists above the temporary directory, because a "root not found"
    /// scenario cannot be produced there.
    /// </summary>
    public void SkipWhenKitRootExistsAbove()
    {
        Assert.SkipWhen(HasKitRootAbove(),
            $"A '{KitStorePaths.RootPath}' directory exists above '{RootPath}'; the not-found scenario cannot be tested here.");
    }

    private bool HasKitRootAbove()
    {
        for (var directory = _directory.Parent; directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, KitStorePaths.RootPath)))
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
