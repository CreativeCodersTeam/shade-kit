namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when an integration command needs an agent, but none of the supported
/// agents is detected on the computer.
/// </summary>
/// <param name="supportedAgentIds">The ids of the supported agents, for example <c>claude</c>.</param>
public sealed class NoAgentFoundException(IReadOnlyList<string> supportedAgentIds)
    : Exception($"None of the supported agents ({string.Join(", ", supportedAgentIds ?? [])}) was found.")
{
    /// <summary>
    /// Gets the ids of the supported agents.
    /// </summary>
    /// <value>A copy of the agent ids in the order in which the agents are registered.</value>
    public IReadOnlyList<string> SupportedAgentIds { get; } = [.. supportedAgentIds ?? []];
}
