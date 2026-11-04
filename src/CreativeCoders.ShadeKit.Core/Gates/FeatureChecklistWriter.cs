using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Copies the checklist template of a gate into the folder of a feature through the store, if the copy does not exist
/// yet.
/// </summary>
public sealed class FeatureChecklistWriter : IFeatureChecklistWriter
{
    /// <summary>
    /// Represents the placeholder in a checklist template that is replaced by the feature id.
    /// </summary>
    internal const string FeatureIdPlaceholder = "{{feature-id}}";

    /// <inheritdoc />
    public async Task<FeatureChecklistCopyResult> EnsureCopyAsync(IKitStore store, string featureId,
        string checklist, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.IsNotNullOrWhitespace(featureId);
        Ensure.IsNotNullOrWhitespace(checklist);

        // Callers pass the id of a resolved feature; the check keeps the copy inside its feature folder, because an
        // id such as ".." or "a/b" would name another folder of the store.
        if (!FeatureIds.IsValid(featureId))
        {
            throw new ArgumentException($"'{featureId}' is not a valid feature id.", nameof(featureId));
        }

        // The gate configuration checks the path when it is read; the check here keeps the copy inside the feature
        // folder and the template inside the templates directory for every other caller.
        if (!GatesConfig.IsValidChecklistPath(checklist))
        {
            throw new ArgumentException($"The checklist '{checklist}' is not a valid checklist path.",
                nameof(checklist));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var copyPath = KitStorePaths.GetFeatureChecklistPath(featureId, checklist);

        if (store.DirectoryExists(copyPath))
        {
            throw new PathNotAFileException(store.GetFullPath(copyPath));
        }

        if (store.FileExists(copyPath))
        {
            return new FeatureChecklistCopyResult(Created: false, Warnings: []);
        }

        var templatePath = KitStorePaths.GetTemplatePath(checklist);
        var template = await store.ReadFileAsync(templatePath, cancellationToken).ConfigureAwait(false)
                       ?? throw new ChecklistTemplateNotFoundException(KitStorePaths.GetRepositoryPath(templatePath));

        var copy = template.ReplaceInLines(FeatureIdPlaceholder, featureId);

        return await WriteCopyAsync(store, copyPath, copy, cancellationToken).ConfigureAwait(false);
    }

    // The copy is created only if it does not exist, so of two concurrent starts only one creates it and the other
    // finds it. Every other I/O failure, for example a file in the place of a directory of the copy path, is reported
    // as a store write failure; other exceptions, cancellation included, pass unchanged.
    private static async Task<FeatureChecklistCopyResult> WriteCopyAsync(IKitStore store, string copyPath,
        TextFileContent copy, CancellationToken cancellationToken)
    {
        try
        {
            var warnings = await store.WriteFileAsync(copyPath, copy, FileWriteMode.CreateNew, cancellationToken)
                .ConfigureAwait(false);

            return new FeatureChecklistCopyResult(Created: true, warnings);
        }
        catch (FileAlreadyExistsException)
        {
            return new FeatureChecklistCopyResult(Created: false, Warnings: []);
        }
        catch (IOException exception)
        {
            throw new KitFileWriteFailedException(KitStorePaths.GetRepositoryPath(copyPath), exception);
        }
    }
}
