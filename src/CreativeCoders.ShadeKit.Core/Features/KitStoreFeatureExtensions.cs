using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Provides the knowledge about the features of a SHADE kit store that is shared by the feature services.
/// </summary>
internal static class KitStoreFeatureExtensions
{
    /// <summary>
    /// Determines whether the specified store has a feature with the specified id.
    /// </summary>
    /// <remarks>
    /// The id is compared ordinally with the real folder names, because a path check would ignore case on
    /// case-insensitive file systems. An invalid id never leads to a file system access.
    /// </remarks>
    /// <param name="store">The SHADE kit store.</param>
    /// <param name="featureId">The id of the feature.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="featureId" /> is a valid feature id and a feature folder whose name
    /// equals it ordinally exists; otherwise, <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    public static bool FeatureExists(this IKitStore store, string featureId)
    {
        Ensure.NotNull(store);

        return FeatureIds.IsValid(featureId)
               && store.GetFeatureNames().Contains(featureId, StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads the <c>feature.json</c> file of a feature whose folder exists.
    /// </summary>
    /// <param name="store">The SHADE kit store.</param>
    /// <param name="featureId">The id of the feature, which is the name of its existing folder.</param>
    /// <param name="access">
    /// <see cref="FormatAccess.Read" /> for a command that only reads the file, or <see cref="FormatAccess.Write" />
    /// for a command that changes it.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the document and its format
    /// warnings.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidKitFileException">
    /// The file is missing although the feature folder exists, or the file, or <c>config.json</c> when it is read for
    /// the <c>outdatedFormatOnRead</c> setting, is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// The file, or <c>config.json</c> when it is read for the setting, uses a format version newer than the engine
    /// supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// The file uses an older format version and either <paramref name="access" /> is <see cref="FormatAccess.Write" />
    /// or <c>outdatedFormatOnRead</c> is <c>stop</c>.
    /// </exception>
    public static async Task<VersionedReadResult<FeatureDocument>> ReadFeatureAsync(this IKitStore store,
        string featureId, FormatAccess access, CancellationToken cancellationToken)
    {
        Ensure.NotNull(store);

        var featureFilePath = KitStorePaths.GetFeatureFilePath(featureId);

        return await store
                   .ReadJsonAsync<FeatureDocument>(featureFilePath, FormatIds.Feature, access, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidKitFileException(KitStorePaths.GetRepositoryPath(featureFilePath),
                   "The file is missing, although the feature folder exists.");
    }

    /// <summary>
    /// Checks that the id recorded in a <c>feature.json</c> file is the name of its feature folder.
    /// </summary>
    /// <remarks>
    /// The folder name is the id of the feature; a different id in <c>feature.json</c> is reported, not corrected.
    /// </remarks>
    /// <param name="featureId">The name of the feature folder.</param>
    /// <param name="feature">The document read from the <c>feature.json</c> file of the folder.</param>
    /// <returns>
    /// A <see cref="FeatureIdMismatchWarning" /> if the recorded id differs ordinally from
    /// <paramref name="featureId" />; otherwise, an empty list.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="feature" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<KitWarning> CheckFeatureId(string featureId, FeatureDocument feature)
    {
        Ensure.NotNull(feature);

        return string.Equals(feature.Id, featureId, StringComparison.Ordinal)
            ? []
            : [new FeatureIdMismatchWarning(featureId, feature.Id)];
    }
}
