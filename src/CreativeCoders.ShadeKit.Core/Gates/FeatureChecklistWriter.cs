using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;

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
            // The same full path that the store puts into the exception when it reads a directory.
            throw new PathNotAFileException(Path.GetFullPath(Path.Combine(store.RootPath, copyPath)));
        }

        if (store.FileExists(copyPath))
        {
            return new FeatureChecklistCopyResult(Created: false, Warnings: []);
        }

        var templatePath = KitStorePaths.GetTemplatePath(checklist);
        var template = await store.ReadFileAsync(templatePath, cancellationToken).ConfigureAwait(false)
                       ?? throw new ChecklistTemplateNotFoundException(KitStorePaths.GetRepositoryPath(templatePath));

        var copy = template.ReplaceInLines(FeatureIdPlaceholder, featureId);

        var warnings = await store.WriteFileAsync(copyPath, copy, cancellationToken).ConfigureAwait(false);

        return new FeatureChecklistCopyResult(Created: true, warnings);
    }
}
