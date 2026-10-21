using System.Runtime.Versioning;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

// Removes the write permission of a directory on Unix and restores the original mode when disposed, so the temporary
// test directory can be deleted again.
[UnsupportedOSPlatform("windows")]
internal sealed class ReadOnlyDirectoryScope : IDisposable
{
    private readonly string _directoryPath;

    private readonly UnixFileMode _originalMode;

    public ReadOnlyDirectoryScope(string directoryPath)
    {
        _directoryPath = directoryPath;
        _originalMode = File.GetUnixFileMode(directoryPath);

        File.SetUnixFileMode(directoryPath,
            UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    // False when the process may still write, for example because the tests run as root.
    public bool IsEffective()
    {
        var probePath = Path.Combine(_directoryPath, $".probe-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(probePath);
            Directory.Delete(probePath);

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }

    public void Dispose()
    {
        File.SetUnixFileMode(_directoryPath, _originalMode);
    }
}
