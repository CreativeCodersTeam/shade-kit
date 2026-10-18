using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Provides access to the content of a SHADE kit store, the <c>.shadekit</c> directory at the root of a git
/// repository.
/// </summary>
public interface IKitStore
{
    /// <summary>
    /// Gets the full path of the SHADE kit root directory.
    /// </summary>
    /// <value>The full path of the <c>.shadekit</c> directory, without a trailing directory separator.</value>
    string RootPath { get; }

    /// <summary>
    /// Gets the names of all features in the SHADE kit store.
    /// </summary>
    /// <returns>
    /// The names of the directories in the <c>features</c> directory of the store, or an empty sequence if the store
    /// contains no <c>features</c> directory.
    /// </returns>
    IEnumerable<string> GetFeatureNames();

    /// <summary>
    /// Reads a versioned JSON file of the store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A file in the current format version is returned as it is. A file in an older format version stops a command
    /// with <see cref="FormatAccess.Write" /> access. With <see cref="FormatAccess.Read" /> access, the
    /// <c>outdatedFormatOnRead</c> setting of <c>config.json</c> decides: the file is migrated in memory and an
    /// <see cref="FormatWarningKind.UpgradeNeeded" /> warning is returned, or the command stops. To read this setting,
    /// an outdated <c>config.json</c> is migrated in memory and the setting applies to it as well. If
    /// <c>config.json</c> does not exist, the file is migrated with a warning. With <see cref="FormatAccess.Upgrade" />
    /// access, the file is always migrated in memory and <c>config.json</c> is not read; the
    /// <see cref="FormatWarningKind.UpgradeNeeded" /> warning names the version the file was migrated from.
    /// </para>
    /// <para>
    /// File paths in exceptions and warnings are relative to the repository root and use <c>/</c> as separator, for
    /// example <c>.shadekit/config.json</c>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="relativePath">The path of the file relative to <see cref="RootPath" />, for example <c>config.json</c>.</param>
    /// <param name="formatId">The id of the file type, for example <see cref="FormatIds.Config" />.</param>
    /// <param name="access">
    /// One of the enumeration values that specifies whether the command only reads the file, reads it to change it,
    /// or migrates it.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the document and its
    /// warnings, or <see langword="null" /> if the file does not exist.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="access" /> is not a defined value.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />; or no file type
    /// with the id <paramref name="formatId" /> is registered.
    /// </exception>
    /// <exception cref="InvalidKitFileException">The file, or <c>config.json</c> when it is read for the setting, is invalid.</exception>
    /// <exception cref="FormatTooNewException">
    /// The file, or <c>config.json</c> when it is read for the setting, uses a format version newer than the engine
    /// supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// The file uses an older format version and either <paramref name="access" /> is <see cref="FormatAccess.Write" />
    /// or <c>outdatedFormatOnRead</c> is <c>stop</c>.
    /// </exception>
    Task<VersionedReadResult<T>?> ReadJsonAsync<T>(string relativePath, string formatId, FormatAccess access,
        CancellationToken cancellationToken = default)
        where T : VersionedDocument;

    /// <summary>
    /// Writes a versioned JSON file of the store.
    /// </summary>
    /// <remarks>
    /// If the file exists, its format is checked first; a file that is too new or invalid stops the write and stays
    /// unchanged. A file in an older format version stops a write with <see cref="FormatAccess.Write" /> access and
    /// is replaced by a write with <see cref="FormatAccess.Upgrade" /> access. The written file carries the current
    /// format version and its <c>minEngineVersion</c>, is UTF-8 encoded without byte order mark, and is written
    /// directly, not atomically. Missing directories are created.
    /// </remarks>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="relativePath">The path of the file relative to <see cref="RootPath" />, for example <c>config.json</c>.</param>
    /// <param name="formatId">The id of the file type, for example <see cref="FormatIds.Config" />.</param>
    /// <param name="document">The document to write.</param>
    /// <param name="access">
    /// <see cref="FormatAccess.Write" /> for a command that changes the file, or <see cref="FormatAccess.Upgrade" />
    /// for a command that migrates it to the current format.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="access" /> is neither <see cref="FormatAccess.Write" /> nor <see cref="FormatAccess.Upgrade" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />; or no file type
    /// with the id <paramref name="formatId" /> is registered.
    /// </exception>
    /// <exception cref="InvalidKitFileException">The existing file is invalid.</exception>
    /// <exception cref="FormatTooNewException">The existing file uses a format version newer than the engine supports.</exception>
    /// <exception cref="UpgradeNeededException">
    /// The existing file uses an older format version and <paramref name="access" /> is
    /// <see cref="FormatAccess.Write" />.
    /// </exception>
    Task WriteJsonAsync<T>(string relativePath, string formatId, T document, FormatAccess access,
        CancellationToken cancellationToken = default)
        where T : VersionedDocument;
}
