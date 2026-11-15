namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Specifies the kind of the source of a plugin marketplace that an agent reports, independent of the agent.
/// </summary>
public enum AgentMarketplaceSourceKind
{
    /// <summary>
    /// The source is of a kind that SHADE kit does not compare, or the agent reported no kind.
    /// </summary>
    Other,

    /// <summary>
    /// The source is a GitHub repository with an optional branch or tag.
    /// </summary>
    GitHub,

    /// <summary>
    /// The source is a local directory.
    /// </summary>
    Directory
}
