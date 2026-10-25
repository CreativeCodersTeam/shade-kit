using CreativeCoders.ShadeKit.Core.Features.Exceptions;

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
    /// <paramref name="source" /> is a URL source if it is an absolute <c>http</c> or <c>https</c> URL, a file
    /// source if <see cref="File.Exists" /> is <see langword="true" /> for it, and a text source otherwise. A file must
    /// be UTF-8 text of at most <see cref="FeatureSourceReader.MaxSourceLength" /> bytes; a byte order mark is removed.
    /// </para>
    /// <para>
    /// A URL whose host is a key of <paramref name="trackers" /> (ignoring case) gives the id
    /// <c>&lt;prefix&gt;-&lt;number&gt;-&lt;slug&gt;</c>, with the number from the last path segment; every other
    /// source gives <c>&lt;yyyyMMdd&gt;-&lt;HHmm&gt;-&lt;slug&gt;</c> in local time.
    /// </para>
    /// <para>
    /// The request content is the text for a text source and the file content for a file source. For a URL whose host
    /// is a tracker, the issue is read in <paramref name="repositoryRootPath" /> and formatted as Markdown with its
    /// title, description, and comments; for any other URL, the content of the URL is read as UTF-8 text. Every
    /// content is loaded before the method returns.
    /// </para>
    /// <para>
    /// Without <paramref name="title" />, the title is the first non-empty line of a text, the first level-1 heading of
    /// a file, else the file name, or the title of the issue of a tracker URL; a URL whose host is not a tracker needs
    /// <paramref name="title" />. A title must not contain control characters.
    /// </para>
    /// </remarks>
    /// <param name="source">The value of <c>--source</c>: free text, a URL, or a file path.</param>
    /// <param name="title">The value of <c>--title</c>, or <see langword="null" /> if none was given.</param>
    /// <param name="workingDirectory">The directory against which a relative file path is resolved.</param>
    /// <param name="trackers">The id prefixes keyed by issue URL host, from the <c>trackers</c> field of
    /// <c>config.json</c>.</param>
    /// <param name="repositoryRootPath">The root directory of the repository, against which the reference of a file
    /// source is made relative and in which the issue of a tracker URL is read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the id, title, source, request
    /// content, and creation time of the new feature.
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
    /// issue number; it is a URL whose issue cannot be read, because <c>gh</c> cannot be found or started, fails, or
    /// returns data that cannot be used; it is any other URL whose content cannot be read; or it names a file that
    /// cannot be read, has more than <see cref="FeatureSourceReader.MaxSourceLength" /> bytes, or is not UTF-8 text.
    /// The exception of the reader is the inner exception.
    /// </exception>
    /// <exception cref="InvalidFeatureTitleException">
    /// <paramref name="source" /> is a URL whose host is not a tracker and no <paramref name="title" /> was given, the
    /// title contains a control character, or the title contains no letter or digit from which a slug can be built.
    /// </exception>
    /// <exception cref="InvalidFeatureIdException">
    /// The derived id is not valid, for example because a tracker prefix contains upper-case letters.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<FeatureIdentity> CreateAsync(string source, string? title, string workingDirectory,
        IReadOnlyDictionary<string, string> trackers, string repositoryRootPath,
        CancellationToken cancellationToken = default);
}
