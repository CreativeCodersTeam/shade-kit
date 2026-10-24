namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Represents a warning that an <c>.editorconfig</c> file could not be read, so the <c>.editorconfig</c> files did not
/// decide the line ending of a file and the line ending of the operating system was used.
/// </summary>
/// <param name="FilePath">
/// The absolute path of the <c>.editorconfig</c> file that could not be read, or the absolute path of the file whose
/// line ending was resolved if the failing <c>.editorconfig</c> file is not known;
/// <paramref name="IsEditorConfigFilePath" /> tells which.
/// </param>
/// <param name="Reason">The message of the error that occurred while the file was read.</param>
/// <param name="IsEditorConfigFilePath">
/// <see langword="true" /> if <paramref name="FilePath" /> is the path of the <c>.editorconfig</c> file that could not
/// be read; otherwise, <see langword="false" />, if the failing <c>.editorconfig</c> file is not known and
/// <paramref name="FilePath" /> is the path of the file whose line ending was resolved. The default is
/// <see langword="true" />.
/// </param>
public sealed record EditorConfigUnreadableWarning(string FilePath, string Reason, bool IsEditorConfigFilePath = true)
    : KitWarning;
