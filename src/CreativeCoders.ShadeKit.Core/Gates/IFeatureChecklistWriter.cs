using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Copies the checklist template of a gate into the folder of a feature, where the filled copy is the record of the
/// gate.
/// </summary>
public interface IFeatureChecklistWriter
{
    /// <summary>
    /// Makes sure that the folder of a feature contains the copy of a checklist, and creates the copy from its template
    /// if it does not exist yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The copy lies at <see cref="KitStorePaths.GetFeatureChecklistPath" />, the template at
    /// <see cref="KitStorePaths.GetTemplatePath" /> of <paramref name="checklist" />. An existing copy is neither read
    /// nor changed, so a reopened phase keeps the filled checklist. The copy is created with
    /// <see cref="FileWriteMode.CreateNew" />, so a copy that another call creates at the same time is kept as well.
    /// </para>
    /// <para>
    /// A new copy has the lines of the template with every <c>{{feature-id}}</c> replaced by
    /// <paramref name="featureId" />; everything else, including the byte order mark and the final line ending, is
    /// kept. Missing directories are created.
    /// </para>
    /// </remarks>
    /// <param name="store">The store of the feature.</param>
    /// <param name="featureId">The id of the feature, which is the name of its feature folder.</param>
    /// <param name="checklist">
    /// The path of the checklist relative to the feature folder and to the templates directory, as a gate names it,
    /// for example <c>checks/definition-of-ready.md</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result tells whether the copy was created and
    /// contains the warnings of writing it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="featureId" /> is <see langword="null" />, empty, whitespace, or not a valid feature id; or
    /// <paramref name="checklist" /> is <see langword="null" />, empty, whitespace, or not a valid checklist path.
    /// </exception>
    /// <exception cref="PathNotAFileException">
    /// A directory exists at the path of the copy or of the template; nothing is written.
    /// </exception>
    /// <exception cref="ChecklistTemplateNotFoundException">
    /// The copy does not exist and neither does the template; nothing is written.
    /// </exception>
    /// <exception cref="InvalidKitFileException">The template is not UTF-8 text; nothing is written.</exception>
    /// <exception cref="KitFileWriteFailedException">
    /// The copy cannot be written because of an <see cref="IOException" />, for example because a file exists in the
    /// place of one of its directories.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<FeatureChecklistCopyResult> EnsureCopyAsync(IKitStore store, string featureId, string checklist,
        CancellationToken cancellationToken = default);
}
