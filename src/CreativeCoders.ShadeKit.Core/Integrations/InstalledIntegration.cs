namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Represents the SHADE kit integration that is installed in an agent for the user.
/// </summary>
/// <param name="AgentId">The id of the agent, for example <c>claude</c>.</param>
/// <param name="PluginId">
/// The id of the integration in the agent, for example <c>shade-kit@creativecoders-shade-kit</c>.
/// </param>
/// <param name="KitVersion">
/// The installed kit version, taken from the <c>v&lt;SemVer&gt;</c> ref of the marketplace, or
/// <see langword="null" /> if the ref is no such tag, the marketplace is a local directory, or the agent no longer
/// knows the marketplace.
/// </param>
/// <param name="Ref">
/// The branch or tag of the marketplace, or <see langword="null" /> for a local directory, the default branch, or a
/// marketplace that the agent no longer knows.
/// </param>
/// <param name="Enabled">
/// <see langword="true" /> if the integration is enabled in the agent; otherwise, <see langword="false" />.
/// </param>
public sealed record InstalledIntegration(
    string AgentId,
    string PluginId,
    string? KitVersion,
    string? Ref,
    bool Enabled);
