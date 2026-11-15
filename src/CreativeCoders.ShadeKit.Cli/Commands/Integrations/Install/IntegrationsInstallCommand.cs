using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Integrations;
using JetBrains.Annotations;

namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations.Install;

/// <summary>
/// Installs the SHADE kit integration for the user in the specified agent or in every detected agent, and updates it
/// where it is installed.
/// </summary>
/// <remarks>
/// The command works outside a git repository and writes nothing into the current repository.
/// </remarks>
/// <param name="context">The context that runs the install and writes the result or the failure.</param>
/// <param name="integrationService">The service that installs the integrations.</param>
[UsedImplicitly]
[CliCommand([IntegrationsCommandGroup.Name, Name],
    Description = "Installs the SHADE kit integration in the agents for the user")]
public class IntegrationsInstallCommand(
    ShadeKitCommandContext context,
    IIntegrationService integrationService)
    : ICliCommand<IntegrationsInstallCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="IntegrationsCommandGroup" />.
    /// </summary>
    public const string Name = "install";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IIntegrationService _integrationService = Ensure.NotNull(integrationService);

    /// <summary>
    /// Installs the integration and writes the result of each agent, as plain text or as a JSON envelope.
    /// </summary>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No agent is detected or the named agent is missing (exit code
    /// <see cref="ShadeKitCliExitCodes.AgentNotFound" />), an agent command cannot be run or fails (exit code
    /// <see cref="ShadeKitCliExitCodes.AgentCommandFailed" />), the agent knows the marketplace of SHADE kit with
    /// another source (exit code <see cref="ShadeKitCliExitCodes.IntegrationConflict" />), or the marketplace source
    /// override is invalid (exit code <see cref="ShadeKitCliExitCodes.InvalidArguments" />).
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(IntegrationsInstallCommandOptions options)
    {
        Ensure.NotNull(options);

        IReadOnlyCollection<string>? agentIds = options.Agent is { } agent ? [agent] : null;

        var result = await _context.RunAsync(options.Json,
                cancellationToken => _integrationService.InstallAsync(agentIds, cancellationToken))
            .ConfigureAwait(false);

        _context.WriteSuccess(options.Json, IntegrationRunData.From(result), result.Warnings,
            () => IntegrationsTextFormat.WriteRun(_context.Console, result));

        return CommandResult.Success;
    }
}
