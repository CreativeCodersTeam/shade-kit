using System.Text;
using CreativeCoders.Core;

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

    /// <summary>
    /// Represents the encoding name that is reported for content that contains bytes that are not valid UTF-8.
    /// </summary>
    public const string InvalidUtf8EncodingName = "an unknown encoding with bytes that are not valid UTF-8";

    private const string SaveAsUtf8Hint = "Save the file as UTF-8 and run the command again.";

    // The byte order marks of the encodings that are not compatible with UTF-8. UTF-32 LE starts with the UTF-16 LE
    // mark, so it must be checked first.
    private static readonly (byte[] Bom, string EncodingName)[] UnsupportedBoms =
    [
        ([0xFF, 0xFE, 0x00, 0x00], "UTF-32 LE"),
        ([0x00, 0x00, 0xFE, 0xFF], "UTF-32 BE"),
        ([0xFF, 0xFE], "UTF-16 LE"),
        ([0xFE, 0xFF], "UTF-16 BE")
    ];

    // Decodes strictly, so invalid bytes stop the caller instead of being replaced with U+FFFD.
    private static readonly UTF8Encoding StrictUtf8 =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Gets the encoding with which SHADE kit writes text: UTF-8 without byte order mark.
    /// </summary>
    /// <value>
    /// A UTF-8 encoding that emits no byte order mark and replaces invalid bytes with U+FFFD when it decodes.
    /// </value>
    public static UTF8Encoding Utf8WithoutBom { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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

    /// <summary>
    /// Decodes the specified content strictly as UTF-8 text, without a leading UTF-8 byte order mark.
    /// </summary>
    /// <remarks>
    /// The content is rejected if <see cref="FindUnsupportedEncodingName" /> finds another Unicode encoding or if it
    /// contains bytes that are not valid UTF-8. The reason passed to <paramref name="createException" /> is a sentence
    /// that ends with the advice to save the file as UTF-8 and to run the command again.
    /// </remarks>
    /// <param name="bytes">The content of a text file.</param>
    /// <param name="createException">
    /// The function that creates the exception that is thrown when the content is not UTF-8 text, from the name of the
    /// encoding (the one <see cref="FindUnsupportedEncodingName" /> finds, or <see cref="InvalidUtf8EncodingName" />),
    /// the reason, and the exception of the decoder, which is <see langword="null" /> if the content has another
    /// encoding.
    /// </param>
    /// <returns>The decoded text.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="createException" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="Exception">
    /// The content is not UTF-8 text; the exception is the one that <paramref name="createException" /> returns.
    /// </exception>
    public static string DecodeUtf8(ReadOnlySpan<byte> bytes,
        Func<string, string, Exception?, Exception> createException)
    {
        Ensure.NotNull(createException);

        var encodingName = FindUnsupportedEncodingName(bytes);

        if (encodingName == UnmarkedWideEncodingName)
        {
            throw createException(encodingName,
                "The file contains a NUL byte, so it is not UTF-8 text; it may be UTF-16 or UTF-32 without byte " +
                $"order mark, or a binary file. {SaveAsUtf8Hint}", null);
        }

        if (encodingName is not null)
        {
            throw createException(encodingName,
                $"The file is encoded as {encodingName}, not as UTF-8. {SaveAsUtf8Hint}", null);
        }

        try
        {
            return StrictUtf8.GetString(SkipUtf8Bom(bytes));
        }
        catch (DecoderFallbackException exception)
        {
            throw createException(InvalidUtf8EncodingName,
                $"The file contains bytes that are not valid UTF-8. {SaveAsUtf8Hint}", exception);
        }
    }
}
