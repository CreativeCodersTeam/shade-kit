using System.Runtime.Versioning;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

// Removes all permissions of a file on Unix and restores the original mode when disposed. Skips the current test if
// the process can still read the file, for example because the tests run as root.
[UnsupportedOSPlatform("windows")]
internal sealed class UnreadableFileScope : IDisposable
{
    private readonly string _filePath;

    private readonly UnixFileMode _originalMode;

    public UnreadableFileScope(string filePath)
    {
        _filePath = filePath;
        _originalMode = File.GetUnixFileMode(filePath);

        File.SetUnixFileMode(filePath, UnixFileMode.None);

        if (!CanRead())
        {
            return;
        }

        Dispose();
        Assert.Skip($"'{filePath}' is readable without permissions, for example because the tests run as root.");
    }

    public void Dispose()
    {
        File.SetUnixFileMode(_filePath, _originalMode);
    }

    private bool CanRead()
    {
        try
        {
            _ = File.ReadAllBytes(_filePath);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
