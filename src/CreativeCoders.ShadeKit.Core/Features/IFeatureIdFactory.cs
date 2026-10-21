namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Derives the id, title, and source of a new feature from the input of <c>shade-kit feature new</c>.
/// </summary>
public interface IFeatureIdFactory
{
    /// <summary>
    /// Creates the identity of a new feature from the specified source and title.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The kind of <paramref name="source" /> is detected in this order: an absolute <c>http</c> or <c>https</c> URL is
    /// a URL source; otherwise a path, relative to <paramref name="workingDirectory" /> or absolute, for which
    /// <see cref="File.Exists" /> is <see langword="true" /> is a file source; otherwise <paramref name="source" /> is a
    /// text source. The path of a directory, or of no existing entry, is therefore a text source. On Unix,
    /// <see cref="File.Exists" /> is also <see langword="true" /> for FIFOs, devices, sockets, and symbolic links whose
    /// target does not exist; such an entry is read like a file, and an entry that cannot be opened or read is refused.
    /// Opening a FIFO that no process writes to blocks; this is not detected.
    /// </para>
    /// <para>
    /// The file of a file source must have at most <see cref="FeatureIdFactory.MaxSourceFileSize" /> bytes (1 MiB); at
    /// most one byte more is read, so a larger file or an endless device such as <c>/dev/zero</c> is refused without
    /// being read completely. An entry that reads no bytes, such as <c>/dev/null</c>, is an empty file.
    /// The file must be UTF-8 text, with or without byte order mark; the byte order mark is removed.
    /// A file that starts with a UTF-16 or UTF-32 byte order mark, contains a NUL byte, or contains bytes that are not
    /// valid UTF-8 is refused, so the original input is never stored with replacement characters.
    /// </para>
    /// <para>
    /// A URL whose host is a key of <paramref name="trackers" /> (compared exactly but ignoring case) gives the
    /// issue-based id <c>&lt;prefix&gt;-&lt;number&gt;-&lt;slug&gt;</c>, where the number is the last non-empty
    /// segment of the URL path if it consists only of ASCII digits; the query and the fragment are ignored. Every
    /// other source gives the time-based id <c>&lt;yyyyMMdd&gt;-&lt;HHmm&gt;-&lt;slug&gt;</c> in local time.
    /// </para>
    /// <para>
    /// <paramref name="title" /> is used if it is not <see langword="null" />, empty, or whitespace. Otherwise the
    /// title is the first non-empty line of a text source, or the first level-1 ATX heading (<c># …</c>) of a file
    /// source, else the file name without extension. A URL source needs <paramref name="title" />. The title, given or
    /// derived, is trimmed and must not contain a control character (<see cref="char.IsControl(char)" />), such as a
    /// line feed, a carriage return, or a tab. The slug is built from the title by the rules of the feature slug: ASCII
    /// lower case, at most 40 characters.
    /// </para>
    /// </remarks>
    /// <param name="source">The value of <c>--source</c>: free text, a URL, or a file path.</param>
    /// <param name="title">The value of <c>--title</c>, or <see langword="null" /> if none was given.</param>
    /// <param name="workingDirectory">The directory against which a relative file path is resolved.</param>
    /// <param name="trackers">The id prefixes keyed by issue URL host, from the <c>trackers</c> field of
    /// <c>config.json</c>.</param>
    /// <param name="repositoryRootPath">The root directory of the repository, against which the reference of a file
    /// source is made relative.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the id, title, source, original
    /// input, and creation time of the new feature.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source" /> or <paramref name="trackers" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="workingDirectory" /> or <paramref name="repositoryRootPath" /> is <see langword="null" />,
    /// empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidFeatureSourceException">
    /// <paramref name="source" /> is empty or whitespace; it is a URL whose host is a tracker but whose path has no
    /// issue number; or it names a file that cannot be read, has more than
    /// <see cref="FeatureIdFactory.MaxSourceFileSize" /> bytes, or is not UTF-8 text.
    /// </exception>
    /// <exception cref="InvalidFeatureTitleException">
    /// <paramref name="source" /> is a URL and no <paramref name="title" /> was given, the title contains a control
    /// character, or the title contains no letter or digit from which a slug can be built.
    /// </exception>
    /// <exception cref="InvalidFeatureIdException">
    /// The derived id is not valid, for example because a tracker prefix contains upper-case letters.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<FeatureIdentity> CreateAsync(string source, string? title, string workingDirectory,
        IReadOnlyDictionary<string, string> trackers, string repositoryRootPath,
        CancellationToken cancellationToken = default);
}
