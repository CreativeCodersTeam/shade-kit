using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Creates, lists, reads, selects, and drops the features of a SHADE kit store, as the <c>shade-kit feature</c>
/// commands do.
/// </summary>
/// <remarks>
/// <para>
/// A feature exists if and only if its folder <c>features/&lt;id&gt;</c> of the store exists. The folder name is the id
/// of the feature in every result; an <c>id</c> in <c>feature.json</c> that differs from it is reported with a
/// <see cref="FeatureIdMismatchWarning" /> warning, and the file is not changed. Every method that takes
/// a feature id checks it with <see cref="FeatureIds.EnsureValid" /> before a path is built from it.
/// </para>
/// <para>
/// Folders in <c>features</c> whose names start with a dot are not features: <see cref="CreateAsync" /> builds a new
/// feature in <c>features/.&lt;id&gt;.&lt;random&gt;.tmp</c>, and <see cref="DropAsync" /> deletes a feature as
/// <c>features/.&lt;id&gt;.&lt;random&gt;.dropping</c>. Such folders, also those that a failed run left behind, are
/// ignored.
/// </para>
/// <para>
/// The repository root is the parent directory of <see cref="IKitStore.RootPath" />, because the SHADE kit root
/// directory <c>.shadekit</c> lies at the root of the git repository. <c>git</c> is run there.
/// </para>
/// </remarks>
public interface IFeatureStore
{
    /// <summary>
    /// Creates a new feature in the specified store and makes it the current feature, as
    /// <c>shade-kit feature new</c> does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The workflow profile, the id, the git identity, and the local override are checked before anything is written.
    /// Then <c>feature.json</c> (status <see cref="FeatureStatus.Active" />) and <c>request.md</c> are written to the
    /// hidden staging folder <c>features/.&lt;id&gt;.&lt;random&gt;.tmp</c>, the staging folder is moved to
    /// <c>features/&lt;id&gt;</c> with <see cref="IKitStore.MoveDirectory" />, so the feature folder appears only when
    /// it is complete, and the local override <c>features/current-feature.json</c> is written last. A local override
    /// that cannot be used because of its own format (invalid, too new, or outdated) is replaced: it is deleted, then
    /// written.
    /// </para>
    /// <para>
    /// <c>request.md</c> is created from the repository template <c>templates/request.md</c>, or from the shipped
    /// template if the repository has none, and written as UTF-8 without byte order mark. The whole file, the original
    /// input included, is written with the line ending that the repository prescribes for it, as are
    /// <c>feature.json</c> and the local override.
    /// </para>
    /// <para>
    /// If a step up to the move fails or is cancelled, only the staging folder is deleted (best effort) and the
    /// exception is rethrown; an existing feature folder is never touched. If another run creates the feature folder
    /// between the existence check and the move, the move fails and <see cref="FeatureAlreadyExistsException" /> is
    /// thrown. If the staging folder cannot be created or moved for another reason, such as missing write permission
    /// or a file that another program holds open, <see cref="FeatureIoFailedException" /> is thrown. If writing the local override fails, the new feature folder is moved back to a hidden folder and
    /// deleted (best effort), so the override never selects a feature that was removed; a broken override that was
    /// deleted for the replacement stays deleted.
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
    /// The source is empty; it is a URL of a tracker in <c>config.json</c> without an issue number; or it names a file
    /// that cannot be read, is larger than <see cref="FeatureIdFactory.MaxSourceFileSize" /> bytes (1 MiB), or is not
    /// UTF-8 text.
    /// </exception>
    /// <exception cref="InvalidFeatureTitleException">
    /// A URL source has no title, the title contains a control character, or the title contains no letter or digit
    /// for the slug of the id.
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
    /// <c>config.json</c> is invalid, or the repository template <c>templates/request.md</c> is not UTF-8 text.
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
    /// Every <c>feature.json</c> is read with <see cref="FormatAccess.Read" /> access, and the name of its folder is
    /// listed as the id. Folders whose names start with a dot are ignored without a warning. A folder whose name is
    /// not a valid feature id is skipped with a <see cref="FeatureFolderInvalidWarning" /> warning, and a
    /// feature folder without <c>feature.json</c> with a <see cref="FeatureFileMissingWarning" />
    /// warning. A <c>feature.json</c> whose <c>id</c> differs from the folder name gives a
    /// <see cref="FeatureIdMismatchWarning" /> warning.
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
    /// The current feature is resolved with <see cref="ICurrentFeatureResolver.ResolveAsync" />, which ignores a local
    /// override that cannot be used with a <see cref="CurrentFeatureInvalidWarning" /> warning. A current
    /// feature that cannot be resolved is a result, not an error.
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
    /// The current branch is resolved before the override is written, so a failing <c>git</c> leaves the override
    /// unchanged. If the branch selects another existing feature, the override is written anyway and a
    /// <see cref="CurrentFeatureMismatchWarning" /> warning is returned, because the branch wins. An
    /// existing override that cannot be used because of its own format (invalid, too new, or outdated) is replaced:
    /// it is deleted, then written. The warnings of writing the override (see <see cref="IKitStore.WriteJsonAsync{T}" />)
    /// are returned as well; warnings of earlier writes of <paramref name="store" /> are not included.
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
    /// The local override is affected if it can be read with <see cref="FormatAccess.Read" /> access and selects the
    /// feature. A local override that is invalid or uses an unsupported format version is not affected; it is left
    /// alone.
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
    /// <para>
    /// The feature is dropped even if its <c>feature.json</c> is missing or invalid. The feature folder is first
    /// renamed to the hidden folder <c>features/.&lt;id&gt;.&lt;random&gt;.dropping</c> with
    /// <see cref="IKitStore.MoveDirectory" />; if the rename fails, nothing is changed and
    /// <see cref="FeatureIoFailedException" /> is thrown. Then the hidden folder is
    /// deleted, and the local override is cleared by deleting <c>features/current-feature.json</c> under the rule of
    /// <see cref="PreviewDropAsync" />; an override that selects another feature, is invalid, or uses an unsupported
    /// format version is left alone.
    /// </para>
    /// <para>
    /// If deleting the hidden folder fails, the feature is dropped already: it no longer exists for any method of
    /// this interface, and the rest of its files stay in the ignored hidden folder. The override is then still
    /// cleared (best effort) and the exception of the delete is rethrown.
    /// </para>
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
