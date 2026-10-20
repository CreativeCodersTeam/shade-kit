using System.Text;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Provides access to the content of a SHADE kit store in the file system.
/// </summary>
public class KitStore : IKitStore
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly IVersionedJsonSerializer _serializer;

    private readonly IFormatRegistry _formatRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="KitStore" /> class.
    /// </summary>
    /// <param name="kitStorePath">The path of the <c>.shadekit</c> directory.</param>
    /// <param name="serializer">The serializer that reads and writes the versioned JSON files.</param>
    /// <param name="formatRegistry">The registry that defines the file types the store accepts.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="kitStorePath" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serializer" /> or <paramref name="formatRegistry" /> is <see langword="null" />.
    /// </exception>
    public KitStore(string kitStorePath, IVersionedJsonSerializer serializer, IFormatRegistry formatRegistry)
    {
        Ensure.IsNotNullOrWhitespace(kitStorePath);

        RootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(kitStorePath));
        _serializer = Ensure.NotNull(serializer);
        _formatRegistry = Ensure.NotNull(formatRegistry);
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

        if (!Enum.IsDefined(access))
        {
            throw new ArgumentOutOfRangeException(nameof(access), access, "The access is not defined.");
        }

        var result = await ReadFileAsync<T>(relativePath, formatId, access != FormatAccess.Write, cancellationToken)
            .ConfigureAwait(false);

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

        var (policy, configWarnings) = await ReadOutdatedFormatPolicyAsync(cancellationToken).ConfigureAwait(false);

        if (policy == OutdatedFormatOnReadPolicy.Stop)
        {
            throw CreateUpgradeNeededException(result.Warnings[0]);
        }

        return result with { Warnings = [.. result.Warnings, .. configWarnings] };
    }

    /// <inheritdoc />
    public async Task WriteJsonAsync<T>(string relativePath, string formatId, T document, FormatAccess access,
        CancellationToken cancellationToken = default)
        where T : VersionedDocument
    {
        Ensure.NotNull(document);

        if (access is not (FormatAccess.Write or FormatAccess.Upgrade))
        {
            throw new ArgumentOutOfRangeException(nameof(access), access,
                $"A file can only be written with {nameof(FormatAccess.Write)} or {nameof(FormatAccess.Upgrade)} " +
                "access.");
        }

        await ReadFileAsync<T>(relativePath, formatId, access == FormatAccess.Upgrade, cancellationToken)
            .ConfigureAwait(false);

        var json = _serializer.Serialize(document, formatId);

        await WriteFileAsync(relativePath, Utf8WithoutBom.GetBytes(json), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(string relativePath, byte[] content,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(content);

        var fullPath = GetFullPath(relativePath);

        // GetFullPath only returns paths below RootPath, so the file always has a parent directory.
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void CreateDirectory(string relativePath)
    {
        Directory.CreateDirectory(GetFullPath(relativePath));
    }

    private async Task<VersionedReadResult<T>?> ReadFileAsync<T>(string relativePath, string formatId,
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

    private async Task<(OutdatedFormatOnReadPolicy Policy, IReadOnlyList<FormatWarning> Warnings)>
        ReadOutdatedFormatPolicyAsync(CancellationToken cancellationToken)
    {
        var config = await ReadFileAsync<KitConfig>(KitStorePaths.ConfigFileName, FormatIds.Config, true,
            cancellationToken).ConfigureAwait(false);

        if (config is null)
        {
            return (OutdatedFormatOnReadPolicy.MigrateAndWarn, []);
        }

        var policy = config.Document.OutdatedFormatOnRead;

        if (policy == OutdatedFormatOnReadPolicy.Stop && config.Warnings.Count > 0)
        {
            throw CreateUpgradeNeededException(config.Warnings[0]);
        }

        return (policy, config.Warnings);
    }

    private static UpgradeNeededException CreateUpgradeNeededException(FormatWarning warning)
    {
        return new UpgradeNeededException(warning.FilePath, warning.FileVersion, warning.CurrentVersion);
    }

    private string GetFullPath(string relativePath)
    {
        Ensure.IsNotNullOrWhitespace(relativePath);

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException($"The path '{relativePath}' must be relative to the SHADE kit root.",
                nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(RootPath, relativePath));

        return fullPath.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? fullPath
            : throw new ArgumentException($"The path '{relativePath}' points outside the SHADE kit root.",
                nameof(relativePath));
    }

    private string GetDisplayPath(string relativePath)
    {
        return KitStorePaths.GetRepositoryPath(Path.GetRelativePath(RootPath, GetFullPath(relativePath))
            .Replace(Path.DirectorySeparatorChar, '/'));
    }
}
