namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Represents a file or directory that the initialisation of a repository created or changed.
/// </summary>
/// <param name="Path">
/// The path relative to the repository root, with <c>/</c> as separator, for example <c>.shadekit/config.json</c>. A
/// directory ends with <c>/</c>, for example <c>.shadekit/features/</c>.
/// </param>
/// <param name="Action">One of the enumeration values that specifies how the file was changed.</param>
public sealed record InitializedFile(string Path, InitializedFileAction Action);
