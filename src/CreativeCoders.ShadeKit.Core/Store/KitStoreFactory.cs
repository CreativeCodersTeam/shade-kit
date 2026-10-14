using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Creates <see cref="KitStore" /> instances for the SHADE kit root at the root of a git repository.
/// </summary>
/// <param name="serializer">The serializer that the created stores use for versioned JSON files.</param>
/// <param name="formatRegistry">The registry that defines the file types the created stores accept.</param>
public class KitStoreFactory(IVersionedJsonSerializer serializer, IFormatRegistry formatRegistry) : IKitStoreFactory
{
    private const string GitEntryName = ".git";

    private readonly IVersionedJsonSerializer _serializer = Ensure.NotNull(serializer);

    private readonly IFormatRegistry _formatRegistry = Ensure.NotNull(formatRegistry);

    /// <inheritdoc />
    public IKitStore Create()
    {
        return Create(Directory.GetCurrentDirectory());
    }

    /// <inheritdoc />
    public IKitStore Create(string path)
    {
        Ensure.IsNotNullOrWhitespace(path);

        var gitRootPath = FindGitRootPath(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
                          ?? throw new NotAGitRepositoryException(path);

        var kitStorePath = Path.Combine(gitRootPath, KitStorePaths.RootPath);

        return Directory.Exists(kitStorePath)
            ? new KitStore(kitStorePath, _serializer, _formatRegistry)
            : throw new KitRootPathNotFoundException(path, gitRootPath);
    }

    private static string? FindGitRootPath(string startPath)
    {
        for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
        {
            var gitEntryPath = Path.Combine(directory.FullName, GitEntryName);

            if (Path.Exists(gitEntryPath))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
