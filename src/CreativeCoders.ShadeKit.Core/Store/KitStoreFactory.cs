using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Creates <see cref="KitStore" /> instances for the SHADE kit root at the root of a git repository.
/// </summary>
/// <param name="gitRepositoryLocator">The locator that finds the root of the git repository that contains a directory.</param>
/// <param name="serializer">The serializer that the created stores use for versioned JSON files.</param>
/// <param name="formatRegistry">The registry that defines the file types the created stores accept.</param>
public class KitStoreFactory(
    IGitRepositoryLocator gitRepositoryLocator,
    IVersionedJsonSerializer serializer,
    IFormatRegistry formatRegistry)
    : IKitStoreFactory
{
    private readonly IGitRepositoryLocator _gitRepositoryLocator = Ensure.NotNull(gitRepositoryLocator);

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

        var gitRootPath = _gitRepositoryLocator.GetRepositoryRootPath(path);
        var kitStorePath = Path.Combine(gitRootPath, KitStorePaths.RootPath);

        return Directory.Exists(kitStorePath)
            ? CreateForKitRoot(kitStorePath)
            : throw new KitRootPathNotFoundException(path, gitRootPath);
    }

    /// <inheritdoc />
    public IKitStore CreateForKitRoot(string kitRootPath)
    {
        Ensure.IsNotNullOrWhitespace(kitRootPath);

        return new KitStore(kitRootPath, _serializer, _formatRegistry);
    }
}
