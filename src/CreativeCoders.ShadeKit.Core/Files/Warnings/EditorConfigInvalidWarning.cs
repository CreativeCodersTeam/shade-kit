using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Files.Warnings;

/// <summary>
/// Represents a warning that an <c>.editorconfig</c> file could be read but not evaluated, so the line ending of
/// the operating system was used.
/// </summary>
/// <remarks>
/// For example, a numeric range that overflows, such as <c>[{1..99999999999}]</c>, makes the parser fail.
/// </remarks>
/// <param name="FilePath">
/// The absolute path of the failing <c>.editorconfig</c> file, or of the resolved file if that one is not known.
/// </param>
/// <param name="Reason">The message of the error that occurred while the file was evaluated.</param>
/// <param name="IsEditorConfigFilePath">
/// <see langword="true" /> if <paramref name="FilePath" /> is the failing <c>.editorconfig</c> file; otherwise,
/// <see langword="false" />. The default is <see langword="true" />.
/// </param>
public sealed record EditorConfigInvalidWarning(string FilePath, string Reason, bool IsEditorConfigFilePath = true)
    : KitWarning;
