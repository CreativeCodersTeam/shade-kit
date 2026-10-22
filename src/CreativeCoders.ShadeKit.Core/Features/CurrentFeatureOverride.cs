using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Reads and writes the local override <c>features/current-feature.json</c> of a SHADE kit store.
/// </summary>
/// <remarks>
/// The override is a pure pointer to a feature that is local to a clone and holds no other data. An override that
/// cannot be used because of its own format is therefore reported to the caller instead of stopping the command, and
/// it is replaced when it is written. Format problems of every other file, for example of <c>config.json</c> while the
/// policy for an outdated override is read, still stop the command.
/// </remarks>
internal static class CurrentFeatureOverride
{
    /// <summary>
    /// Reads the local override of the specified store for a command that only reads it and returns a format problem
    /// of the override itself instead of throwing it.
    /// </summary>
    /// <param name="store">The SHADE kit store whose override is read.</param>
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
        ReadAsync(IKitStore store, CancellationToken cancellationToken)
    {
        // With read access, an outdated override makes the store read config.json for its outdatedFormatOnRead
        // setting, so only the path tells a problem of the override from a problem of config.json. The store builds
        // that path with KitStorePaths.GetDisplayPath, so the comparison uses the same method.
        try
        {
            var result = await store
                .ReadJsonAsync<CurrentFeatureDocument>(KitStorePaths.CurrentFeatureFilePath, FormatIds.CurrentFeature,
                    FormatAccess.Read, cancellationToken)
                .ConfigureAwait(false);

            return (result, null);
        }
        catch (KitFileFormatException exception) when (string.Equals(exception.FilePath,
                                                           KitStorePaths.GetDisplayPath(
                                                               KitStorePaths.CurrentFeatureFilePath),
                                                           StringComparison.Ordinal))
        {
            return (null, exception);
        }
    }

    /// <summary>
    /// Writes the local override of the specified store so that it selects the specified feature.
    /// </summary>
    /// <remarks>
    /// The override is written with <see cref="FormatAccess.Replace" /> access, so an existing override that cannot be
    /// used because of its own format (invalid, too new, or outdated) is deleted and written again instead of stopping
    /// the write.
    /// </remarks>
    /// <param name="store">The SHADE kit store whose override is written.</param>
    /// <param name="featureId">The id of the feature the override selects.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static async Task WriteAsync(IKitStore store, string featureId, CancellationToken cancellationToken)
    {
        await store
            .WriteJsonAsync(KitStorePaths.CurrentFeatureFilePath, FormatIds.CurrentFeature,
                new CurrentFeatureDocument { FeatureId = featureId }, FormatAccess.Replace, cancellationToken)
            .ConfigureAwait(false);
    }
}
