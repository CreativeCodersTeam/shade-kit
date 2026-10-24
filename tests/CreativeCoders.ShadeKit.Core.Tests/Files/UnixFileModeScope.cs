using System.Runtime.Versioning;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

// Changes the Unix file mode of a file or directory and restores the original mode when disposed, so the temporary
// test directory can be deleted again. Skips the current test if the change has no effect, for example because the
// tests run as root.
[UnsupportedOSPlatform("windows")]
internal sealed class UnixFileModeScope : IDisposable
{
    private const UnixFileMode ReadAndExecute =
        UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private readonly string _path;

    private readonly UnixFileMode _originalMode;

    private UnixFileModeScope(string path, UnixFileMode mode)
    {
        _path = path;
        _originalMode = File.GetUnixFileMode(path);

        File.SetUnixFileMode(path, mode);
    }

    // Removes all permissions of a file.
    public static UnixFileModeScope MakeUnreadable(string filePath)
    {
        return Create(filePath, UnixFileMode.None, CanRead, "is readable without permissions");
    }

    // Removes the write permission of a directory.
    public static UnixFileModeScope MakeReadOnly(string directoryPath)
    {
        return Create(directoryPath, ReadAndExecute, CanWriteInto, "is writable without the write permission");
    }

    public void Dispose()
    {
        File.SetUnixFileMode(_path, _originalMode);
    }

    private static UnixFileModeScope Create(string path, UnixFileMode mode, Func<string, bool> isStillPermitted,
        string description)
    {
        var scope = new UnixFileModeScope(path, mode);

        if (isStillPermitted(path))
        {
            scope.Dispose();
            Assert.Skip($"'{path}' {description}, for example because the tests run as root.");
        }

        return scope;
    }

    private static bool CanRead(string filePath)
    {
        try
        {
            _ = File.ReadAllBytes(filePath);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool CanWriteInto(string directoryPath)
    {
        var probePath = Path.Combine(directoryPath, $".probe-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(probePath);
            Directory.Delete(probePath);

            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
