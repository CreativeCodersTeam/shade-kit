using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Store.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the file system refuses to write a file of the SHADE kit store.
/// </summary>
/// <remarks>
/// Thrown for an <see cref="IOException" /> of the write, for example when a file exists in the place of one of the
/// parent directories of the file, so the directory cannot be created.
/// </remarks>
/// <param name="filePath">
/// The repository-relative path of the file that cannot be written, for example
/// <c>.shadekit/features/login/checks/definition-of-ready.md</c>.
/// </param>
/// <param name="innerException">The <see cref="IOException" /> of the file system that caused this exception.</param>
public class KitFileWriteFailedException(string filePath, Exception innerException)
    : Exception($"The file '{filePath}' cannot be written: {Ensure.NotNull(innerException).Message}", innerException)
{
    /// <summary>
    /// Gets the path of the file that cannot be written.
    /// </summary>
    /// <value>
    /// The repository-relative path with <c>/</c> as separator, for example
    /// <c>.shadekit/features/login/checks/definition-of-ready.md</c>.
    /// </value>
    public string FilePath { get; } = filePath;
}
