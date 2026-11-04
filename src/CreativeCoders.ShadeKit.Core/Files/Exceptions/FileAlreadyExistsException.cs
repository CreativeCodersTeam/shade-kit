namespace CreativeCoders.ShadeKit.Core.Files.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a file is written with <see cref="FileWriteMode.CreateNew" />, but a
/// file already exists at its path.
/// </summary>
/// <remarks>
/// It derives from <see cref="IOException" />, the exception that the file system reports; the existing file is not
/// changed.
/// </remarks>
/// <param name="path">
/// The path of the existing file relative to the repository root, with <c>/</c> as separator, for example
/// <c>.shadekit/features/login/checks/definition-of-ready.md</c>.
/// </param>
/// <param name="innerException">The exception of the file system, or <see langword="null" /> if there is none.</param>
public class FileAlreadyExistsException(string path, Exception? innerException)
    : IOException($"The file '{path}' already exists.", innerException)
{
    /// <summary>
    /// Gets the path of the existing file.
    /// </summary>
    /// <value>
    /// The path of the file relative to the repository root, with <c>/</c> as separator, as
    /// <see cref="RepositoryFile.GitPath" /> gives it.
    /// </value>
    public string Path { get; } = path;
}
