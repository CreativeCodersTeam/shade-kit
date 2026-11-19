namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Provides the names of the SHADE kit plugin and its plugin marketplace.
/// </summary>
/// <remarks>
/// The names come from the manifests <c>.claude-plugin/marketplace.json</c> and <c>.claude-plugin/plugin.json</c> of
/// the SHADE kit repository. Every supported agent reads these manifests, so the names are the same for every agent.
/// </remarks>
public static class ShadeKitPlugin
{
    /// <summary>
    /// Represents the name of the SHADE kit plugin.
    /// </summary>
    public const string Name = "shade-kit";

    /// <summary>
    /// Represents the name of the plugin marketplace of SHADE kit.
    /// </summary>
    public const string MarketplaceName = "creativecoders-shade-kit";

    /// <summary>
    /// Represents the id of the SHADE kit plugin in its marketplace, in the form <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>.
    /// </summary>
    public const string Id = Name + "@" + MarketplaceName;
}
