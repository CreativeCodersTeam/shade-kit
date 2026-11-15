namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the integration of one agent fails in a run over several agents after
/// the integrations of other agents were already installed or updated.
/// </summary>
/// <remarks>
/// The exception keeps the results of the agents that were done before the failure, so they can still be reported.
/// The <see cref="Exception.InnerException" /> is the failure of the agent, for example a
/// <see cref="ClaudeCommandFailedException" /> or an <see cref="IntegrationIncompleteException" />. A run whose first
/// agent fails, and a canceled run, report the failure without this exception.
/// </remarks>
/// <param name="failedAgentId">The id of the agent whose integration failed.</param>
/// <param name="source">The marketplace source that the run targeted.</param>
/// <param name="completedIntegrations">
/// The results of the agents that were installed or updated before the failure, in the order of the run.
/// </param>
/// <param name="innerException">The failure of the agent.</param>
public sealed class IntegrationRunFailedException(
    string failedAgentId,
    MarketplaceSource source,
    IReadOnlyList<IntegrationResult> completedIntegrations,
    Exception innerException)
    : Exception(
        $"The SHADE kit integration of the agent '{failedAgentId}' failed after the integrations of " +
        $"{string.Join(", ", (completedIntegrations ?? []).Select(x => $"'{x.AgentId}'"))} were done: " +
        $"{innerException?.Message}",
        innerException)
{
    /// <summary>
    /// Gets the id of the agent whose integration failed.
    /// </summary>
    /// <value>The agent id, for example <c>claude</c>.</value>
    public string FailedAgentId { get; } = failedAgentId;

    /// <summary>
    /// Gets the marketplace source that the run targeted.
    /// </summary>
    /// <value>The source of the completed integrations.</value>
    public MarketplaceSource MarketplaceSource { get; } = source;

    /// <summary>
    /// Gets the results of the agents that were installed or updated before the failure.
    /// </summary>
    /// <value>A copy of the results in the order of the run.</value>
    public IReadOnlyList<IntegrationResult> CompletedIntegrations { get; } = [.. completedIntegrations ?? []];
}
