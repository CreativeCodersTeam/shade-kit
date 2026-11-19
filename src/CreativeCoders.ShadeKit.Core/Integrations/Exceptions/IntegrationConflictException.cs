using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when an agent already knows the plugin marketplace of SHADE kit by its
/// name, but with a different source repository or directory; nothing is changed.
/// </summary>
/// <param name="agentId">The id of the agent, for example <c>claude</c>.</param>
/// <param name="marketplaceName">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
/// <param name="existingSource">
/// The source of the existing marketplace as the agent reports it, for example <c>other/repo#main</c> or the path of
/// a directory.
/// </param>
/// <param name="requestedSource">The source that SHADE kit needs, for example <c>CreativeCodersTeam/shade-kit#v0.8.0</c>.</param>
/// <param name="resolveCommands">
/// The commands, in the order to run them, that remove the existing marketplace, so that the next run can add it with
/// the needed source, for example only <c>claude plugin marketplace remove creativecoders-shade-kit</c>, or
/// <c>copilot plugin uninstall shade-kit@creativecoders-shade-kit</c> and then
/// <c>copilot plugin marketplace remove creativecoders-shade-kit</c> for an agent that removes no marketplace while
/// plugins of it are installed.
/// </param>
/// <exception cref="ArgumentNullException"><paramref name="resolveCommands" /> is <see langword="null" />.</exception>
public sealed class IntegrationConflictException(
    string agentId,
    string marketplaceName,
    string existingSource,
    string requestedSource,
    IReadOnlyList<string> resolveCommands)
    : Exception($"The plugin marketplace '{marketplaceName}' of the agent '{agentId}' has the source " +
                $"'{existingSource}', but SHADE kit needs '{requestedSource}'.")
{
    /// <summary>
    /// Gets the id of the agent whose marketplace conflicts.
    /// </summary>
    /// <value>The agent id, for example <c>claude</c>.</value>
    public string AgentId { get; } = agentId;

    /// <summary>
    /// Gets the name of the marketplace that conflicts.
    /// </summary>
    /// <value>The marketplace name, for example <c>creativecoders-shade-kit</c>.</value>
    public string MarketplaceName { get; } = marketplaceName;

    /// <summary>
    /// Gets the source of the existing marketplace.
    /// </summary>
    /// <value>The source as the agent reports it, for example <c>other/repo#main</c> or the path of a directory.</value>
    public string ExistingSource { get; } = existingSource;

    /// <summary>
    /// Gets the source that SHADE kit needs.
    /// </summary>
    /// <value>The source, for example <c>CreativeCodersTeam/shade-kit#v0.8.0</c>.</value>
    public string RequestedSource { get; } = requestedSource;

    /// <summary>
    /// Gets the commands that resolve the conflict.
    /// </summary>
    /// <value>
    /// The commands, in the order to run them, that remove the existing marketplace, for example only
    /// <c>claude plugin marketplace remove creativecoders-shade-kit</c>.
    /// </value>
    public IReadOnlyList<string> ResolveCommands { get; } = [.. Ensure.NotNull(resolveCommands)];
}
