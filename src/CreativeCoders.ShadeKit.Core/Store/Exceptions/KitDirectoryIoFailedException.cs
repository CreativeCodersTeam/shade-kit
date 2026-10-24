using System.Diagnostics.CodeAnalysis;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Store.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the file system refuses a step of a store directory protocol that
/// changes nothing when it fails.
/// </summary>
/// <remarks>
/// Thrown by <see cref="KitStoreDirectoryExtensions.CreateDirectoryExclusiveAsync" /> and
/// <see cref="KitStoreDirectoryExtensions.DeleteDirectoryAtomically" />; nothing was changed. Callers map it to an
/// exception of their own.
/// </remarks>
/// <param name="relativePath">The path of the directory relative to <see cref="IKitStore.RootPath" />.</param>
/// <param name="fileSystemException">
/// The exception of the file system that caused this exception, an <see cref="IOException" /> or an
/// <see cref="UnauthorizedAccessException" />.
/// </param>
[SuppressMessage("Major Code Smell", "S3871:Exception types should be \"public\"",
    Justification = "Thrown only by KitStoreDirectoryExtensions and always mapped by its callers in Core to a public " +
                    "exception, so it never leaves the assembly; a public type would need a CLI mapping it never uses.")]
internal sealed class KitDirectoryIoFailedException(string relativePath, Exception fileSystemException)
    : Exception($"The directory '{relativePath}' cannot be changed.", Ensure.NotNull(fileSystemException))
{
    /// <summary>
    /// Gets the path of the directory that cannot be changed.
    /// </summary>
    /// <value>The path of the directory relative to <see cref="IKitStore.RootPath" />.</value>
    public string RelativePath { get; } = relativePath;

    /// <summary>
    /// Gets the exception of the file system that caused this exception.
    /// </summary>
    /// <value>The same exception as <see cref="Exception.InnerException" />, never <see langword="null" />.</value>
    public Exception FileSystemException { get; } = fileSystemException;
}
