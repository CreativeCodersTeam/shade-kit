using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Installs, updates, and reads the SHADE kit integration of one agent, such as Claude Code, through the CLI of the
/// agent.
/// </summary>
/// <remarks>
/// An implementation installs the integration for the user, never into a repository. Its steps are repeatable, so a
/// failed run is repaired by running it again. A missing or failing agent CLI is reported as an
/// <see cref="ExternalToolUnavailableException" />.
/// </remarks>
public interface IAgentIntegration
{
    /// <summary>
    /// Gets the id of the agent.
    /// </summary>
    /// <value>A lowercase id, for example <c>claude</c>, which <c>shade-kit integrations install --agent</c> takes.</value>
    string AgentId { get; }

    /// <summary>
    /// Returns a value that indicates whether the agent is detected on this computer.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> if the CLI of the agent is found; otherwise, <see langword="false" />.
    /// </returns>
    bool IsAvailable();

    /// <summary>
    /// Throws the exception of the agent for a missing CLI if the agent is not detected on this computer.
    /// </summary>
    /// <remarks>
    /// Lets a caller report a named agent that is missing before it does anything else, for example before a network
    /// call.
    /// </remarks>
    /// <exception cref="ExternalToolUnavailableException">
    /// The CLI of the agent is not found, for example a <see cref="ClaudeNotFoundException" />.
    /// </exception>
    void EnsureAvailable();

    /// <summary>
    /// Reads the SHADE kit integration that is installed in the agent for the user.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the installed integration, or
    /// <see langword="null" /> if none is installed for the user.
    /// </returns>
    /// <exception cref="ExternalToolUnavailableException">The agent CLI is missing, cannot be started, or fails.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<InstalledIntegration?> GetInstalledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs the SHADE kit integration from the specified source for the user, or updates it if it is installed.
    /// </summary>
    /// <param name="source">The marketplace source to install from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains what was done.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="IntegrationConflictException">
    /// The agent knows the marketplace of SHADE kit with a different source; nothing was changed.
    /// </exception>
    /// <exception cref="IntegrationIncompleteException">
    /// An agent command failed after the marketplace was switched to another branch or tag.
    /// </exception>
    /// <exception cref="IntegrationSwitchCanceledException">
    /// <paramref name="cancellationToken" /> was canceled while the marketplace was switched to another branch or tag.
    /// </exception>
    /// <exception cref="ExternalToolUnavailableException">The agent CLI is missing, cannot be started, or fails.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<IntegrationResult> InstallAsync(MarketplaceSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the SHADE kit integration if it is installed for the user, from the source that the specified function
    /// returns.
    /// </summary>
    /// <remarks>
    /// <paramref name="getSource" /> is called once, and only if the integration is installed, before anything is
    /// changed; so a run in which nothing is installed resolves no source.
    /// </remarks>
    /// <param name="getSource">
    /// The function that returns the marketplace source to update from; it receives
    /// <paramref name="cancellationToken" />.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains what was done, or
    /// <see langword="null" /> if the integration is not installed for the user, in which case nothing was changed.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="getSource" /> is <see langword="null" /> or returns <see langword="null" />.
    /// </exception>
    /// <exception cref="IntegrationConflictException">
    /// The agent knows the marketplace of SHADE kit with a different source; nothing was changed.
    /// </exception>
    /// <exception cref="IntegrationIncompleteException">
    /// An agent command failed after the marketplace was switched to another branch or tag.
    /// </exception>
    /// <exception cref="IntegrationSwitchCanceledException">
    /// <paramref name="cancellationToken" /> was canceled while the marketplace was switched to another branch or tag.
    /// </exception>
    /// <exception cref="ExternalToolUnavailableException">The agent CLI is missing, cannot be started, or fails.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed. Any exception that
    /// <paramref name="getSource" /> throws escapes unchanged.
    /// </exception>
    Task<IntegrationResult?> UpdateAsync(Func<CancellationToken, Task<MarketplaceSource>> getSource,
        CancellationToken cancellationToken = default);
}
