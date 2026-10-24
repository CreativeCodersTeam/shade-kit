namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Represents the line ending that the <c>.editorconfig</c> files prescribe for a file, together with the warning of
/// an <c>.editorconfig</c> file that could not be used.
/// </summary>
/// <param name="LineEnding">
/// The line ending that the <c>end_of_line</c> property prescribes, or <see langword="null" /> if the
/// <c>.editorconfig</c> files do not decide.
/// </param>
/// <param name="Warning">
/// The warning about the <c>.editorconfig</c> file that could not be used, or <see langword="null" /> if all files were
/// read and evaluated: an <see cref="EditorConfigUnreadableWarning" /> if a file could not be read, or an
/// <see cref="EditorConfigInvalidWarning" /> if a file could not be evaluated because of its content. If it is set,
/// <paramref name="LineEnding" /> is <see langword="null" />.
/// </param>
public sealed record EditorConfigLineEndingResult(LineEnding? LineEnding, KitWarning? Warning);
