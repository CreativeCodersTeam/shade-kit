namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a run is canceled while the plugin marketplace of SHADE kit is
/// switched to another branch or tag, so the integration of the agent may be incomplete.
/// </summary>
/// <remarks>
/// The exception is an <see cref="OperationCanceledException" /> with the token of the canceled run, so it is handled
/// as a cancellation. Removing the marketplace also uninstalls its plugins, so running
/// <c>shade-kit integrations install</c> repairs the integration. The <see cref="Exception.InnerException" /> is the
/// original cancellation.
/// </remarks>
/// <param name="agentId">The id of the agent, for example <c>claude</c>.</param>
/// <param name="innerException">The cancellation that stopped the switch.</param>
public sealed class IntegrationSwitchCanceledException(string agentId, OperationCanceledException innerException)
    : OperationCanceledException(
        $"The SHADE kit integration of the agent '{agentId}' was canceled while its plugin marketplace was switched, " +
        "so it may be incomplete.",
        innerException,
        innerException?.CancellationToken ?? CancellationToken.None)
{
    /// <summary>
    /// Gets the id of the agent whose integration may be incomplete.
    /// </summary>
    /// <value>The agent id, for example <c>claude</c>.</value>
    public string AgentId { get; } = agentId;
}
