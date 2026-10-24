using System.Diagnostics.CodeAnalysis;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Provides access to the content of a SHADE kit store in the file system.
/// </summary>
/// <remarks>
/// <para>
/// The <c>outdatedFormatOnRead</c> setting of <c>config.json</c> and the warnings of an outdated <c>config.json</c>
/// are read once and then reused by later reads of the same instance, so reading many outdated files does not read
/// <c>config.json</c> again for each of them. A failed read of the setting is not reused, and every change that the
/// instance makes to the store discards the reused setting, so it is read again on the next read that needs it.
/// </para>
/// <para>
/// Text files are written with an <see cref="ITextFileWriter" /> for the repository root, the parent directory of
/// <see cref="RootPath" />, so the line ending of a file is determined for its path relative to the repository root,
/// for example <c>.shadekit/templates/spec.md</c>. Each write returns its own warnings; the instance keeps none.
/// </para>
/// </remarks>
public class KitStore : IKitStore
{
    // Every entry below the directory, hidden and system files included; inaccessible entries are not skipped silently.
    private static readonly EnumerationOptions AllFilesEnumerationOptions = new EnumerationOptions
    {
        RecurseSubdirectories = true,
        AttributesToSkip = 0,
        IgnoreInaccessible = false
    };

    private readonly IVersionedJsonSerializer _serializer;

    private readonly IFormatRegistry _formatRegistry;

    private readonly ITextFileWriter _textFileWriter;

    private readonly string _repositoryRootPath;

    private OutdatedFormatPolicy? _outdatedFormatPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="KitStore" /> class.
    /// </summary>
    /// <param name="kitStorePath">The path of the <c>.shadekit</c> directory.</param>
    /// <param name="serializer">The serializer that reads and writes the versioned JSON files.</param>
    /// <param name="formatRegistry">The registry that defines the file types the store accepts.</param>
    /// <param name="textFileWriter">
    /// The writer that writes the text files and the versioned JSON files of the store with the line ending of the
    /// repository.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="kitStorePath" /> is <see langword="null" />, empty, or whitespace, or it is a root directory,
    /// which has no parent directory that could be the repository root.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serializer" />, <paramref name="formatRegistry" />, or <paramref name="textFileWriter" /> is
    /// <see langword="null" />.
    /// </exception>
    public KitStore(string kitStorePath, IVersionedJsonSerializer serializer, IFormatRegistry formatRegistry,
        ITextFileWriter textFileWriter)
    {
        Ensure.IsNotNullOrWhitespace(kitStorePath);

        RootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(kitStorePath));
        _repositoryRootPath = Path.GetDirectoryName(RootPath)
                              ?? throw new ArgumentException(
                                  $"The SHADE kit root directory '{RootPath}' has no parent directory.",
                                  nameof(kitStorePath));
        _serializer = Ensure.NotNull(serializer);
        _formatRegistry = Ensure.NotNull(formatRegistry);
        _textFileWriter = Ensure.NotNull(textFileWriter);
    }

    /// <inheritdoc />
    public string RootPath { get; }

    /// <inheritdoc />
    public IEnumerable<string> GetFeatureNames()
    {
        var featuresPath = Path.Combine(RootPath, KitStorePaths.FeaturesDirName);

        if (!Directory.Exists(featuresPath))
        {
            return [];
        }

        return
        [
            .. new DirectoryInfo(featuresPath).EnumerateDirectories()
                .Select(directory => directory.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
        ];
    }

    /// <inheritdoc />
    public async Task<VersionedReadResult<T>?> ReadJsonAsync<T>(string relativePath, string formatId,
        FormatAccess access, CancellationToken cancellationToken = default)
        where T : VersionedDocument
    {
        _formatRegistry.GetFormat(formatId);

        if (access is not (FormatAccess.Read or FormatAccess.Write or FormatAccess.Upgrade))
        {
            throw new ArgumentOutOfRangeException(nameof(access), access,
                $"A file can only be read with {nameof(FormatAccess.Read)}, {nameof(FormatAccess.Write)}, or " +
                $"{nameof(FormatAccess.Upgrade)} access.");
        }

        var result = await ReadVersionedFileAsync<T>(relativePath, formatId, access != FormatAccess.Write,
            cancellationToken).ConfigureAwait(false);

        if (result is null || result.Warnings.Count == 0 || access == FormatAccess.Upgrade)
        {
            return result;
        }

        // The config file holds the policy for itself, so it is neither read again nor are its warnings added twice.
        if (result.Document is KitConfig config)
        {
            return config.OutdatedFormatOnRead == OutdatedFormatOnReadPolicy.Stop
                ? throw CreateUpgradeNeededException(result.Warnings[0])
                : result;
        }

        var (policy, configWarnings) = await GetOutdatedFormatPolicyAsync(cancellationToken).ConfigureAwait(false);

        if (policy == OutdatedFormatOnReadPolicy.Stop)
        {
            throw CreateUpgradeNeededException(result.Warnings[0]);
        }

        return result with { Warnings = [.. result.Warnings, .. configWarnings] };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KitWarning>> WriteJsonAsync<T>(string relativePath, string formatId, T document,
        FormatAccess access, string? lineEndingPath = null, CancellationToken cancellationToken = default)
        where T : VersionedDocument
    {
        Ensure.NotNull(document);

        if (access is not (FormatAccess.Write or FormatAccess.Upgrade or FormatAccess.Replace))
        {
            throw new ArgumentOutOfRangeException(nameof(access), access,
                $"A file can only be written with {nameof(FormatAccess.Write)}, {nameof(FormatAccess.Upgrade)}, or " +
                $"{nameof(FormatAccess.Replace)} access.");
        }

        // Checked before the format check, which may delete an unusable file with Replace access; relativePath first,
        // as in WriteFileAsync and the text file writer.
        GetFullPath(relativePath);

        if (lineEndingPath is not null)
        {
            GetFullPath(lineEndingPath);
        }

        if (access == FormatAccess.Replace)
        {
            await DeleteUnusableFileAsync<T>(relativePath, formatId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ReadVersionedFileAsync<T>(relativePath, formatId, access == FormatAccess.Upgrade, cancellationToken)
                .ConfigureAwait(false);
        }

        var json = _serializer.Serialize(document, formatId);

        // The serializer ends the JSON with a line ending, so the file does as well.
        return await WriteFileAsync(relativePath, TextFileContent.FromText(json), lineEndingPath, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KitWarning>> WriteFileAsync(string relativePath, TextFileContent content,
        string? lineEndingPath = null, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(content);

        var repositoryPath = GetRepositoryRelativePath(relativePath);
        var lineEndingRepositoryPath = lineEndingPath is null ? null : GetRepositoryRelativePath(lineEndingPath);

        DiscardOutdatedFormatPolicy();

        // The writer creates the missing directories.
        return await _textFileWriter
            .WriteAsync(_repositoryRootPath, repositoryPath, content, lineEndingRepositoryPath, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void CreateDirectory(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);

        DiscardOutdatedFormatPolicy();

        Directory.CreateDirectory(fullPath);
    }

    /// <inheritdoc />
    public async Task<TextFileContent?> ReadFileAsync(string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(relativePath);

        if (Directory.Exists(fullPath))
        {
            return null;
        }

        // Every file of the store is a kit file, so content that is not UTF-8 text makes it an invalid kit file
        // rather than a file with an unsupported encoding.
        var displayPath = GetDisplayPath(relativePath);

        return await TextFileContent
            .ReadAsync(fullPath,
                (reason, innerException) => new InvalidKitFileException(displayPath, reason, innerException),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool FileExists(string relativePath)
    {
        return File.Exists(GetFullPath(relativePath));
    }

    /// <inheritdoc />
    public bool DirectoryExists(string relativePath)
    {
        return Directory.Exists(GetFullPath(relativePath));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetFiles(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);

        if (!Directory.Exists(fullPath))
        {
            return [];
        }

        // Directory links are neither listed nor followed, so the result never leaves the store and a link loop
        // cannot recurse endlessly. Directory.EnumerateFiles would follow them.
        var files = new FileSystemEnumerable<string>(fullPath,
            (ref entry) => Path.GetRelativePath(RootPath, entry.ToFullPath()), AllFilesEnumerationOptions)
        {
            ShouldIncludePredicate = (ref entry) => !entry.IsDirectory,
            ShouldRecursePredicate = (ref entry) => (entry.Attributes & FileAttributes.ReparsePoint) == 0
        };

        return
        [
            .. files
                .Select(file => file.Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal)
        ];
    }

    /// <inheritdoc />
    public void DeleteFile(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);

        DiscardOutdatedFormatPolicy();

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    /// <inheritdoc />
    public void DeleteDirectory(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);

        DiscardOutdatedFormatPolicy();

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
        }
    }

    /// <inheritdoc />
    public void MoveDirectory(string sourceRelativePath, string destinationRelativePath)
    {
        var sourceFullPath = GetFullPath(sourceRelativePath);
        var destinationFullPath = GetFullPath(destinationRelativePath);

        // Directory.Move also moves files, so a file at the source counts as a missing directory.
        if (!Directory.Exists(sourceFullPath))
        {
            throw new DirectoryNotFoundException(
                $"The directory '{GetDisplayPath(sourceRelativePath)}' does not exist.");
        }

        // Checked explicitly, because some platforms would replace an existing empty directory. The rename itself
        // refuses a destination that another process creates with content after this check.
        if (File.Exists(destinationFullPath) || Directory.Exists(destinationFullPath))
        {
            throw new IOException($"'{GetDisplayPath(destinationRelativePath)}' already exists.");
        }

        DiscardOutdatedFormatPolicy();

        // GetFullPath only returns paths below RootPath, so the destination always has a parent directory.
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFullPath)!);

        Directory.Move(sourceFullPath, destinationFullPath);
    }

    private async Task<VersionedReadResult<T>?> ReadVersionedFileAsync<T>(string relativePath, string formatId,
        bool migrateOutdated, CancellationToken cancellationToken)
        where T : VersionedDocument
    {
        var fullPath = GetFullPath(relativePath);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);

        return _serializer.Deserialize<T>(json, formatId, GetDisplayPath(relativePath), migrateOutdated);
    }

    // The file is read as for a write, so a file that cannot be read still stops the write. A file that cannot be used
    // because of its own format is deleted, so it is replaced by a new file even where it could not be overwritten in
    // place, for example a read-only file in a writable folder. The format check of a write never reads config.json,
    // so every format exception here belongs to the file itself.
    private async Task DeleteUnusableFileAsync<T>(string relativePath, string formatId,
        CancellationToken cancellationToken)
        where T : VersionedDocument
    {
        try
        {
            await ReadVersionedFileAsync<T>(relativePath, formatId, false, cancellationToken).ConfigureAwait(false);
        }
        catch (KitFileFormatException)
        {
            DeleteFile(relativePath);
        }
    }

    // Only a successful read is kept, so a canceled or failed read is repeated by the next read that needs it.
    private async Task<OutdatedFormatPolicy> GetOutdatedFormatPolicyAsync(CancellationToken cancellationToken)
    {
        return _outdatedFormatPolicy ??=
            await ReadOutdatedFormatPolicyAsync(cancellationToken).ConfigureAwait(false);
    }

    private void DiscardOutdatedFormatPolicy()
    {
        _outdatedFormatPolicy = null;
    }

    private async Task<OutdatedFormatPolicy> ReadOutdatedFormatPolicyAsync(CancellationToken cancellationToken)
    {
        var config = await ReadVersionedFileAsync<KitConfig>(KitStorePaths.ConfigFileName, FormatIds.Config, true,
            cancellationToken).ConfigureAwait(false);

        if (config is null)
        {
            return new OutdatedFormatPolicy(OutdatedFormatOnReadPolicy.MigrateAndWarn, []);
        }

        var policy = config.Document.OutdatedFormatOnRead;

        if (policy == OutdatedFormatOnReadPolicy.Stop && config.Warnings.Count > 0)
        {
            throw CreateUpgradeNeededException(config.Warnings[0]);
        }

        return new OutdatedFormatPolicy(policy, config.Warnings);
    }

    private static UpgradeNeededException CreateUpgradeNeededException(FormatWarning warning)
    {
        return new UpgradeNeededException(warning.FilePath, warning.FileVersion, warning.CurrentVersion);
    }

    // An invalid path throws an ArgumentException whose ParamName is the caller's parameter that holds the path
    // (relativePath, lineEndingPath, sourceRelativePath, ...), filled in by the compiler.
    [SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
        Justification = "The caller's parameter name is forwarded on purpose, so an invalid path names the caller's " +
                        "parameter instead of this helper's.")]
    private string GetFullPath(string relativePath,
        [CallerArgumentExpression(nameof(relativePath))] string paramName = "")
    {
        Ensure.IsNotNullOrWhitespace(relativePath, paramName);

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException($"The path '{relativePath}' must be relative to the SHADE kit root.",
                paramName);
        }

        var fullPath = Path.GetFullPath(Path.Combine(RootPath, relativePath));

        return fullPath.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? fullPath
            : throw new ArgumentException($"The path '{relativePath}' points outside the SHADE kit root.",
                paramName);
    }

    // The path relative to the repository root keeps the name of the kit root, so git attributes and .editorconfig
    // sections for .shadekit/ apply.
    [SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
        Justification = "The caller's parameter name is forwarded on purpose, so an invalid path names the caller's " +
                        "parameter instead of this helper's.")]
    private string GetRepositoryRelativePath(string relativePath,
        [CallerArgumentExpression(nameof(relativePath))] string paramName = "")
    {
        return Path.GetRelativePath(_repositoryRootPath, GetFullPath(relativePath, paramName));
    }

    private string GetDisplayPath(string relativePath)
    {
        // Resolving against the root removes "." and ".." segments; KitStorePaths builds the final string.
        return KitStorePaths.GetDisplayPath(Path.GetRelativePath(RootPath, GetFullPath(relativePath)));
    }

    // The outdatedFormatOnRead setting of config.json and the warnings of an outdated config.json.
    private sealed record OutdatedFormatPolicy(
        OutdatedFormatOnReadPolicy Policy,
        IReadOnlyList<FormatWarning> Warnings);
}
