using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Represents the result of a successful install or update of the SHADE kit integration of one agent.
/// </summary>
/// <param name="AgentId">The id of the agent, for example <c>claude</c>.</param>
/// <param name="PluginId">
/// The id of the integration in the agent, for example <c>shade-kit@creativecoders-shade-kit</c>.
/// </param>
/// <param name="Outcome">One of the enumeration values that specifies what was done.</param>
/// <param name="PreviousKitVersion">
/// The kit version that was installed before, taken from the <c>v&lt;SemVer&gt;</c> ref of the marketplace before the
/// change, or <see langword="null" /> if the integration was not installed or its ref is no such tag.
/// </param>
/// <param name="KitVersion">
/// The kit version that is installed now, taken from the <c>v&lt;SemVer&gt;</c> ref of the source, or
/// <see langword="null" /> if the ref is no such tag or the source is a local directory.
/// </param>
/// <param name="Ref">
/// The branch or tag that is installed now, or <see langword="null" /> for a local directory or the default branch.
/// </param>
/// <param name="Enabled">
/// <see langword="true" /> if the integration is enabled in the agent; otherwise, <see langword="false" />. An update
/// does not enable a disabled integration.
/// </param>
public sealed record IntegrationResult(
    string AgentId,
    string PluginId,
    IntegrationOutcome Outcome,
    string? PreviousKitVersion,
    string? KitVersion,
    string? Ref,
    bool Enabled)
{
    /// <summary>
    /// Creates the result of an install or update of the SHADE kit plugin <see cref="ShadeKitPlugin.Id" /> from the
    /// specified source.
    /// </summary>
    /// <param name="agentId">The id of the agent, for example <c>claude</c>.</param>
    /// <param name="source">The marketplace source that is installed now.</param>
    /// <param name="outcome">One of the enumeration values that specifies what was done.</param>
    /// <param name="previousKitVersion">
    /// The kit version that was installed before, or <see langword="null" /> if the integration was not installed or
    /// its ref is no <c>v&lt;SemVer&gt;</c> tag.
    /// </param>
    /// <param name="enabled">
    /// <see langword="true" /> if the plugin is enabled in the agent; otherwise, <see langword="false" />.
    /// </param>
    /// <returns>
    /// The result with the plugin id <see cref="ShadeKitPlugin.Id" /> and the kit version and ref of
    /// <paramref name="source" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="agentId" /> or <paramref name="source" /> is <see langword="null" />.
    /// </exception>
    internal static IntegrationResult ForShadeKitPlugin(string agentId, MarketplaceSource source,
        IntegrationOutcome outcome, string? previousKitVersion, bool enabled)
    {
        Ensure.NotNull(agentId);
        Ensure.NotNull(source);

        return new IntegrationResult(agentId, ShadeKitPlugin.Id, outcome, previousKitVersion, source.KitVersion,
            source.Ref, enabled);
    }
}
