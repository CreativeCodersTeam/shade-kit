namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Specifies what an install or update does with the plugin marketplace of SHADE kit in an agent.
/// </summary>
internal enum MarketplaceChange
{
    /// <summary>
    /// The agent does not know the marketplace, so it is added with the needed source.
    /// </summary>
    Add,

    /// <summary>
    /// The marketplace has the needed source and is kept.
    /// </summary>
    Keep,

    /// <summary>
    /// The marketplace has the needed GitHub repository but another ref, so it is removed and added again with the
    /// needed source.
    /// </summary>
    Switch,

    /// <summary>
    /// The marketplace has the needed GitHub repository, but the agent does not report its ref
    /// (<see cref="AgentMarketplace.IsRefReported" /> is <see langword="false" />), so the agent must check whether
    /// the marketplace has the ref of the needed source, and keep or switch it accordingly.
    /// </summary>
    Verify
}
