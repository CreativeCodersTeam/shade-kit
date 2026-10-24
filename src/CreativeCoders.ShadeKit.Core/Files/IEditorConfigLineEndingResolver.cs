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
    /// The <c>.editorconfig</c> files are read from the directory of the file up to the first file with
    /// <c>root = true</c> or the root of the file system, with the precedence rules of the EditorConfig
    /// specification. The file itself does not need to exist. The method is synchronous because the files are read
    /// with a synchronous parser.
    /// <para>
    /// If one of the <c>.editorconfig</c> files cannot be read because of an I/O error or missing permissions, the
    /// <c>.editorconfig</c> files do not decide and the result carries an <see cref="EditorConfigUnreadableWarning" />
    /// instead of throwing an exception.
    /// </para>
    /// <para>
    /// If one of the <c>.editorconfig</c> files can be read but not evaluated because of its content, for example a
    /// section header with a numeric range such as <c>[{1..99999999999}]</c>, the <c>.editorconfig</c> files do not
    /// decide either and the result carries an <see cref="EditorConfigInvalidWarning" />.
    /// </para>
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
