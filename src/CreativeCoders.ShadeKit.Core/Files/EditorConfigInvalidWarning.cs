namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Represents a warning that an <c>.editorconfig</c> file could be read but not evaluated because of its content, so
/// the <c>.editorconfig</c> files did not decide the line ending of a file and the line ending of the operating system
/// was used.
/// </summary>
/// <remarks>
/// The EditorConfig core library fails, for example, on a section header with a numeric range that does not fit into
/// a 32-bit integer or that expands to too many patterns, such as <c>[{1..99999999999}]</c>.
/// </remarks>
/// <param name="FilePath">
/// The absolute path of the <c>.editorconfig</c> file that could not be evaluated, or the absolute path of the file
/// whose line ending was resolved if the failing <c>.editorconfig</c> file is not known;
/// <paramref name="IsEditorConfigFilePath" /> tells which.
/// </param>
/// <param name="Reason">The message of the error that occurred while the file was evaluated.</param>
/// <param name="IsEditorConfigFilePath">
/// <see langword="true" /> if <paramref name="FilePath" /> is the path of the <c>.editorconfig</c> file that could not
/// be evaluated; otherwise, <see langword="false" />, if the failing <c>.editorconfig</c> file is not known and
/// <paramref name="FilePath" /> is the path of the file whose line ending was resolved. The default is
/// <see langword="true" />.
/// </param>
public sealed record EditorConfigInvalidWarning(string FilePath, string Reason, bool IsEditorConfigFilePath = true)
    : KitWarning;
