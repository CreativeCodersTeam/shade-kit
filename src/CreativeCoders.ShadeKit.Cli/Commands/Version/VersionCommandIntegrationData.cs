using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Represents an installed SHADE kit integration in the <c>data</c> object that <c>shade-kit version --json</c>
/// returns.
/// </summary>
/// <param name="Agent">The id of the agent, for example <c>claude</c>.</param>
/// <param name="PluginId">
/// The id of the integration in the agent, for example <c>shade-kit@creativecoders-shade-kit</c>.
/// </param>
/// <param name="KitVersion">
/// The installed kit version, for example <c>0.8.0</c>, or <see langword="null" /> if the marketplace ref is no
/// <c>v&lt;SemVer&gt;</c> tag, the marketplace is a local directory, or the agent no longer knows the marketplace.
/// </param>
/// <param name="Ref">
/// The branch or tag of the marketplace, for example <c>v0.8.0</c>, or <see langword="null" /> for a local directory,
/// the default branch, or a marketplace that the agent no longer knows.
/// </param>
/// <param name="Enabled">
/// <see langword="true" /> if the integration is enabled in the agent; otherwise, <see langword="false" />.
/// </param>
public sealed record VersionCommandIntegrationData(
    string Agent,
    string PluginId,
    string? KitVersion,
    string? Ref,
    bool Enabled)
{
    /// <summary>
    /// Creates the data of the specified installed integration.
    /// </summary>
    /// <param name="integration">The installed integration.</param>
    /// <returns>The data with the values of <paramref name="integration" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="integration" /> is <see langword="null" />.</exception>
    public static VersionCommandIntegrationData From(InstalledIntegration integration)
    {
        Ensure.NotNull(integration);

        return new VersionCommandIntegrationData(integration.AgentId, integration.PluginId, integration.KitVersion,
            integration.Ref, integration.Enabled);
    }
}
