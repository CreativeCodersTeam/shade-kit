using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Reads a stream of limited length and decodes it strictly as UTF-8 text.
/// </summary>
internal static class LimitedUtf8Reader
{
    // Below the large object heap threshold of 85,000 bytes, so a read of a short text allocates no large buffer.
    private const int ChunkLength = 81_920;

    /// <summary>
    /// Reads the specified stream up to its end and decodes the content strictly as UTF-8 text, without a leading
    /// UTF-8 byte order mark.
    /// </summary>
    /// <remarks>
    /// At most <paramref name="maxLength" /> + 1 bytes are read, so longer content is detected without reading it
    /// completely. The length of the stream is not used, because devices such as <c>/dev/zero</c> report a length of
    /// 0 and a server can send more or less than it announces.
    /// </remarks>
    /// <param name="stream">The stream to read.</param>
    /// <param name="maxLength">The maximum number of bytes of the content.</param>
    /// <param name="createTooLongException">
    /// The function that creates the exception that is thrown when the content is longer than
    /// <paramref name="maxLength" /> bytes.
    /// </param>
    /// <param name="createDecodeException">
    /// The function that creates the exception that is thrown when the content is not UTF-8 text, as described for
    /// <see cref="TextFileEncoding.DecodeUtf8" />.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read. The task result contains the decoded text.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="stream" />, <paramref name="createTooLongException" />, or
    /// <paramref name="createDecodeException" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="Exception">
    /// The content is too long or not UTF-8 text; the exception is the one that the matching function returns.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    public static async Task<string> ReadAsync(Stream stream, int maxLength, Func<Exception> createTooLongException,
        Func<string, string, Exception?, Exception> createDecodeException, CancellationToken cancellationToken)
    {
        Ensure.NotNull(stream);
        Ensure.NotNull(createTooLongException);
        Ensure.NotNull(createDecodeException);

        using var content = new MemoryStream();
        var chunk = new byte[ChunkLength];

        while (true)
        {
            var remainingLength = (int)Math.Min(chunk.Length, maxLength + 1L - content.Length);
            var readLength = await stream.ReadAsync(chunk.AsMemory(0, remainingLength), cancellationToken)
                .ConfigureAwait(false);

            if (readLength == 0)
            {
                break;
            }

            await content.WriteAsync(chunk.AsMemory(0, readLength), cancellationToken).ConfigureAwait(false);

            if (content.Length > maxLength)
            {
                throw createTooLongException();
            }
        }

        return TextFileEncoding.DecodeUtf8(content.GetBuffer().AsSpan(0, (int)content.Length),
            createDecodeException);
    }
}
