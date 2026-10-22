using System.Diagnostics.CodeAnalysis;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Represents the exception that is thrown when a directory of the store cannot be created exclusively, because
/// another directory appeared at its path, for example one created by a concurrent run.
/// </summary>
/// <remarks>
/// The exception is thrown by
/// <see cref="KitStoreDirectoryExtensions.CreateDirectoryExclusiveAsync" /> after its staging directory has been
/// removed (best effort); the existing directory is untouched. Callers map it to an exception of their own.
/// </remarks>
/// <param name="relativePath">The path of the directory relative to <see cref="IKitStore.RootPath" />.</param>
/// <param name="fileSystemException">
/// The exception of the file system that refused the move into place, usually an <see cref="IOException" /> or an
/// <see cref="UnauthorizedAccessException" />.
/// </param>
[SuppressMessage("Major Code Smell", "S3871:Exception types should be \"public\"",
    Justification = "Thrown only by KitStoreDirectoryExtensions and always mapped by its callers in Core to a public " +
                    "exception, so it never leaves the assembly; a public type would need a CLI mapping it never uses.")]
internal sealed class KitDirectoryExistsException(string relativePath, Exception fileSystemException)
    : Exception($"The directory '{relativePath}' already exists.", Ensure.NotNull(fileSystemException))
{
    /// <summary>
    /// Gets the path of the directory that already exists.
    /// </summary>
    /// <value>The path of the directory relative to <see cref="IKitStore.RootPath" />.</value>
    public string RelativePath { get; } = relativePath;

    /// <summary>
    /// Gets the exception of the file system that caused this exception.
    /// </summary>
    /// <value>The same exception as <see cref="Exception.InnerException" />, never <see langword="null" />.</value>
    public Exception FileSystemException { get; } = fileSystemException;
}
