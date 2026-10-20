using System.Text;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Represents the content of a text file that SHADE kit changes in place, so that every byte outside the
/// changed part stays as it is.
/// </summary>
/// <remarks>
/// <para>
/// The content is kept as bytes. Lines are decoded only to match them; they are never encoded again, so a file with
/// any encoding that is compatible with ASCII line terminators keeps its bytes outside the changed part. A file that
/// starts with a UTF-16 or UTF-32 byte order mark, or that contains a NUL byte, which UTF-16 and UTF-32 without byte
/// order mark do, is refused when it is read, because UTF-8 text inserted into it would mix two encodings.
/// </para>
/// <para>
/// Inserted text is passed as a block of lines separated by <c>\n</c>, without a final line terminator. It is written
/// as UTF-8 with the line terminator of the file: <c>\r\n</c> if the file contains one, otherwise <c>\n</c>. A
/// leading UTF-8 byte order mark is kept. A new file is written as UTF-8 without byte order mark and with <c>\n</c>.
/// </para>
/// </remarks>
internal sealed class TextFileContent
{
    private const string Lf = "\n";

    private const string CrLf = "\r\n";

    private const string UnmarkedWideEncodingName = "UTF-16 or UTF-32 without byte order mark";

    private readonly byte[] _bytes;

    // The offset at which the text starts, after a leading UTF-8 byte order mark.
    private readonly int _textStart;

    // The byte order marks of the encodings that are not compatible with ASCII line terminators. UTF-32 LE starts
    // with the UTF-16 LE mark, so it must be checked first.
    private static readonly (byte[] Bom, string EncodingName)[] UnsupportedBoms =
    [
        ([0xFF, 0xFE, 0x00, 0x00], "UTF-32 LE"),
        ([0x00, 0x00, 0xFE, 0xFF], "UTF-32 BE"),
        ([0xFF, 0xFE], "UTF-16 LE"),
        ([0xFE, 0xFF], "UTF-16 BE")
    ];

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private TextFileContent(byte[] bytes)
    {
        _bytes = bytes;
        _textStart = bytes.AsSpan().StartsWith(Utf8Bom) ? Utf8Bom.Length : 0;
        NewLine = bytes.AsSpan().IndexOf("\r\n"u8) >= 0 ? CrLf : Lf;
        Lines = SplitLines(bytes, _textStart);
    }

    /// <summary>
    /// Gets the lines of the file.
    /// </summary>
    /// <value>The lines in file order; a final line terminator does not start another line.</value>
    public IReadOnlyList<TextFileLine> Lines { get; }

    /// <summary>
    /// Gets the line terminator that inserted text uses.
    /// </summary>
    /// <value><c>\r\n</c> if the file contains one; otherwise, <c>\n</c>.</value>
    public string NewLine { get; }

    /// <summary>
    /// Reads the content of a text file.
    /// </summary>
    /// <param name="filePath">The absolute path of the file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the content of the file, or
    /// <see langword="null" /> if the file does not exist.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath" /> is <see langword="null" />, empty, whitespace, or not an absolute path.
    /// </exception>
    /// <exception cref="PathNotAFileException"><paramref name="filePath" /> is an existing directory.</exception>
    /// <exception cref="UnsupportedFileEncodingException">
    /// The file starts with a UTF-16 or UTF-32 byte order mark or contains a NUL byte.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    public static async Task<TextFileContent?> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        Ensure.IsNotNullOrWhitespace(filePath);

        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException($"The path '{filePath}' must be absolute.", nameof(filePath));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(filePath))
        {
            throw new PathNotAFileException(filePath);
        }

        if (!File.Exists(filePath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (FindUnsupportedEncodingName(bytes) is { } encodingName)
        {
            throw new UnsupportedFileEncodingException(filePath, encodingName);
        }

        return new TextFileContent(bytes);
    }

    /// <summary>
    /// Creates the bytes of a new file that contains only a block of lines.
    /// </summary>
    /// <param name="block">The lines of the file, separated by <c>\n</c>, without a final line terminator.</param>
    /// <returns>The block followed by <c>\n</c>, encoded as UTF-8 without byte order mark.</returns>
    public static byte[] CreateNew(string block)
    {
        return Encoding.UTF8.GetBytes(block + Lf);
    }

    /// <summary>
    /// Creates the bytes of the file with a block of lines added at its end.
    /// </summary>
    /// <remarks>
    /// A missing final line terminator is added first. If the file has content, one blank line separates it from the
    /// block; no blank line is added if the file already ends with an empty line. The block ends with a line
    /// terminator.
    /// </remarks>
    /// <param name="block">The lines to add, separated by <c>\n</c>, without a final line terminator.</param>
    /// <returns>The unchanged bytes of the file followed by the added text.</returns>
    public byte[] AppendBlock(string block)
    {
        var text = new StringBuilder();

        if (_bytes.Length > _textStart)
        {
            if (_bytes[^1] != (byte)'\n')
            {
                text.Append(NewLine);
            }

            if (!EndsWithEmptyLine)
            {
                text.Append(NewLine);
            }
        }

        text.Append(ToFileLineEndings(block)).Append(NewLine);

        return [.. _bytes, .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    /// <summary>
    /// Creates the bytes of the file with a range of lines replaced by a block of lines.
    /// </summary>
    /// <param name="first">The first line to replace.</param>
    /// <param name="last">The last line to replace; its line terminator is kept.</param>
    /// <param name="block">The replacement lines, separated by <c>\n</c>, without a final line terminator.</param>
    /// <returns>The unchanged bytes before <paramref name="first" />, the block, and the unchanged bytes from the line
    /// terminator of <paramref name="last" /> on.</returns>
    public byte[] ReplaceLines(TextFileLine first, TextFileLine last, string block)
    {
        return
        [
            .. _bytes.AsSpan(0, first.Start),
            .. Encoding.UTF8.GetBytes(ToFileLineEndings(block)),
            .. _bytes.AsSpan(last.ContentEnd)
        ];
    }

    /// <summary>
    /// Determines whether the file consists of exactly the specified bytes.
    /// </summary>
    /// <param name="bytes">The bytes to compare with the file.</param>
    /// <returns><see langword="true" /> if the file has the same bytes; otherwise, <see langword="false" />.</returns>
    public bool HasBytes(ReadOnlySpan<byte> bytes)
    {
        return bytes.SequenceEqual(_bytes);
    }

    // A last line without line terminator is never empty, so an empty last line is always followed by one.
    private bool EndsWithEmptyLine => Lines.Count > 0 && Lines[^1].Text.Length == 0;

    private static string? FindUnsupportedEncodingName(ReadOnlySpan<byte> bytes)
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

    private string ToFileLineEndings(string block)
    {
        return block.ReplaceLineEndings(NewLine);
    }

    private static TextFileLine[] SplitLines(byte[] bytes, int textStart)
    {
        var lines = new List<TextFileLine>();

        for (var start = textStart; start < bytes.Length;)
        {
            var lineFeedOffset = bytes.AsSpan(start).IndexOf((byte)'\n');
            var end = lineFeedOffset < 0 ? bytes.Length : start + lineFeedOffset;
            var contentEnd = lineFeedOffset >= 0 && end > start && bytes[end - 1] == (byte)'\r' ? end - 1 : end;

            lines.Add(new TextFileLine(Encoding.UTF8.GetString(bytes, start, contentEnd - start), start, contentEnd));

            start = end + 1;
        }

        return [.. lines];
    }
}
