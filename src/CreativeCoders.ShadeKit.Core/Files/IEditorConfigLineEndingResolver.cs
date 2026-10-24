using CreativeCoders.ShadeKit.Core.Files.Warnings;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Determines the line ending that the <c>.editorconfig</c> files prescribe for a file.
/// </summary>
public interface IEditorConfigLineEndingResolver
{
    /// <summary>
    /// Gets the line ending that the <c>end_of_line</c> property of the <c>.editorconfig</c> files prescribes for the
    /// specified file.
    /// </summary>
    /// <remarks>
    /// Follows the EditorConfig precedence rules up to <c>root = true</c>; the file need not exist. An
    /// <c>.editorconfig</c> file that cannot be read or evaluated does not throw: the files do not decide, and the
    /// result carries an <see cref="EditorConfigUnreadableWarning" /> or <see cref="EditorConfigInvalidWarning" />.
    /// </remarks>
    /// <param name="filePath">The absolute path of the file.</param>
    /// <returns>
    /// The result with <see cref="LineEnding.Lf" /> for <c>end_of_line = lf</c>, <see cref="LineEnding.CrLf" /> for
    /// <c>end_of_line = crlf</c>, or no line ending if <c>end_of_line</c> is not set, has any other value, such as
    /// <c>cr</c>, or an <c>.editorconfig</c> file cannot be read or evaluated.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath" /> is <see langword="null" />, empty, or whitespace, or it is not an absolute path.
    /// </exception>
    EditorConfigLineEndingResult GetLineEnding(string filePath);
}
