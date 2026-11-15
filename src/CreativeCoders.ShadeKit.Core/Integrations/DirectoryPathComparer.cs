using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Compares absolute directory paths after resolving their symbolic links, with the case sensitivity of the file
/// system of an operating system.
/// </summary>
/// <remarks>
/// <para>
/// A path that exists is resolved segment by segment, as <c>realpath</c> does: every segment that is a symbolic link
/// (or a junction on Windows) is replaced by its target, and the target is resolved again, so a link in a parent
/// directory counts too. On macOS, for example, <c>/tmp</c> is a link to <c>private/tmp</c> and
/// <c>/var</c> a link to <c>private/var</c>, and neither <see cref="Path.GetFullPath(string)" /> nor
/// <see cref="Directory.ResolveLinkTarget(string, bool)" /> resolves a link in a parent directory. The <c>..</c>
/// segments of the path itself are removed first, as <see cref="Path.GetFullPath(string)" /> does, so that the path
/// means what it means for the source that SHADE kit passes to the agent CLI; the <c>..</c> segments of a relative
/// link target are removed against the resolved directory of the link, as the operating system does.
/// </para>
/// <para>
/// A path that does not exist, a path whose links cannot be read, and a path with more than
/// <see cref="MaxLinkCount" /> links are compared in their normalized form without link resolution.
/// </para>
/// </remarks>
/// <param name="comparison">
/// One of the enumeration values that specifies how the resolved paths are compared.
/// </param>
internal sealed class DirectoryPathComparer(StringComparison comparison)
{
    /// <summary>
    /// Represents the largest number of symbolic links that are followed while one path is resolved, the limit of
    /// <c>SYMLOOP_MAX</c> on Linux.
    /// </summary>
    internal const int MaxLinkCount = 40;

    private static readonly char[] DirectorySeparators =
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// Gets the comparison of the resolved paths.
    /// </summary>
    /// <value>
    /// One of the enumeration values that specifies how the resolved paths are compared.
    /// </value>
    public StringComparison Comparison { get; } = comparison;

    /// <summary>
    /// Creates the comparer for the current operating system.
    /// </summary>
    /// <returns>
    /// A comparer that compares case-insensitively on Windows and macOS and case-sensitively on every other operating
    /// system.
    /// </returns>
    public static DirectoryPathComparer ForCurrentOperatingSystem()
    {
        return new DirectoryPathComparer(GetComparison(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()));
    }

    /// <summary>
    /// Returns the comparison of directory paths for an operating system.
    /// </summary>
    /// <param name="isWindows">
    /// <see langword="true" /> if the operating system is Windows; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="isMacOS">
    /// <see langword="true" /> if the operating system is macOS; otherwise, <see langword="false" />.
    /// </param>
    /// <returns>
    /// <see cref="StringComparison.OrdinalIgnoreCase" /> for Windows and macOS, whose default file systems ignore the
    /// case; otherwise, <see cref="StringComparison.Ordinal" />.
    /// </returns>
    internal static StringComparison GetComparison(bool isWindows, bool isMacOS)
    {
        return isWindows || isMacOS ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    /// <summary>
    /// Determines whether two absolute paths name the same directory.
    /// </summary>
    /// <param name="first">The first absolute path.</param>
    /// <param name="second">The second absolute path.</param>
    /// <returns>
    /// <see langword="true" /> if the resolved paths are equal under <see cref="Comparison" />; otherwise,
    /// <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="first" /> or <paramref name="second" /> is not a fully qualified path.
    /// </exception>
    public bool AreSameDirectory(string first, string second)
    {
        return string.Equals(ResolvePath(first), ResolvePath(second), Comparison);
    }

    /// <summary>
    /// Returns the specified absolute path with every symbolic link resolved to its final target.
    /// </summary>
    /// <param name="path">The absolute path.</param>
    /// <returns>
    /// The resolved path without a trailing directory separator; or, for a path that does not exist, whose links
    /// cannot be read, or that has more than <see cref="MaxLinkCount" /> links, the normalized path from
    /// <see cref="MarketplaceSource.NormalizeDirectoryPath(string)" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="path" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="path" /> is not a fully qualified path.</exception>
    internal static string ResolvePath(string path)
    {
        Ensure.NotNull(path);

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"'{path}' is not a fully qualified path.", nameof(path));
        }

        var normalizedPath = MarketplaceSource.NormalizeDirectoryPath(path);

        if (!Directory.Exists(normalizedPath))
        {
            return normalizedPath;
        }

        try
        {
            var linkCount = 0;

            return ResolveExistingPath(normalizedPath, ref linkCount) ?? normalizedPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return normalizedPath;
        }
    }

    // Returns null when more than MaxLinkCount links are followed. Every resolved prefix is free of links, so a
    // relative link target is combined with it and its ".." segments are removed lexically, as realpath does.
    private static string? ResolveExistingPath(string normalizedPath, ref int linkCount)
    {
        var root = Path.GetPathRoot(normalizedPath)!;
        var current = root;

        foreach (var segment in normalizedPath[root.Length..]
                     .Split(DirectorySeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(current, segment);

            if (new DirectoryInfo(candidate).LinkTarget is { } linkTarget)
            {
                if (++linkCount > MaxLinkCount)
                {
                    return null;
                }

                var target = MarketplaceSource.NormalizeDirectoryPath(Path.Combine(current, linkTarget));

                if (ResolveExistingPath(target, ref linkCount) is not { } resolvedTarget)
                {
                    return null;
                }

                candidate = resolvedTarget;
            }

            current = candidate;
        }

        return Path.TrimEndingDirectorySeparator(current);
    }
}
