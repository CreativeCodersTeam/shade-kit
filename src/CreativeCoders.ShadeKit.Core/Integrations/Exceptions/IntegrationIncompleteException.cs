namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when an agent command fails after the plugin marketplace of SHADE kit was
/// switched to another branch or tag, so the integration of the agent may be incomplete.
/// </summary>
/// <remarks>
/// The steps of an install are repeatable, so running <c>shade-kit integrations install</c> repairs the integration;
/// an update would not, because removing the marketplace also uninstalls its plugins. Nothing is rolled back. The
/// <see cref="Exception.InnerException" /> is the failure of the agent command, for example a
/// <see cref="ClaudeCommandFailedException" />.
/// </remarks>
/// <param name="agentId">The id of the agent, for example <c>claude</c>.</param>
/// <param name="innerException">The failure of the agent command.</param>
public sealed class IntegrationIncompleteException(string agentId, Exception innerException)
    : Exception($"The SHADE kit integration of the agent '{agentId}' may be incomplete: {innerException?.Message}",
        innerException)
{
    /// <summary>
    /// Gets the id of the agent whose integration may be incomplete.
    /// </summary>
    /// <value>The agent id, for example <c>claude</c>.</value>
    public string AgentId { get; } = agentId;
}
