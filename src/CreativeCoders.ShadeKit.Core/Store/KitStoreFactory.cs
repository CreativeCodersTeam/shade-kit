using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Creates <see cref="KitStore" /> instances for the SHADE kit root at the root of a git repository.
/// </summary>
/// <param name="gitRepositoryLocator">The locator that finds the root of the git repository that contains a directory.</param>
/// <param name="serializer">The serializer that the created stores use for versioned JSON files.</param>
/// <param name="formatRegistry">The registry that defines the file types the created stores accept.</param>
/// <param name="textFileWriter">
/// The writer with which the created stores write their files with the line ending of the repository.
/// </param>
public class KitStoreFactory(
    IGitRepositoryLocator gitRepositoryLocator,
    IVersionedJsonSerializer serializer,
    IFormatRegistry formatRegistry,
    ITextFileWriter textFileWriter)
    : IKitStoreFactory
{
    private readonly IGitRepositoryLocator _gitRepositoryLocator = Ensure.NotNull(gitRepositoryLocator);

    private readonly IVersionedJsonSerializer _serializer = Ensure.NotNull(serializer);

    private readonly IFormatRegistry _formatRegistry = Ensure.NotNull(formatRegistry);

    private readonly ITextFileWriter _textFileWriter = Ensure.NotNull(textFileWriter);

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
        var kitStore = CreateForRepositoryRoot(gitRootPath);

        if (File.Exists(kitStore.RootPath))
        {
            throw new KitRootNotADirectoryException(kitStore.RootPath);
        }

        return Directory.Exists(kitStore.RootPath)
            ? kitStore
            : throw new KitRootPathNotFoundException(path, gitRootPath);
    }

    /// <inheritdoc />
    public IKitStore CreateForRepositoryRoot(string repositoryRootPath)
    {
        Ensure.IsNotNullOrWhitespace(repositoryRootPath);

        return new KitStore(Path.Combine(repositoryRootPath, KitStorePaths.RootPath), _serializer, _formatRegistry,
            _textFileWriter);
    }
}
