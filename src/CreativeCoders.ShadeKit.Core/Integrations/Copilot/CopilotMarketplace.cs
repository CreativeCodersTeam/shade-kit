namespace CreativeCoders.ShadeKit.Core.Integrations.Copilot;

/// <summary>
/// Represents a plugin marketplace that the GitHub Copilot CLI knows, as <c>copilot plugin marketplace list --json</c>
/// reports it.
/// </summary>
/// <remarks>
/// Unverified: the GitHub Docs do not document the entries of <c>copilot plugin marketplace list --json</c>. The
/// reader assumes the source kinds of the Claude Code marketplace settings, which the Copilot CLI also reads:
/// <see cref="GitHubSourceKind" /> with a repository and an optional ref, and <see cref="DirectorySourceKind" /> or
/// <see cref="LocalSourceKind" /> with a path. A marketplace of any other kind is compared as
/// <see cref="AgentMarketplaceSourceKind.Other" />, which makes an install a conflict that changes nothing.
/// </remarks>
/// <param name="Name">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
/// <param name="SourceKind">
/// The kind of the source of the marketplace, for example <see cref="GitHubSourceKind" /> or
/// <see cref="DirectorySourceKind" />, or <see langword="null" /> if <c>copilot</c> reported none.
/// </param>
/// <param name="Repo">
/// The GitHub repository of a <see cref="GitHubSourceKind" /> source in the form <c>owner/repo</c>, or
/// <see langword="null" /> if <c>copilot</c> reported none.
/// </param>
/// <param name="Ref">
/// The branch or tag of a <see cref="GitHubSourceKind" /> source, or <see langword="null" /> if <c>copilot</c>
/// reported none, which means the default branch of the repository.
/// </param>
/// <param name="Path">
/// The path of a local directory source, or <see langword="null" /> if <c>copilot</c> reported none.
/// </param>
public sealed record CopilotMarketplace(string Name, string? SourceKind, string? Repo, string? Ref, string? Path)
{
    /// <summary>
    /// Represents the source kind of a marketplace in a GitHub repository.
    /// </summary>
    public const string GitHubSourceKind = "github";

    /// <summary>
    /// Represents the source kind of a marketplace in a local directory, as Claude Code names it.
    /// </summary>
    public const string DirectorySourceKind = "directory";

    /// <summary>
    /// Represents another possible source kind of a marketplace in a local directory.
    /// </summary>
    /// <remarks>Unverified: the GitHub Docs call such a source a "local path"; the kind text is assumed.</remarks>
    public const string LocalSourceKind = "local";

    /// <summary>
    /// Returns the marketplace in the agent-neutral form.
    /// </summary>
    /// <returns>
    /// The marketplace with the kind <see cref="AgentMarketplaceSourceKind.GitHub" /> for
    /// <see cref="GitHubSourceKind" />, <see cref="AgentMarketplaceSourceKind.Directory" /> for
    /// <see cref="DirectorySourceKind" /> and <see cref="LocalSourceKind" />, and
    /// <see cref="AgentMarketplaceSourceKind.Other" /> for every other kind; the kinds are compared case-sensitively,
    /// and the reported kind and all other values are taken over unchanged.
    /// </returns>
    internal AgentMarketplace ToAgentMarketplace()
    {
        var sourceKind = SourceKind switch
        {
            GitHubSourceKind => AgentMarketplaceSourceKind.GitHub,
            DirectorySourceKind or LocalSourceKind => AgentMarketplaceSourceKind.Directory,
            _ => AgentMarketplaceSourceKind.Other
        };

        return new AgentMarketplace(Name, sourceKind, SourceKind, Repo, Ref, Path);
    }
}
