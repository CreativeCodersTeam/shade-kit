using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;

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
/// An <see cref="EditorConfigUnreadableWarning" /> or <see cref="EditorConfigInvalidWarning" /> if a file could
/// not be used, in which case <paramref name="LineEnding" /> is <see langword="null" />; otherwise,
/// <see langword="null" />.
/// </param>
public sealed record EditorConfigLineEndingResult(LineEnding? LineEnding, KitWarning? Warning);
