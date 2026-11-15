using System.Text.RegularExpressions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Represents a plugin marketplace that the OpenAI Codex CLI knows, as <c>codex plugin marketplace list --json</c>
/// reports it.
/// </summary>
/// <remarks>
/// <para>
/// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>, <c>codex-rs/cli/src/marketplace_cmd.rs</c>,
/// <c>JsonMarketplaceListEntry</c>, and <c>codex-rs/cli/src/plugin_cmd.rs</c>, <c>JsonMarketplaceSource</c>): the
/// output is one JSON object <c>{ marketplaces: [...] }</c> with the entries
/// <c>{ name, root, marketplaceSource? }</c>, and <c>marketplaceSource</c> is
/// <c>{ sourceType, source }</c>. <c>marketplaceSource</c> is missing for a marketplace that is not configured in
/// <c>config.toml</c>, for example a bundled one.
/// </para>
/// <para>
/// FACT (same tag, <c>codex-rs/core-plugins/src/marketplace_add/source.rs</c> and
/// <c>marketplace_add/metadata.rs</c>): <c>codex plugin marketplace add</c> stores a GitHub shorthand
/// <c>owner/repo</c> as the source type <see cref="GitSourceType" /> with the source
/// <c>https://github.com/owner/repo.git</c>, and a local directory as <see cref="LocalSourceType" /> with its
/// canonical absolute path. The configured ref is stored in <c>config.toml</c> but is not part of the JSON output, so
/// SHADE kit cannot read it; <see cref="ToAgentMarketplace" /> therefore reports no ref.
/// </para>
/// </remarks>
/// <param name="Name">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
/// <param name="Root">The directory that holds the marketplace.</param>
/// <param name="SourceType">
/// The type of the configured source, <see cref="GitSourceType" /> or <see cref="LocalSourceType" />, or
/// <see langword="null" /> if <c>codex</c> reported no <c>marketplaceSource</c>.
/// </param>
/// <param name="Source">
/// The configured source, a Git URL or an absolute path, or <see langword="null" /> if <c>codex</c> reported no
/// <c>marketplaceSource</c>.
/// </param>
public sealed partial record CodexMarketplace(string Name, string Root, string? SourceType, string? Source)
{
    /// <summary>
    /// Represents the source type of a marketplace that <c>codex</c> clones from a Git repository.
    /// </summary>
    public const string GitSourceType = "git";

    /// <summary>
    /// Represents the source type of a marketplace in a local directory.
    /// </summary>
    public const string LocalSourceType = "local";

    private const string WindowsVerbatimPrefix = @"\\?\";

    private const string WindowsVerbatimUncPrefix = @"\\?\UNC\";

    /// <summary>
    /// Returns the marketplace in the agent-neutral form.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="GitSourceType" /> source of the form <c>https://github.com/owner/repo.git</c> is a
    /// <see cref="AgentMarketplaceSourceKind.GitHub" /> source with the repository <c>owner/repo</c> and no ref,
    /// because <c>codex</c> does not report the ref. Any other Git URL, for example an SSH URL or another host, is
    /// <see cref="AgentMarketplaceSourceKind.Other" />.
    /// </para>
    /// <para>
    /// A <see cref="LocalSourceType" /> source is a <see cref="AgentMarketplaceSourceKind.Directory" /> source with the
    /// reported path. A Windows extended-length prefix (<c>\\?\C:\…</c> or <c>\\?\UNC\server\share\…</c>) is removed,
    /// because <c>codex</c> stores the result of Rust's <c>canonicalize</c>, which has that prefix on Windows.
    /// Unverified: that a real <c>codex</c> on Windows reports such a path; it has not been run there.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The marketplace with the kind <see cref="AgentMarketplaceSourceKind.GitHub" />,
    /// <see cref="AgentMarketplaceSourceKind.Directory" />, or <see cref="AgentMarketplaceSourceKind.Other" />; the
    /// source types are compared case-sensitively, and the reported source type is kept as the reported kind.
    /// </returns>
    internal AgentMarketplace ToAgentMarketplace()
    {
        return SourceType switch
        {
            GitSourceType when Source is not null && GitHubUrlRegex().Match(Source) is { Success: true } match =>
                new AgentMarketplace(Name, AgentMarketplaceSourceKind.GitHub, SourceType, match.Groups["repo"].Value,
                    null, null),
            LocalSourceType when Source is not null =>
                new AgentMarketplace(Name, AgentMarketplaceSourceKind.Directory, SourceType, null, null,
                    RemoveWindowsVerbatimPrefix(Source)),
            _ => new AgentMarketplace(Name, AgentMarketplaceSourceKind.Other, SourceType, null, null, null)
        };
    }

    private static string RemoveWindowsVerbatimPrefix(string path)
    {
        if (path.StartsWith(WindowsVerbatimUncPrefix, StringComparison.Ordinal))
        {
            return @"\\" + path[WindowsVerbatimUncPrefix.Length..];
        }

        return path.StartsWith(WindowsVerbatimPrefix, StringComparison.Ordinal)
            ? path[WindowsVerbatimPrefix.Length..]
            : path;
    }

    // The URL that codex makes of the shorthand owner/repo; the segments allow the characters of its shorthand check.
    [GeneratedRegex(@"\Ahttps://github\.com/(?<repo>[A-Za-z0-9._-]+/[A-Za-z0-9._-]+)\.git\z")]
    private static partial Regex GitHubUrlRegex();
}
