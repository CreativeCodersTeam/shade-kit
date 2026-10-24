using System.Collections.Immutable;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Represents the content of a UTF-8 text file as a list of lines.
/// </summary>
/// <remarks>
/// Holds the lines without line endings, the UTF-8 byte order mark flag, and the final line ending flag; the line
/// ending is decided by the <see cref="ITextFileWriter" />. Lines are split at <c>\r\n</c>, <c>\n</c>, and a lone
/// <c>\r</c>, so a file is always written with one line ending; other Unicode line separators stay part of a line.
/// Instances are immutable; <see cref="Lines" /> cannot be changed through a cast either.
/// </remarks>
public sealed class TextFileContent
{
    private static readonly string[] LineSeparators = ["\r\n", "\r", "\n"];

    private TextFileContent(IEnumerable<string> lines, bool hasUtf8Bom, bool hasFinalLineEnding)
    {
        // An immutable copy, so neither the caller's collection nor a cast of Lines can change the content.
        ImmutableArray<string> lineArray = [.. lines];

        Lines = lineArray;
        HasUtf8Bom = hasUtf8Bom;

        // Without lines there is no line that could end with a line ending.
        HasFinalLineEnding = hasFinalLineEnding && lineArray.Length > 0;
    }

    /// <summary>
    /// Gets the lines of the file.
    /// </summary>
    /// <value>
    /// The lines in file order, without their line endings; a final line ending does not start another line. Empty for
    /// an empty file.
    /// </value>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>
    /// Gets a value indicating whether the file starts with a UTF-8 byte order mark.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if the file starts with the bytes <c>EF BB BF</c>; otherwise, <see langword="false" />.
    /// </value>
    public bool HasUtf8Bom { get; }

    /// <summary>
    /// Gets a value indicating whether the last line of the file ends with a line ending.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if the file has at least one line and its last line ends with a line ending;
    /// otherwise, <see langword="false" />.
    /// </value>
    public bool HasFinalLineEnding { get; }

    /// <summary>
    /// Creates the content of a file from its text.
    /// </summary>
    /// <remarks>
    /// The text is split into lines at <c>\r\n</c>, <c>\n</c>, and a lone <c>\r</c>. The content has no UTF-8 byte
    /// order mark, and it has a final line ending if the text ends with <c>\n</c> or <c>\r</c>.
    /// </remarks>
    /// <param name="text">The text of the file.</param>
    /// <returns>The content with the lines of <paramref name="text" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text" /> is <see langword="null" />.</exception>
    public static TextFileContent FromText(string text)
    {
        Ensure.NotNull(text);

        return FromText(text, hasUtf8Bom: false);
    }

    /// <summary>
    /// Reads the content of a UTF-8 text file.
    /// </summary>
    /// <remarks>
    /// A leading UTF-8 byte order mark is recorded in <see cref="HasUtf8Bom" /> and is not part of the first line.
    /// </remarks>
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
    /// The file starts with a UTF-16 or UTF-32 byte order mark, contains a NUL byte, or contains bytes that are not
    /// valid UTF-8.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    internal static Task<TextFileContent?> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        return ReadAsync(filePath, createEncodingException: null, cancellationToken);
    }

    /// <summary>
    /// Reads the content of a UTF-8 text file and creates the exception for content that is not UTF-8 text with the
    /// specified function.
    /// </summary>
    /// <remarks>
    /// A leading UTF-8 byte order mark is recorded in <see cref="HasUtf8Bom" /> and is not part of the first line.
    /// </remarks>
    /// <param name="filePath">The absolute path of the file.</param>
    /// <param name="createEncodingException">
    /// The function that creates the exception that is thrown when the file is not UTF-8 text, from the reason, a
    /// sentence that ends with the advice to save the file as UTF-8, and the exception of the decoder, which is
    /// <see langword="null" /> if the file has another Unicode encoding; or <see langword="null" /> to throw an
    /// <see cref="UnsupportedFileEncodingException" />.
    /// </param>
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
    /// The file is not UTF-8 text and <paramref name="createEncodingException" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="Exception">
    /// The file is not UTF-8 text; the exception is the one that <paramref name="createEncodingException" /> returns.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    internal static async Task<TextFileContent?> ReadAsync(string filePath,
        Func<string, Exception?, Exception>? createEncodingException, CancellationToken cancellationToken)
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

        var text = TextFileEncoding.DecodeUtf8(bytes, (encodingName, reason, decoderException) =>
            createEncodingException?.Invoke(reason, decoderException)
            ?? new UnsupportedFileEncodingException(filePath, encodingName));

        return FromText(text, bytes.AsSpan().StartsWith(TextFileEncoding.Utf8Bom));
    }

    /// <summary>
    /// Creates the content of a new file that contains only a block of lines.
    /// </summary>
    /// <param name="block">
    /// The lines of the file, separated by <c>\r\n</c>, <c>\n</c>, or <c>\r</c>, without a final line ending.
    /// </param>
    /// <returns>The lines of <paramref name="block" /> with a final line ending and without byte order mark.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="block" /> is <see langword="null" />.</exception>
    internal static TextFileContent CreateNew(string block)
    {
        Ensure.NotNull(block);

        return new TextFileContent(SplitBlock(block), hasUtf8Bom: false, hasFinalLineEnding: true);
    }

    /// <summary>
    /// Creates the content of the file with a block of lines added at its end.
    /// </summary>
    /// <remarks>
    /// If the file has lines, one empty line separates them from the block; no empty line is added if the last line
    /// of the file is already empty. The result always has a final line ending, so a missing one is added. The byte
    /// order mark is kept.
    /// </remarks>
    /// <param name="block">
    /// The lines to add, separated by <c>\r\n</c>, <c>\n</c>, or <c>\r</c>, without a final line ending.
    /// </param>
    /// <returns>The content with the lines of the file followed by the lines of <paramref name="block" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="block" /> is <see langword="null" />.</exception>
    internal TextFileContent AppendBlock(string block)
    {
        Ensure.NotNull(block);

        var lines = new List<string>(Lines);

        if (lines.Count > 0 && lines[^1].Length > 0)
        {
            lines.Add(string.Empty);
        }

        lines.AddRange(SplitBlock(block));

        return new TextFileContent(lines, HasUtf8Bom, hasFinalLineEnding: true);
    }

    /// <summary>
    /// Creates the content of the file with a range of lines replaced by a block of lines.
    /// </summary>
    /// <remarks>
    /// The lines before <paramref name="firstLineIndex" /> and after <paramref name="lastLineIndex" />, the byte
    /// order mark, and the final line ending are kept.
    /// </remarks>
    /// <param name="firstLineIndex">The zero-based index of the first line to replace.</param>
    /// <param name="lastLineIndex">The zero-based index of the last line to replace.</param>
    /// <param name="block">
    /// The replacement lines, separated by <c>\r\n</c>, <c>\n</c>, or <c>\r</c>, without a final line ending.
    /// </param>
    /// <returns>The content with the lines from <paramref name="firstLineIndex" /> to
    /// <paramref name="lastLineIndex" /> replaced by the lines of <paramref name="block" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="block" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="firstLineIndex" /> is less than 0, <paramref name="lastLineIndex" /> is not less than the
    /// number of lines, or <paramref name="lastLineIndex" /> is less than <paramref name="firstLineIndex" />.
    /// </exception>
    internal TextFileContent ReplaceLines(int firstLineIndex, int lastLineIndex, string block)
    {
        Ensure.NotNull(block);
        ArgumentOutOfRangeException.ThrowIfNegative(firstLineIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(lastLineIndex, Lines.Count);
        ArgumentOutOfRangeException.ThrowIfLessThan(lastLineIndex, firstLineIndex);

        string[] lines = [.. Lines.Take(firstLineIndex), .. SplitBlock(block), .. Lines.Skip(lastLineIndex + 1)];

        return new TextFileContent(lines, HasUtf8Bom, HasFinalLineEnding);
    }

    /// <summary>
    /// Determines whether the specified content has the same lines, byte order mark, and final line ending as this
    /// content.
    /// </summary>
    /// <remarks>
    /// Line endings are not part of the content, so two files that differ only in their line endings have the same
    /// content.
    /// </remarks>
    /// <param name="other">The content to compare with this content.</param>
    /// <returns>
    /// <see langword="true" /> if both contents have the same lines, the same <see cref="HasUtf8Bom" />, and the same
    /// <see cref="HasFinalLineEnding" />; otherwise, <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other" /> is <see langword="null" />.</exception>
    internal bool HasSameContent(TextFileContent other)
    {
        Ensure.NotNull(other);

        return HasUtf8Bom == other.HasUtf8Bom
               && HasFinalLineEnding == other.HasFinalLineEnding
               && Lines.SequenceEqual(other.Lines, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the text of the content, with the lines joined by the specified line ending.
    /// </summary>
    /// <remarks>
    /// The text ends with the line ending if <see cref="HasFinalLineEnding" /> is <see langword="true" />. The byte
    /// order mark is not part of the text.
    /// </remarks>
    /// <param name="lineEnding">The line ending that separates the lines and ends the last line.</param>
    /// <returns>The lines joined by <paramref name="lineEnding" />; an empty string if there are no lines.</returns>
    internal string ToText(LineEnding lineEnding)
    {
        var newLine = lineEnding == LineEnding.CrLf ? "\r\n" : "\n";
        var text = string.Join(newLine, Lines);

        return HasFinalLineEnding ? text + newLine : text;
    }

    private static TextFileContent FromText(string text, bool hasUtf8Bom)
    {
        if (text.Length == 0)
        {
            return new TextFileContent([], hasUtf8Bom, hasFinalLineEnding: false);
        }

        var lines = SplitBlock(text);
        var hasFinalLineEnding = text[^1] is '\n' or '\r';

        // A final line ending does not start another line, so the empty text after it is no line.
        return new TextFileContent(hasFinalLineEnding ? lines[..^1] : lines, hasUtf8Bom, hasFinalLineEnding);
    }

    /// <summary>
    /// Splits text into lines at <c>\r\n</c>, <c>\n</c>, and a lone <c>\r</c>, so that text that ends with a line
    /// ending has an empty last line.
    /// </summary>
    /// <remarks>
    /// A lone <c>\r</c>, for example from a file with classic Mac line endings, ends a line as well, so no
    /// <c>\r</c> is kept inside a line and the writer writes the whole file with one line ending.
    /// </remarks>
    private static string[] SplitBlock(string text)
    {
        // \r\n comes first, so it is one line ending and not a \r followed by a \n.
        return text.Split(LineSeparators, StringSplitOptions.None);
    }
}
