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

    /// <summary>
    /// Writes a file of the store that is not a versioned JSON file, for example a template.
    /// </summary>
    /// <remarks>
    /// The bytes are written as they are, directly, not atomically; an existing file is replaced. Missing directories
    /// are created.
    /// </remarks>
    /// <param name="relativePath">
    /// The path of the file relative to <see cref="RootPath" />, for example <c>templates/spec.md</c>.
    /// </param>
    /// <param name="content">The bytes of the file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />.
    /// </exception>
    Task WriteFileAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a directory of the store, together with its missing parent directories.
    /// </summary>
    /// <param name="relativePath">
    /// The path of the directory relative to <see cref="RootPath" />, for example <c>features</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />.
    /// </exception>
    void CreateDirectory(string relativePath);

    /// <summary>
    /// Reads a file of the store that is not a versioned JSON file, for example a template.
    /// </summary>
    /// <remarks>
    /// The bytes are returned as they are, without decoding or format check. A directory at
    /// <paramref name="relativePath" /> counts as a missing file.
    /// </remarks>
    /// <param name="relativePath">
    /// The path of the file relative to <see cref="RootPath" />, for example <c>templates/request.md</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the bytes of the file, or
    /// <see langword="null" /> if the file does not exist.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />.
    /// </exception>
    Task<byte[]?> ReadFileAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a file of the store exists.
    /// </summary>
    /// <param name="relativePath">
    /// The path of the file relative to <see cref="RootPath" />, for example <c>features/login/feature.json</c>.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if a file exists at <paramref name="relativePath" />; otherwise,
    /// <see langword="false" />, also if a directory exists there.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />.
    /// </exception>
    bool FileExists(string relativePath);

    /// <summary>
    /// Determines whether a directory of the store exists.
    /// </summary>
    /// <param name="relativePath">
    /// The path of the directory relative to <see cref="RootPath" />, for example <c>features/login</c>.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if a directory exists at <paramref name="relativePath" />; otherwise,
    /// <see langword="false" />, also if a file exists there.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />.
    /// </exception>
    bool DirectoryExists(string relativePath);

    /// <summary>
    /// Gets the paths of all files below a directory of the store, including the files of its subdirectories.
    /// </summary>
    /// <remarks>
    /// Hidden files are included. A symbolic link to a file is returned like a file; a symbolic link to a directory is
    /// neither returned nor followed, so the result never contains files outside the directory. A file at
    /// <paramref name="relativePath" /> counts as a missing directory.
    /// </remarks>
    /// <param name="relativePath">
    /// The path of the directory relative to <see cref="RootPath" />, for example <c>features/login</c>.
    /// </param>
    /// <returns>
    /// The paths of the files relative to <see cref="RootPath" />, with <c>/</c> as separator and in ordinal order,
    /// for example <c>features/login/feature.json</c>; or an empty list if the directory does not exist.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />.
    /// </exception>
    IReadOnlyList<string> GetFiles(string relativePath);

    /// <summary>
    /// Deletes a file of the store.
    /// </summary>
    /// <remarks>
    /// Nothing happens if the file does not exist. A directory at <paramref name="relativePath" /> counts as a
    /// missing file and is not deleted.
    /// </remarks>
    /// <param name="relativePath">
    /// The path of the file relative to <see cref="RootPath" />, for example <c>features/current-feature.json</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />.
    /// </exception>
    void DeleteFile(string relativePath);

    /// <summary>
    /// Deletes a directory of the store together with all its files and subdirectories.
    /// </summary>
    /// <remarks>
    /// Nothing happens if the directory does not exist. A file at <paramref name="relativePath" /> counts as a
    /// missing directory and is not deleted. A symbolic link to a directory, at <paramref name="relativePath" /> or
    /// below it, is removed as a link; the content of its target is kept. The SHADE kit root directory itself cannot
    /// be deleted, because <paramref name="relativePath" /> must point below <see cref="RootPath" />.
    /// </remarks>
    /// <param name="relativePath">
    /// The path of the directory relative to <see cref="RootPath" />, for example <c>features/login</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />.
    /// </exception>
    void DeleteDirectory(string relativePath);

    /// <summary>
    /// Moves or renames a directory of the store together with all its files and subdirectories.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The move never replaces anything: if a file or directory exists at <paramref name="destinationRelativePath" />,
    /// nothing is moved and an <see cref="IOException" /> is thrown. The parent directory of the destination is
    /// created if it is missing.
    /// </para>
    /// <para>
    /// Within one file system the move is a single rename: the directory appears at the destination with all its
    /// content at once, and a failed move leaves the source unchanged. This makes the move suitable for creating a
    /// directory exclusively (build it under a temporary name, then move it) and for removing a directory from view
    /// before it is deleted. If another process creates the destination after the existence check, the operating
    /// system refuses the rename because the destination is not empty; only an empty directory created in that moment
    /// can be replaced on some systems.
    /// </para>
    /// </remarks>
    /// <param name="sourceRelativePath">
    /// The path of the directory to move relative to <see cref="RootPath" />, for example
    /// <c>features/.login.1a2b3c4d.tmp</c>.
    /// </param>
    /// <param name="destinationRelativePath">
    /// The new path of the directory relative to <see cref="RootPath" />, for example <c>features/login</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="sourceRelativePath" /> or <paramref name="destinationRelativePath" /> is empty, rooted, or
    /// points outside <see cref="RootPath" />.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// No directory exists at <paramref name="sourceRelativePath" />.
    /// </exception>
    /// <exception cref="IOException">
    /// A file or directory exists at <paramref name="destinationRelativePath" />, the destination lies inside the
    /// source, or the directory cannot be moved, for example because a file in it is locked.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">The process is not allowed to move the directory.</exception>
    void MoveDirectory(string sourceRelativePath, string destinationRelativePath);
}
