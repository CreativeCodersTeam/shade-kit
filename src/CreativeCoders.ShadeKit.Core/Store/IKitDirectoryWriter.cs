using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Writes the files of a directory of the SHADE kit store that is moved to its final path after it is complete.
/// </summary>
/// <remarks>
/// The paths are relative to the directory. Each file is written below the directory, but its line ending is
/// determined for the path where it ends up; see <see cref="IKitStore.CreateDirectoryWriter" />.
/// </remarks>
public interface IKitDirectoryWriter
{
    /// <summary>
    /// Writes a versioned JSON file of the directory as <see cref="IKitStore.WriteJsonAsync{T}" /> does.
    /// </summary>
    /// <remarks>
    /// Besides the exceptions listed here, the write throws those of <see cref="IKitStore.WriteJsonAsync{T}" />.
    /// </remarks>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="relativePath">The path of the file relative to the directory, for example <c>feature.json</c>.</param>
    /// <param name="formatId">The id of the file type, for example <see cref="FormatIds.Feature" />.</param>
    /// <param name="document">The document to write.</param>
    /// <param name="access">The access with which the file is written, as for <see cref="IKitStore.WriteJsonAsync{T}" />.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous write operation. The task result contains the warnings that occurred
    /// while the line ending of the file was determined; empty if there were none.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside the directory; or no file type with the id
    /// <paramref name="formatId" /> is registered.
    /// </exception>
    Task<IReadOnlyList<KitWarning>> WriteJsonAsync<T>(string relativePath, string formatId, T document,
        FormatAccess access, CancellationToken cancellationToken = default)
        where T : VersionedDocument;

    /// <summary>
    /// Writes a text file of the directory as <see cref="IKitStore.WriteFileAsync" /> does.
    /// </summary>
    /// <param name="relativePath">The path of the file relative to the directory, for example <c>request.md</c>.</param>
    /// <param name="content">The content of the file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous write operation. The task result contains the warnings that occurred
    /// while the line ending of the file was determined; empty if there were none.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="content" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside the directory.
    /// </exception>
    Task<IReadOnlyList<KitWarning>> WriteFileAsync(string relativePath, TextFileContent content,
        CancellationToken cancellationToken = default);
}
