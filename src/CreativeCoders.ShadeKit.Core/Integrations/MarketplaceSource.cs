using System.Text.RegularExpressions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Versions;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Represents the source of the plugin marketplace from which an agent installs the SHADE kit integration: a GitHub
/// repository with an optional branch or tag, or a local directory.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="Repo" /> and <see cref="Path" /> is set. Create instances with
/// <see cref="ForGitHub" /> or <see cref="ForDirectory" />.
/// </remarks>
public sealed partial record MarketplaceSource
{
    private MarketplaceSource(string text, string? repo, string? @ref, string? path)
    {
        Text = text;
        Repo = repo;
        Ref = @ref;
        Path = path;
    }

    /// <summary>
    /// Gets the source as an agent CLI takes it, for example in <c>claude plugin marketplace add &lt;source&gt;</c>.
    /// </summary>
    /// <value>
    /// <c>owner/repo#ref</c> or <c>owner/repo</c> for a GitHub repository, or the absolute path of a local directory.
    /// </value>
    public string Text { get; }

    /// <summary>
    /// Gets the GitHub repository of the source.
    /// </summary>
    /// <value>
    /// The repository in the form <c>owner/repo</c>, or <see langword="null" /> if the source is a local directory.
    /// </value>
    public string? Repo { get; }

    /// <summary>
    /// Gets the branch or tag of the GitHub repository.
    /// </summary>
    /// <value>
    /// The branch or tag, for example <c>v0.8.0</c> or <c>main</c>, or <see langword="null" /> if the source is a
    /// local directory or names no branch or tag, which means the default branch of the repository.
    /// </value>
    public string? Ref { get; }

    /// <summary>
    /// Gets the absolute path of the local directory of the source.
    /// </summary>
    /// <value>
    /// The absolute path without a trailing directory separator, or <see langword="null" /> if the source is a GitHub
    /// repository.
    /// </value>
    public string? Path { get; }

    /// <summary>
    /// Gets the kit version that the source provides.
    /// </summary>
    /// <value>
    /// The SemVer version of a <see cref="Ref" /> of the form <c>v&lt;SemVer&gt;</c> without the <c>v</c>, for example
    /// <c>0.8.0</c> for <c>v0.8.0</c>, or <see langword="null" /> for any other ref and for a local directory.
    /// </value>
    public string? KitVersion => GetKitVersion(Ref);

    /// <summary>
    /// Creates the source of a GitHub repository with an optional branch or tag.
    /// </summary>
    /// <param name="repo">The repository in the form <c>owner/repo</c>, for example <c>CreativeCodersTeam/shade-kit</c>.</param>
    /// <param name="ref">
    /// The branch or tag, for example <c>v0.8.0</c>, or <see langword="null" /> for the default branch.
    /// </param>
    /// <returns>The source with the <see cref="Text" /> <c>owner/repo#ref</c>, or <c>owner/repo</c> without a ref.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="repo" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="repo" /> is not of the form <c>owner/repo</c>, or <paramref name="ref" /> is empty or contains
    /// whitespace or <c>#</c>.
    /// </exception>
    public static MarketplaceSource ForGitHub(string repo, string? @ref = null)
    {
        Ensure.NotNull(repo);

        if (!GitHubRepoRegex().IsMatch(repo))
        {
            throw new ArgumentException($"'{repo}' is not a GitHub repository of the form owner/repo.", nameof(repo));
        }

        if (@ref is not null && !GitHubRefRegex().IsMatch(@ref))
        {
            throw new ArgumentException($"'{@ref}' is not a branch or tag: it is empty or contains whitespace or '#'.",
                nameof(@ref));
        }

        return new MarketplaceSource(@ref is null ? repo : $"{repo}#{@ref}", repo, @ref, null);
    }

    /// <summary>
    /// Creates the source of a local directory.
    /// </summary>
    /// <param name="path">The absolute path of the directory.</param>
    /// <returns>The source with the normalized absolute path as <see cref="Text" /> and <see cref="Path" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="path" /> is not an absolute path.</exception>
    public static MarketplaceSource ForDirectory(string path)
    {
        Ensure.NotNull(path);

        if (!System.IO.Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"'{path}' is not an absolute path.", nameof(path));
        }

        var fullPath = NormalizeDirectoryPath(path);

        return new MarketplaceSource(fullPath, null, null, fullPath);
    }

    /// <summary>
    /// Parses a marketplace source that a developer set, for example in the environment variable
    /// <see cref="IntegrationService.MarketplaceSourceVariableName" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A URL is refused: a value that starts with a scheme of at least two characters and <c>://</c>, for example
    /// <c>https://</c>, <c>ssh://</c>, or <c>file://</c>, and an scp-like git URL such as
    /// <c>git@github.com:owner/repo</c>, whose first <c>:</c> follows at least two characters without <c>/</c> or
    /// <c>\</c>. A Windows drive path such as <c>C:\kit</c>, <c>C:/kit</c>, or <c>C://kit</c> has a single letter
    /// before the <c>:</c> and is no URL; on Windows it is a fully qualified path.
    /// </para>
    /// <para>
    /// A value whose part before the first <c>#</c> is no fully qualified path, does not start with <c>.</c> or
    /// <c>~</c>, and contains exactly one <c>/</c> and no <c>\</c> is a GitHub repository, <c>owner/repo#ref</c> or
    /// <c>owner/repo</c>; the ref may contain <c>/</c>, for example <c>feature/x</c>. Every other value
    /// is the path of a local directory; a relative path is made absolute against
    /// <paramref name="currentDirectory" />, so a relative directory with one <c>/</c> is written as
    /// <c>./dir/sub</c>. A <c>~</c> is not expanded.
    /// </para>
    /// </remarks>
    /// <param name="value">The value to parse.</param>
    /// <param name="currentDirectory">The absolute directory against which a relative path is resolved.</param>
    /// <returns>The parsed source.</returns>
    /// <exception cref="InvalidMarketplaceSourceException">
    /// <paramref name="value" /> is empty or whitespace, starts with <c>-</c>, is a URL, or looks like a GitHub
    /// repository but is not of the form <c>owner/repo#ref</c> or <c>owner/repo</c>.
    /// </exception>
    internal static MarketplaceSource Parse(string value, string currentDirectory)
    {
        Ensure.NotNull(value);
        Ensure.NotNull(currentDirectory);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidMarketplaceSourceException(value, "The value is empty.");
        }

        if (value.StartsWith('-'))
        {
            throw new InvalidMarketplaceSourceException(value,
                "The value starts with '-', so the agent CLI would read it as an option.");
        }

        if (UrlWithSchemeRegex().IsMatch(value))
        {
            throw new InvalidMarketplaceSourceException(value,
                "It is a URL, and SHADE kit takes no URL as a marketplace source; a GitHub repository is written " +
                "as owner/repo#ref or owner/repo.");
        }

        if (ScpLikeGitUrlRegex().IsMatch(value))
        {
            throw new InvalidMarketplaceSourceException(value,
                "It is an scp-like git URL (user@host:path), and SHADE kit takes no URL as a marketplace source; a " +
                "GitHub repository is written as owner/repo#ref or owner/repo.");
        }

        if (IsGitHubForm(value))
        {
            return ParseGitHub(value);
        }

        var path = System.IO.Path.IsPathFullyQualified(value)
            ? value
            : System.IO.Path.GetFullPath(value, currentDirectory);

        return ForDirectory(path);
    }

    /// <summary>
    /// Returns the kit version that the specified branch or tag provides.
    /// </summary>
    /// <param name="ref">The branch or tag, or <see langword="null" />.</param>
    /// <returns>
    /// The text after the <c>v</c> of a ref of the form <c>v&lt;SemVer&gt;</c>, for example <c>0.8.0</c>; otherwise,
    /// <see langword="null" />.
    /// </returns>
    internal static string? GetKitVersion(string? @ref)
    {
        return @ref is ['v', .. var version] && SemanticVersion.TryParse(version, out _)
            ? version
            : null;
    }

    /// <summary>
    /// Returns the specified absolute path in the normalized form of <see cref="Path" />.
    /// </summary>
    /// <remarks>Symbolic links are not resolved; see <see cref="DirectoryPathComparer" /> for the comparison.</remarks>
    /// <param name="path">The absolute path.</param>
    /// <returns>The full path without a trailing directory separator.</returns>
    internal static string NormalizeDirectoryPath(string path)
    {
        return System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
    }

    // Only the part before '#' decides, because a branch such as feature/x may contain '/'.
    private static bool IsGitHubForm(string value)
    {
        var repoPart = value.Split('#', 2)[0];

        return !System.IO.Path.IsPathFullyQualified(repoPart)
               && !repoPart.StartsWith('.')
               && !repoPart.StartsWith('~')
               && !repoPart.Contains('\\', StringComparison.Ordinal)
               && repoPart.Count(x => x == '/') == 1;
    }

    private static MarketplaceSource ParseGitHub(string value)
    {
        var parts = value.Split('#', 2);
        var repo = parts[0];
        var @ref = parts.Length == 2 ? parts[1] : null;

        if (!GitHubRepoRegex().IsMatch(repo))
        {
            throw new InvalidMarketplaceSourceException(value,
                $"'{repo}' is not a GitHub repository of the form owner/repo.");
        }

        if (@ref is not null && !GitHubRefRegex().IsMatch(@ref))
        {
            throw new InvalidMarketplaceSourceException(value,
                "The branch or tag after '#' is empty or contains whitespace or '#'.");
        }

        return ForGitHub(repo, @ref);
    }

    // GitHub owners consist of letters, digits, and hyphens; repository names also allow '.' and '_' but are not
    // '.' or '..'.
    [GeneratedRegex(@"\A[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?/(?!\.\.?\z)[A-Za-z0-9._-]+\z")]
    private static partial Regex GitHubRepoRegex();

    [GeneratedRegex(@"\A[^\s#]+\z")]
    private static partial Regex GitHubRefRegex();

    // A scheme in the syntax of RFC 3986, but of at least two characters, so that the drive path C://kit is no URL.
    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9+.-]+://")]
    private static partial Regex UrlWithSchemeRegex();

    // git's rule: a ':' before the first '/' makes a value an scp-like URL; '\\' counts as a separator too, and a
    // single letter before the ':' is a Windows drive.
    [GeneratedRegex(@"\A[^/\\:]{2,}:")]
    private static partial Regex ScpLikeGitUrlRegex();
}
