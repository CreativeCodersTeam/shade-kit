namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Represents a plugin marketplace as an agent reports it, independent of the agent.
/// </summary>
/// <remarks>
/// Each agent integration maps the marketplace record of its agent CLI to this record, so that SHADE kit decides by
/// the same rules for every agent whether the marketplace is added, kept, switched, verified, or in conflict.
/// </remarks>
/// <param name="Name">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
/// <param name="SourceKind">One of the enumeration values that specifies the kind of the source.</param>
/// <param name="ReportedSourceKind">
/// The kind of the source in the words of the agent, for example <c>github</c> or <c>directory</c>, or
/// <see langword="null" /> if the agent reported none; a conflict message names it.
/// </param>
/// <param name="Repo">
/// The GitHub repository of a <see cref="AgentMarketplaceSourceKind.GitHub" /> source in the form <c>owner/repo</c>,
/// or <see langword="null" /> if the agent reported none.
/// </param>
/// <param name="Ref">
/// The branch or tag of a <see cref="AgentMarketplaceSourceKind.GitHub" /> source, or <see langword="null" /> if the
/// agent reported none, which means the default branch of the repository.
/// </param>
/// <param name="Path">
/// The path of a <see cref="AgentMarketplaceSourceKind.Directory" /> source, or <see langword="null" /> if the agent
/// reported none.
/// </param>
/// <param name="IsRefReported">
/// <see langword="true" /> if the agent reports the ref of a marketplace, so that a <see langword="null" />
/// <paramref name="Ref" /> means the default branch; <see langword="false" /> if the agent never reports it, so that
/// the ref of the marketplace is unknown and <paramref name="Ref" /> is <see langword="null" />. The default is
/// <see langword="true" />.
/// </param>
public sealed record AgentMarketplace(
    string Name,
    AgentMarketplaceSourceKind SourceKind,
    string? ReportedSourceKind,
    string? Repo,
    string? Ref,
    string? Path,
    bool IsRefReported = true)
{
    /// <summary>
    /// Gets the branch or tag of the marketplace if its source is a GitHub repository.
    /// </summary>
    /// <value>
    /// The <see cref="Ref" /> of a <see cref="AgentMarketplaceSourceKind.GitHub" /> source, or
    /// <see langword="null" /> for every other kind of source or a GitHub source without a ref.
    /// </value>
    public string? GitHubRef => SourceKind == AgentMarketplaceSourceKind.GitHub ? Ref : null;

    /// <summary>
    /// Gets the kit version that the marketplace provides.
    /// </summary>
    /// <value>
    /// The SemVer version of a <see cref="GitHubRef" /> of the form <c>v&lt;SemVer&gt;</c> without the <c>v</c>, for
    /// example <c>0.8.0</c> for <c>v0.8.0</c>, or <see langword="null" /> for any other ref and every other kind of
    /// source.
    /// </value>
    public string? KitVersion => MarketplaceSource.GetKitVersion(GitHubRef);
}
