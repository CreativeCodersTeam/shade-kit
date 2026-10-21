namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Provides the rules by which SHADE kit tells UTF-8 text files from text files in other Unicode encodings.
/// </summary>
/// <remarks>
/// SHADE kit reads and writes text as UTF-8, with or without byte order mark. A file that starts with a UTF-16 or
/// UTF-32 byte order mark, or that contains a NUL byte, which UTF-16 and UTF-32 without byte order mark do for every
/// ASCII character, is not UTF-8 text.
/// </remarks>
internal static class TextFileEncoding
{
    /// <summary>
    /// Represents the encoding name that <see cref="FindUnsupportedEncodingName" /> returns for content without a
    /// byte order mark that contains a NUL byte.
    /// </summary>
    public const string UnmarkedWideEncodingName = "UTF-16 or UTF-32 without byte order mark";

    // The byte order marks of the encodings that are not compatible with UTF-8. UTF-32 LE starts with the UTF-16 LE
    // mark, so it must be checked first.
    private static readonly (byte[] Bom, string EncodingName)[] UnsupportedBoms =
    [
        ([0xFF, 0xFE, 0x00, 0x00], "UTF-32 LE"),
        ([0x00, 0x00, 0xFE, 0xFF], "UTF-32 BE"),
        ([0xFF, 0xFE], "UTF-16 LE"),
        ([0xFE, 0xFF], "UTF-16 BE")
    ];

    /// <summary>
    /// Gets the UTF-8 byte order mark.
    /// </summary>
    /// <value>The bytes <c>EF BB BF</c>.</value>
    public static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Removes a leading UTF-8 byte order mark from the specified content.
    /// </summary>
    /// <param name="bytes">The content of a text file.</param>
    /// <returns>
    /// The content after the UTF-8 byte order mark if it starts with one; otherwise, <paramref name="bytes" />
    /// unchanged.
    /// </returns>
    public static ReadOnlySpan<byte> SkipUtf8Bom(ReadOnlySpan<byte> bytes)
    {
        return bytes.StartsWith(Utf8Bom) ? bytes[Utf8Bom.Length..] : bytes;
    }

    /// <summary>
    /// Finds the name of the encoding of the specified content if it is a Unicode encoding other than UTF-8.
    /// </summary>
    /// <remarks>
    /// The content is not checked for invalid UTF-8 sequences; callers that need valid UTF-8 decode it strictly.
    /// </remarks>
    /// <param name="bytes">The content of a text file.</param>
    /// <returns>
    /// <c>UTF-32 LE</c>, <c>UTF-32 BE</c>, <c>UTF-16 LE</c>, or <c>UTF-16 BE</c> if the content starts with the
    /// byte order mark of that encoding; <see cref="UnmarkedWideEncodingName" /> if it contains a NUL byte; otherwise,
    /// <see langword="null" />.
    /// </returns>
    public static string? FindUnsupportedEncodingName(ReadOnlySpan<byte> bytes)
    {
        foreach (var (bom, encodingName) in UnsupportedBoms)
        {
            if (bytes.StartsWith(bom))
            {
                return encodingName;
            }
        }

        // UTF-8 text practically never contains a NUL character, but UTF-16 and UTF-32 encode every ASCII character
        // with NUL bytes.
        return bytes.Contains((byte)0) ? UnmarkedWideEncodingName : null;
    }
}
