using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations;

namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations;

/// <summary>
/// Represents the result of one agent in the <c>data</c> object that <c>shade-kit integrations install --json</c> and
/// <c>shade-kit integrations update --json</c> return.
/// </summary>
/// <param name="Agent">The id of the agent, for example <c>claude</c>.</param>
/// <param name="PluginId">
/// The id of the integration in the agent, for example <c>shade-kit@creativecoders-shade-kit</c>.
/// </param>
/// <param name="Outcome">
/// One of the enumeration values that specifies what was done, written as <c>installed</c>, <c>updated</c>, or
/// <c>current</c>.
/// </param>
/// <param name="PreviousKitVersion">
/// The kit version that was installed before, or <see langword="null" /> if the integration was not installed or its
/// ref was no <c>v&lt;SemVer&gt;</c> tag.
/// </param>
/// <param name="KitVersion">
/// The kit version that is installed now, or <see langword="null" /> if the ref is no <c>v&lt;SemVer&gt;</c> tag or
/// the source is a local directory.
/// </param>
/// <param name="Ref">
/// The branch or tag that is installed now, or <see langword="null" /> for a local directory or the default branch.
/// </param>
/// <param name="Enabled">
/// <see langword="true" /> if the integration is enabled in the agent; otherwise, <see langword="false" />.
/// </param>
public sealed record IntegrationResultData(
    string Agent,
    string PluginId,
    IntegrationOutcome Outcome,
    string? PreviousKitVersion,
    string? KitVersion,
    string? Ref,
    bool Enabled)
{
    /// <summary>
    /// Creates the data of the specified result of an agent.
    /// </summary>
    /// <param name="result">The result of the agent.</param>
    /// <returns>The data with the values of <paramref name="result" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static IntegrationResultData From(IntegrationResult result)
    {
        Ensure.NotNull(result);

        return new IntegrationResultData(result.AgentId, result.PluginId, result.Outcome, result.PreviousKitVersion,
            result.KitVersion, result.Ref, result.Enabled);
    }
}
