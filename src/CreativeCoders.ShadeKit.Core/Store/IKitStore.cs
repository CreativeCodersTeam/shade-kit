using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;

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
    /// Gets the full path of the root of the git repository that contains the SHADE kit root directory.
    /// </summary>
    /// <value>
    /// The full path of the parent directory of <see cref="RootPath" />, because the SHADE kit root directory
    /// <c>.shadekit</c> lies at the root of the git repository.
    /// </value>
    string RepositoryRootPath { get; }

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
    /// An outdated file stops a <see cref="FormatAccess.Write" /> read. With <see cref="FormatAccess.Read" />, the
    /// <c>outdatedFormatOnRead</c> setting of <c>config.json</c> decides between migrating in memory with an
    /// <see cref="FormatWarningKind.UpgradeNeeded" /> warning and stopping; without <c>config.json</c>, the file is
    /// migrated. <see cref="FormatAccess.Upgrade" /> always migrates and does not read <c>config.json</c>.
    /// <see cref="FormatAccess.Replace" /> is not accepted.
    /// </para>
    /// <para>
    /// File paths in exceptions and warnings are repository-relative with <c>/</c>, for example
    /// <c>.shadekit/config.json</c>.
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="access" /> is not <see cref="FormatAccess.Read" />, <see cref="FormatAccess.Write" />, or
    /// <see cref="FormatAccess.Upgrade" />.
    /// </exception>
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
    /// <para>
    /// An existing file that is too new or invalid stops the write; an outdated one stops a
    /// <see cref="FormatAccess.Write" /> and is replaced by an <see cref="FormatAccess.Upgrade" />.
    /// <see cref="FormatAccess.Replace" /> replaces any existing file, for files that hold nothing to lose, such as the
    /// local override. The format check never reads <c>config.json</c>.
    /// </para>
    /// <para>
    /// The file gets the current format version, UTF-8 without byte order mark, a final line ending, and the line
    /// ending of the repository. It is written directly, not atomically; missing directories are created.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="relativePath">The path of the file relative to <see cref="RootPath" />, for example <c>config.json</c>.</param>
    /// <param name="formatId">The id of the file type, for example <see cref="FormatIds.Config" />.</param>
    /// <param name="document">The document to write.</param>
    /// <param name="access">
    /// <see cref="FormatAccess.Write" /> for a command that changes the file, <see cref="FormatAccess.Upgrade" />
    /// for a command that migrates it to the current format, or <see cref="FormatAccess.Replace" /> for a command
    /// that replaces it whatever it contains.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous write operation. The task result contains the warnings that occurred
    /// while the line ending of the file was determined, for example an <see cref="EditorConfigUnreadableWarning" />;
    /// empty if there were none.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="document" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="access" /> is not <see cref="FormatAccess.Write" />, <see cref="FormatAccess.Upgrade" />, or
    /// <see cref="FormatAccess.Replace" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />; or no file type
    /// with the id <paramref name="formatId" /> is registered.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// The existing file is invalid and <paramref name="access" /> is not <see cref="FormatAccess.Replace" />.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// The existing file uses a format version newer than the engine supports and <paramref name="access" /> is not
    /// <see cref="FormatAccess.Replace" />.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// The existing file uses an older format version and <paramref name="access" /> is
    /// <see cref="FormatAccess.Write" />.
    /// </exception>
    Task<IReadOnlyList<KitWarning>> WriteJsonAsync<T>(string relativePath, string formatId, T document,
        FormatAccess access, CancellationToken cancellationToken = default)
        where T : VersionedDocument;

    /// <summary>
    /// Writes a text file of the store that is not a versioned JSON file, for example a template.
    /// </summary>
    /// <remarks>
    /// Written through an <see cref="ITextFileWriter" /> with the line ending of the repository, directly and not
    /// atomically; missing directories are created. A file in a directory that is moved into place later is written
    /// through <see cref="CreateDirectoryWriter" />.
    /// </remarks>
    /// <param name="relativePath">
    /// The path of the file relative to <see cref="RootPath" />, for example <c>templates/spec.md</c>.
    /// </param>
    /// <param name="content">The content of the file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous write operation. The task result contains the warnings that occurred
    /// while the line ending of the file was determined, for example an <see cref="EditorConfigUnreadableWarning" />;
    /// empty if there were none.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="content" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />.
    /// </exception>
    Task<IReadOnlyList<KitWarning>> WriteFileAsync(string relativePath, TextFileContent content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a writer for the files of a directory that is written at one path and moved to another path later,
    /// such as a hidden staging directory.
    /// </summary>
    /// <remarks>
    /// Files are written below <paramref name="writePath" />, but their line endings are determined for the same path
    /// below <paramref name="targetPath" />. Nothing is created until the first write.
    /// </remarks>
    /// <param name="writePath">
    /// The path of the directory relative to <see cref="RootPath" /> into which the files are written, for example
    /// <c>features/.login.1a2b3c4d.tmp</c>.
    /// </param>
    /// <param name="targetPath">
    /// The path of the directory relative to <see cref="RootPath" /> to which the directory is moved later, for
    /// example <c>features/login</c>.
    /// </param>
    /// <returns>The writer for the files of the directory.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="writePath" /> or <paramref name="targetPath" /> is empty, rooted, or points outside
    /// <see cref="RootPath" />.
    /// </exception>
    IKitDirectoryWriter CreateDirectoryWriter(string writePath, string targetPath);

    /// <summary>
    /// Prepares the line endings of several files of the store that are written next, so determining each of them
    /// later takes fewer <c>git</c> processes.
    /// </summary>
    /// <remarks>
    /// Optional; it does not change the results of later writes. A file that is written through
    /// <see cref="CreateDirectoryWriter" /> is prepared with its path below the target directory, the path for which
    /// its line ending is determined.
    /// </remarks>
    /// <param name="relativePaths">
    /// The paths of the files relative to <see cref="RootPath" />, for example <c>features/login/feature.json</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativePaths" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// A path of <paramref name="relativePaths" /> is <see langword="null" />, empty, rooted, or points outside
    /// <see cref="RootPath" />.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task PrepareLineEndingsAsync(IReadOnlyCollection<string> relativePaths,
        CancellationToken cancellationToken = default);

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
    /// Reads a text file of the store that is not a versioned JSON file, for example a template.
    /// </summary>
    /// <remarks>
    /// Decoded strictly as UTF-8; content that is not UTF-8 is reported as an invalid kit file.
    /// </remarks>
    /// <param name="relativePath">
    /// The path of the file relative to <see cref="RootPath" />, for example <c>templates/spec.md</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the content of the file, or
    /// <see langword="null" /> if the file does not exist.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath" /> is empty, rooted, or points outside <see cref="RootPath" />.
    /// </exception>
    /// <exception cref="PathNotAFileException">A directory exists at <paramref name="relativePath" />.</exception>
    /// <exception cref="InvalidKitFileException">
    /// The file is not UTF-8 text: it starts with a UTF-16 or UTF-32 byte order mark, contains a NUL byte, or contains
    /// bytes that are not valid UTF-8. The file path of the exception is relative to the repository root, for example
    /// <c>.shadekit/templates/spec.md</c>.
    /// </exception>
    Task<TextFileContent?> ReadFileAsync(string relativePath, CancellationToken cancellationToken = default);

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
    /// Hidden files are included; symbolic links to directories are neither returned nor followed. A file at
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
    /// Nothing happens if the directory does not exist; a file at <paramref name="relativePath" /> is not deleted.
    /// Symbolic links to directories are removed as links; their targets are kept.
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
    /// Never replaces anything; the parent of the destination is created if missing. Within one file system the move
    /// is a single rename, so it suits exclusive creation through a temporary name and hiding a directory before it is
    /// deleted.
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
