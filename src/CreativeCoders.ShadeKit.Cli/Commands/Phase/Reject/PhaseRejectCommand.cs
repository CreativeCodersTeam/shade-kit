using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Workflow;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Reject;

/// <summary>
/// Rejects the pending transition of the current feature, after the human said no.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="phaseWorkflow">The workflow that applies the rejection to the current feature.</param>
[UsedImplicitly]
[CliCommand([PhaseCommandGroup.Name, Name], Description = "Rejects the pending transition of the current feature")]
public class PhaseRejectCommand(
    ShadeKitCommandContext context,
    IPhaseWorkflow phaseWorkflow)
    : ICliCommand<PhaseRejectCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="PhaseCommandGroup" />.
    /// </summary>
    public const string Name = "reject";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IPhaseWorkflow _phaseWorkflow = Ensure.NotNull(phaseWorkflow);

    /// <summary>
    /// Rejects the pending transition to the phase, records it with the note in the history, and writes the current
    /// phase, which is in progress again, as plain text or as a JSON envelope.
    /// </summary>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, no current feature is resolved, <c>git</c> cannot be run, the
    /// <c>feature.json</c> of the current feature is missing, invalid, too new, or too old, names an unknown workflow
    /// profile, or holds an invalid phase state, the feature is done, the phase is not in its profile, no transition to
    /// the phase is pending, or the git user is not configured.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(PhaseRejectCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) =>
                    _phaseWorkflow.RejectAsync(store, options.Phase, options.Note, cancellationToken))
            .ConfigureAwait(false);

        var data = PhaseRejectCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () =>
        {
            _context.Console.WriteLine($"Feature: {data.FeatureId}");
            _context.Console.WriteLine($"Rejected: {PhaseTextFormat.FormatTransition(data.Rejected)}");
            _context.Console.WriteLine($"Current phase: {PhaseTextFormat.FormatPhase(data.CurrentPhase)}");
            _context.Console.WriteLine($"Note: {data.Note ?? PhaseTextFormat.None}");
        });

        return CommandResult.Success;
    }
}
