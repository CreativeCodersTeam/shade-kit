using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Reads and writes the local override <c>features/current-feature.json</c> of a SHADE kit store.
/// </summary>
/// <remarks>
/// The override is a pure pointer to a feature that is local to a clone and holds no other data. An override that
/// cannot be used because of its own format is therefore reported to the caller instead of stopping the command, and
/// it may be replaced. Format problems of every other file, for example of <c>config.json</c> while the policy for an
/// outdated override is read, still stop the command.
/// </remarks>
internal static class CurrentFeatureOverride
{
    /// <summary>
    /// Represents the path of the local override relative to the SHADE kit root directory.
    /// </summary>
    public const string FilePath = KitStorePaths.CurrentFeatureFilePath;

    /// <summary>
    /// Reads the local override of the specified store and returns a format problem of the override itself instead
    /// of throwing it.
    /// </summary>
    /// <param name="store">The SHADE kit store whose override is read.</param>
    /// <param name="access">One of the enumeration values that specifies the format access of the read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the read result, which is
    /// <see langword="null" /> if the override does not exist or cannot be used, and the format exception of the
    /// override if it cannot be used.
    /// </returns>
    /// <exception cref="KitFileFormatException">
    /// A file other than the override, such as <c>config.json</c>, stops the read.
    /// </exception>
    public static async Task<(VersionedReadResult<CurrentFeatureDocument>? Result, KitFileFormatException? Problem)>
        ReadAsync(IKitStore store, FormatAccess access, CancellationToken cancellationToken)
    {
        try
        {
            var result = await store
                .ReadJsonAsync<CurrentFeatureDocument>(FilePath, FormatIds.CurrentFeature, access, cancellationToken)
                .ConfigureAwait(false);

            return (result, null);
        }
        catch (KitFileFormatException exception) when (string.Equals(exception.FilePath,
                                                           KitStorePaths.GetRepositoryPath(FilePath),
                                                           StringComparison.Ordinal))
        {
            return (null, exception);
        }
    }

    /// <summary>
    /// Writes the local override of the specified store so that it selects the specified feature.
    /// </summary>
    /// <remarks>
    /// An override that cannot be used is deleted first, because <see cref="IKitStore.WriteJsonAsync{T}" /> refuses to
    /// replace a file whose format it cannot use.
    /// </remarks>
    /// <param name="store">The SHADE kit store whose override is written.</param>
    /// <param name="featureId">The id of the feature the override selects.</param>
    /// <param name="replaceUnusable">
    /// <see langword="true" /> to delete the existing override first because it cannot be used; otherwise,
    /// <see langword="false" />.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static async Task WriteAsync(IKitStore store, string featureId, bool replaceUnusable,
        CancellationToken cancellationToken)
    {
        if (replaceUnusable)
        {
            store.DeleteFile(FilePath);
        }

        await store
            .WriteJsonAsync(FilePath, FormatIds.CurrentFeature, new CurrentFeatureDocument { FeatureId = featureId },
                FormatAccess.Write, cancellationToken)
            .ConfigureAwait(false);
    }
}
