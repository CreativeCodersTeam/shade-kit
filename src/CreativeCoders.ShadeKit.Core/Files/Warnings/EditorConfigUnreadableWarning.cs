using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Files.Warnings;

/// <summary>
/// Represents a warning that an <c>.editorconfig</c> file could not be read, so the line ending of the operating
/// system was used.
/// </summary>
/// <param name="FilePath">
/// The absolute path of the unreadable <c>.editorconfig</c> file, or of the resolved file if that one is not known.
/// </param>
/// <param name="Reason">The message of the error that occurred while the file was read.</param>
/// <param name="IsEditorConfigFilePath">
/// <see langword="true" /> if <paramref name="FilePath" /> is the unreadable <c>.editorconfig</c> file; otherwise,
/// <see langword="false" />. The default is <see langword="true" />.
/// </param>
public sealed record EditorConfigUnreadableWarning(string FilePath, string Reason, bool IsEditorConfigFilePath = true)
    : KitWarning;
