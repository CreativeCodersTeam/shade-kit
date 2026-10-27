using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Reads and writes the local override <c>features/current-feature.json</c> of a SHADE kit store.
/// </summary>
/// <remarks>
/// The override is a pure pointer, so a format problem of the override itself is reported instead of stopping the
/// command, and the override is replaced when written. Format problems of other files still stop the command.
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
    /// Determines whether the local override of the specified store can be read and selects the specified feature.
    /// </summary>
    /// <remarks>
    /// An override that cannot be read because of its format, or because of <c>config.json</c>, selects no feature
    /// that can be proven, so it counts as not selecting the feature and is left alone.
    /// </remarks>
    /// <param name="store">The SHADE kit store whose override is read.</param>
    /// <param name="featureId">The id of the feature.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result is <see langword="true" /> if the
    /// override exists, can be read, and selects <paramref name="featureId" />; otherwise, <see langword="false" />.
    /// </returns>
    public static async Task<bool> SelectsAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await store
                .ReadJsonAsync<CurrentFeatureDocument>(KitStorePaths.CurrentFeatureFilePath, FormatIds.CurrentFeature,
                    FormatAccess.Read, cancellationToken)
                .ConfigureAwait(false);

            return result is not null && string.Equals(result.Document.FeatureId, featureId, StringComparison.Ordinal);
        }
        catch (KitFileFormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the local override of the specified store so that it selects the specified feature.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="FormatAccess.Replace" />, so an existing override that is invalid, too new, or outdated is
    /// replaced.
    /// </remarks>
    /// <param name="store">The SHADE kit store whose override is written.</param>
    /// <param name="featureId">The id of the feature the override selects.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous write operation. The task result contains the warnings of the write
    /// (see <see cref="IKitStore.WriteJsonAsync{T}" />).
    /// </returns>
    public static async Task<IReadOnlyList<KitWarning>> WriteAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken)
    {
        return await store
            .WriteJsonAsync(KitStorePaths.CurrentFeatureFilePath, FormatIds.CurrentFeature,
                new CurrentFeatureDocument { FeatureId = featureId }, FormatAccess.Replace,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
