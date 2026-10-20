namespace CreativeCoders.ShadeKit.Tests.Shared;

/// <summary>
/// Restores the current directory of the process on disposal, so that a test can change it.
/// </summary>
/// <remarks>
/// The current directory is process-wide; tests that change it must run in a collection that is not run in
/// parallel with other tests.
/// </remarks>
internal sealed class CurrentDirectoryScope : IDisposable
{
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentDirectoryScope" /> class that keeps the current directory.
    /// </summary>
    public CurrentDirectoryScope()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentDirectoryScope" /> class and makes the specified directory
    /// the current directory.
    /// </summary>
    /// <param name="path">The directory to make the current directory.</param>
    public CurrentDirectoryScope(string path)
    {
        Directory.SetCurrentDirectory(path);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
    }
}
