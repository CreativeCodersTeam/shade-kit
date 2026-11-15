using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Integrations.Warnings;

/// <summary>
/// Represents a warning that the integration of an agent cannot be read because the agent CLI is detected but cannot
/// be started or fails, so the agent is left out of the installed integrations.
/// </summary>
/// <param name="AgentId">The id of the agent, for example <c>claude</c>.</param>
/// <param name="Reason">
/// A sentence that describes why the agent CLI cannot be used, for example the message of the failed command.
/// </param>
public sealed record AgentUnavailableWarning(string AgentId, string Reason) : KitWarning;
