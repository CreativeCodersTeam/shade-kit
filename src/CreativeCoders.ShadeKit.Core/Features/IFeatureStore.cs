using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Creates, lists, reads, selects, and drops the features of a SHADE kit store, as the <c>shade-kit feature</c>
/// commands do.
/// </summary>
/// <remarks>
/// A feature exists if its folder <c>features/&lt;id&gt;</c> exists. The folder name is the id in every result; a
/// different <c>id</c> in <c>feature.json</c> gives a <see cref="FeatureIdMismatchWarning" />. Folders whose names
/// start with a dot, such as staging and dropping folders, are no features. Every feature id is checked with
/// <see cref="FeatureIds.EnsureValid" /> before a path is built from it.
/// </remarks>
public interface IFeatureStore
{
    /// <summary>
    /// Creates a new feature in the specified store and makes it the current feature, as
    /// <c>shade-kit feature new</c> does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything is checked before anything is written. The feature is built in a hidden staging folder and moved
    /// into place, so the feature folder appears only when complete; the local override is written last and replaces
    /// an unusable one. <c>request.md</c> holds the request content loaded from the source (the text, the file
    /// content, the issue formatted as Markdown, or the content of the URL), unchanged except for its line endings.
    /// </para>
    /// <para>
    /// A failure or cancellation before the move deletes only the staging folder; an existing feature folder is never
    /// touched. If writing the override fails, the new feature folder is removed again (best effort).
    /// </para>
    /// </remarks>
    /// <param name="store">The SHADE kit store to create the feature in.</param>
    /// <param name="request">The source, title, workflow profile, and working directory of the new feature.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous create operation. The task result contains the new
    /// <c>feature.json</c> document, the paths of the feature, the suggested branch name, the format warnings, and the
    /// warnings of the files that this call wrote (see <see cref="IKitStore.WriteFileAsync" />); warnings of earlier
    /// writes of <paramref name="store" /> are not included.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The root directory of <paramref name="store" /> has no parent directory, or the working directory of
    /// <paramref name="request" /> is empty.
    /// </exception>
    /// <exception cref="UnknownProfileException">
    /// The workflow profile, or <c>defaultProfile</c> of <c>config.json</c>, is not one of
    /// <see cref="WorkflowProfiles.All" />.
    /// </exception>
    /// <exception cref="InvalidFeatureSourceException">
    /// The source is empty; it is a URL of a tracker in <c>config.json</c> without an issue number or whose issue
    /// cannot be read with <c>gh</c>; it is any other URL whose content cannot be read; or it names a file that cannot
    /// be read, is larger than <see cref="FeatureSourceReader.MaxSourceLength" /> bytes (1 MiB), or is not UTF-8 text.
    /// </exception>
    /// <exception cref="InvalidFeatureTitleException">
    /// A URL source whose host is not a tracker in <c>config.json</c> has no title, the title contains a control
    /// character, or the title contains no letter or digit for the slug of the id.
    /// </exception>
    /// <exception cref="FeatureAlreadyExistsException">
    /// The folder of the new feature id already exists, or another run created it before the new feature was moved
    /// into place.
    /// </exception>
    /// <exception cref="GitIdentityNotConfiguredException">
    /// <c>user.name</c>, <c>user.email</c>, or both are not configured for the repository.
    /// </exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// <c>config.json</c> is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// <c>config.json</c> uses a format version newer than the engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// <c>config.json</c> uses an older format version and <c>outdatedFormatOnRead</c> is <c>stop</c>.
    /// </exception>
    /// <exception cref="FeatureIoFailedException">
    /// The hidden staging folder cannot be created in the <c>features</c> folder, or it cannot be moved to the
    /// feature folder, for example because the process may not write to the <c>features</c> folder or another
    /// program holds a file open; nothing was changed.
    /// </exception>
    /// <exception cref="IOException">
    /// A file of the new feature or the local override cannot be written, or a file exists at the path of the
    /// feature folder.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    Task<NewFeatureResult> CreateAsync(IKitStore store, NewFeatureRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the features of the specified store, as <c>shade-kit feature list</c> does.
    /// </summary>
    /// <remarks>
    /// A folder with an invalid name gives a <see cref="FeatureFolderInvalidWarning" />, a folder without
    /// <c>feature.json</c> a <see cref="FeatureFileMissingWarning" />; both are skipped.
    /// </remarks>
    /// <param name="store">The SHADE kit store whose features are listed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous list operation. The task result contains the features sorted ordinally
    /// by id, together with the feature and format warnings.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidKitFileException">A <c>feature.json</c>, or <c>config.json</c>, is invalid.</exception>
    /// <exception cref="FormatTooNewException">
    /// A <c>feature.json</c>, or <c>config.json</c>, uses a format version newer than the engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// A <c>feature.json</c> uses an older format version and <c>outdatedFormatOnRead</c> is <c>stop</c>.
    /// </exception>
    Task<FeatureListResult> ListAsync(IKitStore store, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the feature with the specified id from the specified store, as <c>shade-kit feature show</c> does.
    /// </summary>
    /// <param name="store">The SHADE kit store that contains the feature.</param>
    /// <param name="featureId">The id of the feature to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the id, the
    /// <c>feature.json</c> document, and the paths of the feature, together with the feature and format warnings.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="featureId" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidFeatureIdException"><paramref name="featureId" /> is not a valid feature id.</exception>
    /// <exception cref="FeatureNotFoundException">The folder of the feature does not exist.</exception>
    /// <exception cref="InvalidKitFileException">
    /// The <c>feature.json</c> of the feature is missing or invalid, or <c>config.json</c> is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// The <c>feature.json</c> of the feature, or <c>config.json</c>, uses a format version newer than the engine
    /// supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// The <c>feature.json</c> of the feature uses an older format version and <c>outdatedFormatOnRead</c> is
    /// <c>stop</c>.
    /// </exception>
    Task<FeatureShowResult> ShowAsync(IKitStore store, string featureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves and reads the current feature of the specified store, as <c>shade-kit feature current</c> does.
    /// </summary>
    /// <remarks>
    /// A current feature that cannot be resolved is a result, not an error; see
    /// <see cref="ICurrentFeatureResolver.ResolveAsync" />.
    /// </remarks>
    /// <param name="store">The SHADE kit store whose current feature is read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the id, the
    /// <c>feature.json</c> document, the origin, and the paths of the current feature, or none of them if no current
    /// feature is resolved, together with the feature and format warnings.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// The root directory of <paramref name="store" /> has no parent directory.
    /// </exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// The <c>feature.json</c> of the current feature, or <c>config.json</c>, is invalid, or the <c>feature.json</c> of
    /// the current feature is missing.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// The <c>feature.json</c> of the current feature, or <c>config.json</c>, uses a format version newer than the
    /// engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// The <c>feature.json</c> of the current feature uses an older format version and <c>outdatedFormatOnRead</c> is
    /// <c>stop</c>.
    /// </exception>
    Task<CurrentFeatureResult> GetCurrentAsync(IKitStore store, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the feature with the specified id the current feature by writing the local override
    /// <c>features/current-feature.json</c>, as <c>shade-kit feature set</c> does.
    /// </summary>
    /// <remarks>
    /// The branch is resolved first, so a failing <c>git</c> leaves the override unchanged. If the branch selects
    /// another feature, the override is still written with a <see cref="CurrentFeatureMismatchWarning" />. An unusable
    /// existing override is replaced.
    /// </remarks>
    /// <param name="store">The SHADE kit store that contains the feature.</param>
    /// <param name="featureId">The id of the feature to make current.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous set operation. The task result contains the feature id and the
    /// warnings.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="featureId" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The root directory of <paramref name="store" /> has no parent directory.
    /// </exception>
    /// <exception cref="InvalidFeatureIdException"><paramref name="featureId" /> is not a valid feature id.</exception>
    /// <exception cref="FeatureNotFoundException">The folder of the feature does not exist.</exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="IOException">The local override cannot be written or replaced.</exception>
    Task<SetCurrentFeatureResult> SetCurrentAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes what <see cref="DropAsync" /> would delete for the feature with the specified id, without changing
    /// anything.
    /// </summary>
    /// <remarks>
    /// The local override is affected only if it can be read and selects the feature.
    /// </remarks>
    /// <param name="store">The SHADE kit store that contains the feature.</param>
    /// <param name="featureId">The id of the feature that would be dropped.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous preview operation. The task result contains every file below the
    /// feature folder and whether the local override would be cleared.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="featureId" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidFeatureIdException"><paramref name="featureId" /> is not a valid feature id.</exception>
    /// <exception cref="FeatureNotFoundException">The folder of the feature does not exist.</exception>
    Task<FeatureDropPreview> PreviewDropAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the feature with the specified id by deleting its folder, and clears the local override if it selects
    /// the feature, as <c>shade-kit feature drop --yes</c> does.
    /// </summary>
    /// <remarks>
    /// The feature is dropped even if its <c>feature.json</c> is missing or invalid. The folder is renamed to a hidden
    /// dropping folder first, so a failed delete leaves no visible feature. The override is cleared under the rule of
    /// <see cref="PreviewDropAsync" />.
    /// </remarks>
    /// <param name="store">The SHADE kit store that contains the feature.</param>
    /// <param name="featureId">The id of the feature to drop.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous drop operation. The task result contains every file that was below
    /// the feature folder, with its path in <c>features/&lt;id&gt;</c>, and whether the local override selected the
    /// feature and was cleared.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="featureId" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidFeatureIdException"><paramref name="featureId" /> is not a valid feature id.</exception>
    /// <exception cref="FeatureNotFoundException">The folder of the feature does not exist.</exception>
    /// <exception cref="FeatureIoFailedException">
    /// The feature folder cannot be renamed to the hidden folder, for example because the process may not write to
    /// the <c>features</c> folder or another program holds a file in the feature folder open; nothing was changed.
    /// </exception>
    /// <exception cref="IOException">
    /// A file of the hidden folder or the local override cannot be deleted after the rename.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The process is not allowed to delete a file of the hidden folder or the local override after the rename.
    /// </exception>
    Task<FeatureDropResult> DropAsync(IKitStore store, string featureId, CancellationToken cancellationToken = default);
}
