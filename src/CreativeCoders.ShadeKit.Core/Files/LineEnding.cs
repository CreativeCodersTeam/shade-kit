namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Specifies the character sequence that ends a line in a text file.
/// </summary>
public enum LineEnding
{
    /// <summary>
    /// A line feed (<c>\n</c>), the line ending of Linux and macOS.
    /// </summary>
    Lf,

    /// <summary>
    /// A carriage return followed by a line feed (<c>\r\n</c>), the line ending of Windows.
    /// </summary>
    CrLf
}
