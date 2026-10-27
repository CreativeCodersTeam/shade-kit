using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Workflow;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Complete;

/// <summary>
/// Completes the last phase of the current feature and sets the feature to done.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="phaseWorkflow">The workflow that completes the current feature.</param>
[UsedImplicitly]
[CliCommand([PhaseCommandGroup.Name, Name], Description = "Completes the last phase of the current feature")]
public class PhaseCompleteCommand(
    ShadeKitCommandContext context,
    IPhaseWorkflow phaseWorkflow)
    : ICliCommand<PhaseCompleteCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="PhaseCommandGroup" />.
    /// </summary>
    public const string Name = "complete";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IPhaseWorkflow _phaseWorkflow = Ensure.NotNull(phaseWorkflow);

    /// <summary>
    /// Approves the last phase, sets the feature to done, records it in the history, clears the local override if it
    /// selects the feature, and writes the result, as plain text or as a JSON envelope.
    /// </summary>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, no current feature is resolved, <c>git</c> cannot be run, the
    /// <c>feature.json</c> of the current feature is missing, invalid, too new, or too old, names an unknown workflow
    /// profile, or holds an invalid phase state, the feature is done, the current phase is not the last phase of the
    /// profile or not in progress, or the git user is not configured.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(PhaseCompleteCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) => _phaseWorkflow.CompleteAsync(store, cancellationToken))
            .ConfigureAwait(false);

        var data = PhaseCompleteCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () =>
        {
            _context.Console.WriteLine($"Feature: {data.FeatureId}");
            _context.Console.WriteLine($"Completed phase: {data.Phase}");
            _context.Console.WriteLine($"Status: {data.Status.ToJsonName()}");

            if (data.OverrideCleared)
            {
                _context.Console.WriteLine("The local override pointed to this feature and was cleared.");
            }
        });

        return CommandResult.Success;
    }
}
