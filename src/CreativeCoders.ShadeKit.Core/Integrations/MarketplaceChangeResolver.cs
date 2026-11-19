using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Decides for one agent what an install or update does with the plugin marketplace
/// <see cref="ShadeKitPlugin.MarketplaceName" /> that the agent reports.
/// </summary>
/// <remarks>
/// A missing marketplace is added. A marketplace with the same GitHub repository (compared case-insensitively, as
/// GitHub does) and the same ref, or with the same directory, is kept; directories are compared with a
/// <see cref="DirectoryPathComparer" />, and only a fully qualified reported path can name the directory of the
/// source. A marketplace with the same repository but another ref is switched. A marketplace with the same repository
/// whose ref the agent does not report (<see cref="AgentMarketplace.IsRefReported" /> is <see langword="false" />) is
/// verified: the resolver cannot compare the refs, so the agent checks them itself. A marketplace with another
/// repository, another kind of source, or another directory is a conflict. Refs are compared case-sensitively, as git
/// does, and a GitHub marketplace without a ref (the default branch) has another ref than every source with a ref.
/// </remarks>
internal sealed class MarketplaceChangeResolver
{
    private readonly string _agentId;

    private readonly IReadOnlyList<string> _resolveCommands;

    private readonly DirectoryPathComparer _directoryPathComparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketplaceChangeResolver" /> class.
    /// </summary>
    /// <param name="agentId">The id of the agent, for example <c>claude</c>.</param>
    /// <param name="resolveCommands">
    /// The commands of the agent, in the order to run them, that remove the marketplace, which a conflict names, for
    /// example only <c>claude plugin marketplace remove creativecoders-shade-kit</c>.
    /// </param>
    /// <param name="directoryPathComparer">
    /// The comparer that decides whether a directory marketplace has the directory of the source.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    public MarketplaceChangeResolver(string agentId, IReadOnlyList<string> resolveCommands,
        DirectoryPathComparer directoryPathComparer)
    {
        _agentId = Ensure.NotNull(agentId);
        _resolveCommands = [.. Ensure.NotNull(resolveCommands)];
        _directoryPathComparer = Ensure.NotNull(directoryPathComparer);
    }

    /// <summary>
    /// Returns what an install or update does with the reported marketplace to reach the specified source.
    /// </summary>
    /// <param name="marketplace">
    /// The marketplace <see cref="ShadeKitPlugin.MarketplaceName" /> as the agent reports it, or
    /// <see langword="null" /> if the agent does not know it.
    /// </param>
    /// <param name="source">The source that SHADE kit needs.</param>
    /// <returns>
    /// <see cref="MarketplaceChange.Add" /> if <paramref name="marketplace" /> is <see langword="null" />,
    /// <see cref="MarketplaceChange.Keep" /> if it has the source, <see cref="MarketplaceChange.Switch" /> if it has
    /// the repository of the source with another ref, or <see cref="MarketplaceChange.Verify" /> if it has the
    /// repository of the source and the agent does not report its ref.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="IntegrationConflictException">
    /// <paramref name="marketplace" /> has another repository, another kind of source, or another directory than
    /// <paramref name="source" />; the exception names the reported source and the commands of the agent that remove
    /// the marketplace.
    /// </exception>
    public MarketplaceChange Resolve(AgentMarketplace? marketplace, MarketplaceSource source)
    {
        Ensure.NotNull(source);

        if (marketplace is null)
        {
            return MarketplaceChange.Add;
        }

        if (source.Repo is { } repo)
        {
            if (marketplace.SourceKind == AgentMarketplaceSourceKind.GitHub
                && string.Equals(marketplace.Repo, repo, StringComparison.OrdinalIgnoreCase))
            {
                if (!marketplace.IsRefReported)
                {
                    return MarketplaceChange.Verify;
                }

                return string.Equals(marketplace.Ref, source.Ref, StringComparison.Ordinal)
                    ? MarketplaceChange.Keep
                    : MarketplaceChange.Switch;
            }
        }
        else if (marketplace.SourceKind == AgentMarketplaceSourceKind.Directory
                 && marketplace.Path is { } path
                 && IsSameDirectory(path, source.Path))
        {
            return MarketplaceChange.Keep;
        }

        throw new IntegrationConflictException(_agentId, ShadeKitPlugin.MarketplaceName, DescribeSource(marketplace),
            source.Text, _resolveCommands);
    }

    /// <summary>
    /// Returns the source of a marketplace in the form that a conflict message names.
    /// </summary>
    /// <param name="marketplace">The marketplace as the agent reports it.</param>
    /// <returns>
    /// <c>owner/repo#ref</c> or <c>owner/repo</c> for a GitHub source with a repository, the path for a directory
    /// source with a path; otherwise, a text that names the reported kind of the source, or says that it is unknown.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="marketplace" /> is <see langword="null" />.</exception>
    internal static string DescribeSource(AgentMarketplace marketplace)
    {
        Ensure.NotNull(marketplace);

        return marketplace switch
        {
            { SourceKind: AgentMarketplaceSourceKind.GitHub, Repo: { } repo } =>
                marketplace.Ref is null ? repo : $"{repo}#{marketplace.Ref}",
            { SourceKind: AgentMarketplaceSourceKind.Directory, Path: { } path } => path,
            { ReportedSourceKind: { } sourceKind } => $"a source of the kind '{sourceKind}'",
            _ => "a source of unknown kind"
        };
    }

    // Agents report absolute paths; any other text names no directory that the source can have.
    private bool IsSameDirectory(string marketplacePath, string? sourcePath)
    {
        return sourcePath is not null
               && Path.IsPathFullyQualified(marketplacePath)
               && _directoryPathComparer.AreSameDirectory(marketplacePath, sourcePath);
    }
}
