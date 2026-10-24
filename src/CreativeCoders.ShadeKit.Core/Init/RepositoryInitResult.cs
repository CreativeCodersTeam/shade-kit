using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Represents the result of the initialisation of a repository.
/// </summary>
/// <param name="RepositoryRootPath">The full path of the root directory of the initialised git repository.</param>
/// <param name="Files">
/// The files and directories that were created or changed, in the order in which they were written. A file that
/// already had the required content is not listed.
/// </param>
/// <param name="Warnings">
/// The warnings that occurred while the files were written, for example an
/// <see cref="EditorConfigUnreadableWarning" />, in the order in which they first occurred; a warning that occurred
/// more than once is listed once. Empty if there were none.
/// </param>
public sealed record RepositoryInitResult(
    string RepositoryRootPath,
    IReadOnlyList<InitializedFile> Files,
    IReadOnlyList<KitWarning> Warnings);
