namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Represents the result of the initialisation of a repository.
/// </summary>
/// <param name="RepositoryRootPath">The full path of the root directory of the initialised git repository.</param>
/// <param name="Files">
/// The files and directories that were created or changed, in the order in which they were written. A file that
/// already had the required content is not listed.
/// </param>
public sealed record RepositoryInitResult(string RepositoryRootPath, IReadOnlyList<InitializedFile> Files);
