namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Represents one line of a <see cref="TextFileContent" /> together with its position in the bytes of the file.
/// </summary>
/// <param name="Text">The decoded text of the line, without its line terminator. It is used for matching only.</param>
/// <param name="Start">The byte offset at which the line starts, after a leading byte order mark.</param>
/// <param name="ContentEnd">The byte offset at which the line terminator (<c>\n</c> or <c>\r\n</c>) starts, or the
/// length of the file for a last line without terminator.</param>
internal readonly record struct TextFileLine(string Text, int Start, int ContentEnd);
