namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Represents the exception that is thrown when a text file that SHADE kit changes in place is not encoded
/// as UTF-8, so text cannot be inserted without mixing encodings.
/// </summary>
/// <remarks>
/// The encoding is recognised by the byte order mark at the start of the file: UTF-16 or UTF-32, each in little- or
/// big-endian byte order.
/// </remarks>
/// <param name="filePath">The path of the file.</param>
/// <param name="encodingName">The name of the encoding of the file, for example <c>UTF-16 LE</c>.</param>
public class UnsupportedFileEncodingException(string filePath, string encodingName)
    : Exception($"The file '{filePath}' is encoded as {encodingName}; only UTF-8 files can be changed.")
{
    /// <summary>
    /// Gets the path of the file whose encoding is not supported.
    /// </summary>
    /// <value>The path of the file as it was passed to the reader.</value>
    public string FilePath { get; } = filePath;

    /// <summary>
    /// Gets the name of the encoding of the file.
    /// </summary>
    /// <value>
    /// One of <c>UTF-16 LE</c>, <c>UTF-16 BE</c>, <c>UTF-32 LE</c>, or <c>UTF-32 BE</c>, after the byte order mark at
    /// the start of the file.
    /// </value>
    public string EncodingName { get; } = encodingName;
}
