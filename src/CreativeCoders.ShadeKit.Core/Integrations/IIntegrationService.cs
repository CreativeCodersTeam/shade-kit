using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Installs, updates, and reads the SHADE kit integrations of all supported agents from the marketplace source that
/// fits the running engine.
/// </summary>
/// <remarks>
/// A release engine <c>x.y.z</c> targets <c>CreativeCodersTeam/shade-kit#vx.y.z</c> if that tag exists or cannot be
/// checked, and <c>CreativeCodersTeam/shade-kit#main</c> with an
/// <see cref="Warnings.IntegrationReleaseTagMissingWarning" /> if it does not exist; a prerelease engine targets
/// <c>CreativeCodersTeam/shade-kit#main</c> with an <see cref="Warnings.IntegrationPrereleaseRefWarning" />. The
/// environment variable <see cref="IntegrationService.MarketplaceSourceVariableName" /> replaces the whole source, as
/// described for <see cref="MarketplaceSource" />. Nothing is written into a repository.
/// </remarks>
public interface IIntegrationService
{
    /// <summary>
    /// Gets the ids of the supported agents.
    /// </summary>
    /// <value>The agent ids in the order in which the agents are registered, for example <c>claude</c>.</value>
    IReadOnlyList<string> AgentIds { get; }

    /// <summary>
    /// Installs the SHADE kit integration for the specified agents, or for every detected agent, and updates it where
    /// it is installed.
    /// </summary>
    /// <param name="agentIds">
    /// The ids of the agents to install for, compared case-insensitively; <see langword="null" /> or an empty
    /// collection installs for every agent that is detected on this computer.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the targeted source, the result of
    /// each agent, and the warnings.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="agentIds" /> contains an id that is not supported.</exception>
    /// <exception cref="InvalidMarketplaceSourceException">
    /// The environment variable <see cref="IntegrationService.MarketplaceSourceVariableName" /> cannot be used.
    /// </exception>
    /// <exception cref="NoAgentFoundException">No agent was named and none is detected.</exception>
    /// <exception cref="IntegrationConflictException">
    /// An agent knows the marketplace of SHADE kit with a different source; nothing was changed for that agent.
    /// </exception>
    /// <exception cref="IntegrationIncompleteException">
    /// An agent command failed after the marketplace was switched to another branch or tag.
    /// </exception>
    /// <exception cref="IntegrationRunFailedException">
    /// An agent failed after other agents were already done; the exception keeps their results, and its
    /// <see cref="Exception.InnerException" /> is the failure of the agent.
    /// </exception>
    /// <exception cref="IntegrationSwitchCanceledException">
    /// <paramref name="cancellationToken" /> was canceled while the marketplace of an agent was switched to another
    /// branch or tag.
    /// </exception>
    /// <exception cref="ExternalToolUnavailableException">
    /// The CLI of an agent is missing, cannot be started, or fails, for example a
    /// <see cref="ClaudeNotFoundException" /> for a named agent that is not detected; a named agent that is not
    /// detected fails before the release tag is checked.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<IntegrationRunResult> InstallAsync(IReadOnlyCollection<string>? agentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the SHADE kit integration of every detected agent in which it is installed for the user.
    /// </summary>
    /// <remarks>
    /// Each agent reads its installed integration once; the source is resolved (and the release tag checked) once,
    /// when the first agent with an installed integration needs it.
    /// </remarks>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the targeted source, the result of
    /// each updated agent, and the warnings. If no integration is installed, the source is <see langword="null" />,
    /// the list is empty, and there are no warnings: the source is resolved (and the release tag checked) only for an
    /// installed integration.
    /// </returns>
    /// <exception cref="InvalidMarketplaceSourceException">
    /// An integration is installed and the environment variable
    /// <see cref="IntegrationService.MarketplaceSourceVariableName" /> cannot be used.
    /// </exception>
    /// <exception cref="NoAgentFoundException">No agent is detected.</exception>
    /// <exception cref="IntegrationConflictException">
    /// An agent knows the marketplace of SHADE kit with a different source; nothing was changed for that agent.
    /// </exception>
    /// <exception cref="IntegrationIncompleteException">
    /// An agent command failed after the marketplace was switched to another branch or tag.
    /// </exception>
    /// <exception cref="IntegrationRunFailedException">
    /// An agent failed after other agents were already done; the exception keeps their results, and its
    /// <see cref="Exception.InnerException" /> is the failure of the agent.
    /// </exception>
    /// <exception cref="IntegrationSwitchCanceledException">
    /// <paramref name="cancellationToken" /> was canceled while the marketplace of an agent was switched to another
    /// branch or tag.
    /// </exception>
    /// <exception cref="ExternalToolUnavailableException">The CLI of an agent cannot be started or fails.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<IntegrationRunResult> UpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the SHADE kit integrations that are installed in the agents for the user.
    /// </summary>
    /// <remarks>
    /// Only agents whose CLI is detected are asked; an agent whose CLI is not detected is left out without a warning.
    /// An agent whose CLI is detected but cannot be started or fails is left out with an
    /// <see cref="Warnings.AgentUnavailableWarning" />, so this method fails only on cancellation.
    /// </remarks>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the installed integrations and the
    /// warnings.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<InstalledIntegrationsResult> GetInstalledAsync(CancellationToken cancellationToken = default);
}
