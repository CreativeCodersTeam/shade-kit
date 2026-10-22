using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Provides the crash-safe directory protocols of a SHADE kit store, built on the primitives of
/// <see cref="IKitStore" />: creating a directory exclusively through a hidden staging directory, and deleting a
/// directory after it has been renamed to a hidden dropping directory.
/// </summary>
/// <remarks>
/// The hidden directories lie next to the directory they belong to and are named
/// <c>.&lt;name&gt;.&lt;8 hex digits&gt;.tmp</c> (staging) or <c>.&lt;name&gt;.&lt;8 hex digits&gt;.dropping</c>
/// (dropping); the random part keeps concurrent runs apart. A directory that a failed run leaves behind keeps its
/// hidden name, so a listing that skips the names for which <see cref="IsHiddenName" /> is <see langword="true" />
/// never shows it.
/// </remarks>
internal static class KitStoreDirectoryExtensions
{
    private const string StagingSuffix = ".tmp";

    private const string DroppingSuffix = ".dropping";

    /// <summary>
    /// Determines whether a directory name is hidden, as the names of all staging and dropping directories are.
    /// </summary>
    /// <param name="name">The name of a directory, a single path segment.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="name" /> starts with a dot; otherwise, <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="name" /> is <see langword="null" />.</exception>
    public static bool IsHiddenName(string name)
    {
        Ensure.NotNull(name);

        return name.StartsWith('.');
    }

    /// <summary>
    /// Creates a directory of the store exclusively: the directory is built in a hidden staging directory and moved
    /// into place in a single rename, so it appears only when it is complete, and an existing directory, also one
    /// created by a concurrent run, is never touched.
    /// </summary>
    /// <remarks>
    /// The steps are: create the staging directory, call <paramref name="populate" />, check for cancellation, and
    /// move the staging directory to <paramref name="relativePath" /> with <see cref="IKitStore.MoveDirectory" />. If
    /// any step fails or the operation is canceled, only the staging directory is deleted (best effort; a staging
    /// directory that cannot be deleted stays hidden) and the failure is thrown.
    /// </remarks>
    /// <param name="store">The SHADE kit store.</param>
    /// <param name="relativePath">
    /// The path of the directory to create relative to <see cref="IKitStore.RootPath" />, with <c>/</c> as separator,
    /// for example <c>features/login</c>.
    /// </param>
    /// <param name="populate">
    /// The function that writes the content of the directory; it receives the path of the staging directory relative
    /// to <see cref="IKitStore.RootPath" /> and the cancellation token. Its exceptions are thrown as they are.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous create operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="populate" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> ends with <c>/</c>, is empty, rooted, or points outside
    /// <see cref="IKitStore.RootPath" />.
    /// </exception>
    /// <exception cref="KitDirectoryExistsException">
    /// The move into place failed with an <see cref="IOException" /> or <see cref="UnauthorizedAccessException" />
    /// and a directory exists at <paramref name="relativePath" />.
    /// </exception>
    /// <exception cref="KitDirectoryIoFailedException">
    /// The staging directory cannot be created, or the move into place failed with an <see cref="IOException" /> or
    /// <see cref="UnauthorizedAccessException" /> while neither a directory nor a file exists at
    /// <paramref name="relativePath" />.
    /// </exception>
    /// <exception cref="IOException">
    /// The move into place failed because a file exists at <paramref name="relativePath" />; the exception of the
    /// move is thrown as it is.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled before the move into place.
    /// </exception>
    public static async Task CreateDirectoryExclusiveAsync(this IKitStore store, string relativePath,
        Func<string, CancellationToken, Task> populate, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(populate);

        var stagingPath = CreateHiddenPath(relativePath, StagingSuffix);

        try
        {
            CreateStagingDirectory(store, stagingPath, relativePath);

            await populate(stagingPath, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            MoveIntoPlace(store, stagingPath, relativePath);
        }
        catch
        {
            // Only the staging directory belongs to this run; the directory of another run is never removed.
            DeleteDirectoryBestEffort(store, stagingPath);
            throw;
        }
    }

    /// <summary>
    /// Removes a directory that <see cref="CreateDirectoryExclusiveAsync" /> has moved into place, best effort: it is
    /// moved back to a hidden staging directory and deleted there.
    /// </summary>
    /// <remarks>
    /// Nothing is thrown for an <see cref="IOException" /> or <see cref="UnauthorizedAccessException" />: if the
    /// move back fails, the directory stays in place; if the delete fails, the hidden staging directory stays.
    /// </remarks>
    /// <param name="store">The SHADE kit store.</param>
    /// <param name="relativePath">
    /// The path of the directory relative to <see cref="IKitStore.RootPath" />, for example <c>features/login</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> ends with <c>/</c>, is empty, rooted, or points outside
    /// <see cref="IKitStore.RootPath" />.
    /// </exception>
    public static void UndoCreateDirectoryExclusive(this IKitStore store, string relativePath)
    {
        Ensure.NotNull(store);

        var stagingPath = CreateHiddenPath(relativePath, StagingSuffix);

        try
        {
            store.MoveDirectory(relativePath, stagingPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The complete directory stays in place; the failure of the caller is more important.
            return;
        }

        DeleteDirectoryBestEffort(store, stagingPath);
    }

    /// <summary>
    /// Deletes a directory of the store after renaming it to a hidden dropping directory, so it disappears from its
    /// path at once, even if deleting its content fails.
    /// </summary>
    /// <remarks>
    /// The rename is atomic: if it fails, nothing is deleted. After it, nothing exists at
    /// <paramref name="relativePath" /> any more; a dropping directory that cannot be deleted stays hidden.
    /// </remarks>
    /// <param name="store">The SHADE kit store.</param>
    /// <param name="relativePath">
    /// The path of the directory relative to <see cref="IKitStore.RootPath" />, for example <c>features/login</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> ends with <c>/</c>, is empty, rooted, or points outside
    /// <see cref="IKitStore.RootPath" />.
    /// </exception>
    /// <exception cref="KitDirectoryIoFailedException">
    /// The rename failed with an <see cref="IOException" /> or <see cref="UnauthorizedAccessException" />; nothing
    /// was changed.
    /// </exception>
    /// <exception cref="IOException">
    /// The rename succeeded, but the dropping directory cannot be deleted; the exception of the delete is thrown as it
    /// is.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The rename succeeded, but the process is not allowed to delete the dropping directory.
    /// </exception>
    public static void DeleteDirectoryAtomically(this IKitStore store, string relativePath)
    {
        Ensure.NotNull(store);

        var droppingPath = CreateHiddenPath(relativePath, DroppingSuffix);

        try
        {
            store.MoveDirectory(relativePath, droppingPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new KitDirectoryIoFailedException(relativePath, exception);
        }

        store.DeleteDirectory(droppingPath);
    }

    // The hidden directory lies next to the directory; its name starts with a dot.
    private static string CreateHiddenPath(string relativePath, string suffix)
    {
        Ensure.NotNull(relativePath);

        var nameStart = relativePath.LastIndexOf('/') + 1;

        if (nameStart == relativePath.Length)
        {
            throw new ArgumentException($"The path '{relativePath}' does not name a directory.",
                nameof(relativePath));
        }

        return $"{relativePath[..nameStart]}.{relativePath[nameStart..]}.{Guid.NewGuid().ToString("N")[..8]}{suffix}";
    }

    private static void CreateStagingDirectory(IKitStore store, string stagingPath, string relativePath)
    {
        try
        {
            store.CreateDirectory(stagingPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Usually the parent directory is not writable; nothing exists yet that would have to be removed.
            throw new KitDirectoryIoFailedException(relativePath, exception);
        }
    }

    private static void MoveIntoPlace(IKitStore store, string stagingPath, string relativePath)
    {
        try
        {
            store.MoveDirectory(stagingPath, relativePath);
        }
        catch (Exception exception) when ((exception is IOException or UnauthorizedAccessException)
                                          && store.DirectoryExists(relativePath))
        {
            // Another run created the directory after the caller checked that it does not exist.
            throw new KitDirectoryExistsException(relativePath, exception);
        }
        catch (Exception exception) when ((exception is IOException or UnauthorizedAccessException)
                                          && !store.FileExists(relativePath))
        {
            // The directory cannot be renamed, for example because the parent directory is not writable or a file in
            // the staging directory is held open. A file at the destination is not wrapped and stays an unexpected
            // failure.
            throw new KitDirectoryIoFailedException(relativePath, exception);
        }
    }

    private static void DeleteDirectoryBestEffort(IKitStore store, string relativePath)
    {
        try
        {
            store.DeleteDirectory(relativePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The hidden directory stays in place and is ignored; the original failure is more important.
        }
    }
}
