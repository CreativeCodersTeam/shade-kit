using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Integrations;
using JetBrains.Annotations;

namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations.Update;

/// <summary>
/// Updates the SHADE kit integration in every detected agent in which it is installed for the user.
/// </summary>
/// <remarks>
/// The command works outside a git repository and writes nothing into the current repository.
/// </remarks>
/// <param name="context">The context that runs the update and writes the result or the failure.</param>
/// <param name="integrationService">The service that updates the integrations.</param>
[UsedImplicitly]
[CliCommand([IntegrationsCommandGroup.Name, Name],
    Description = "Updates the installed SHADE kit integrations to the version of this engine")]
public class IntegrationsUpdateCommand(
    ShadeKitCommandContext context,
    IIntegrationService integrationService)
    : ICliCommand<IntegrationsUpdateCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="IntegrationsCommandGroup" />.
    /// </summary>
    public const string Name = "update";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IIntegrationService _integrationService = Ensure.NotNull(integrationService);

    /// <summary>
    /// Updates the installed integrations and writes the result of each agent, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// No installed integration is a result, not an error: the envelope has an empty <c>integrations</c> list, and
    /// the plain output says that nothing is installed.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No agent is detected (exit code <see cref="ShadeKitCliExitCodes.AgentNotFound" />), an agent command cannot be
    /// run or fails (exit code <see cref="ShadeKitCliExitCodes.AgentCommandFailed" />), the agent knows the
    /// marketplace of SHADE kit with another source (exit code
    /// <see cref="ShadeKitCliExitCodes.IntegrationConflict" />), or the marketplace source override is invalid (exit
    /// code <see cref="ShadeKitCliExitCodes.InvalidArguments" />).
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(IntegrationsUpdateCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                cancellationToken => _integrationService.UpdateAsync(cancellationToken))
            .ConfigureAwait(false);

        _context.WriteSuccess(options.Json, IntegrationRunData.From(result), result.Warnings,
            () => IntegrationsTextFormat.WriteRun(_context.Console, result));

        return CommandResult.Success;
    }
}
