using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Workflow;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Approve;

/// <summary>
/// Approves the pending transition of the current feature, after the human said yes.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="phaseWorkflow">The workflow that applies the approval to the current feature.</param>
[UsedImplicitly]
[CliCommand([PhaseCommandGroup.Name, Name], Description = "Approves the pending transition of the current feature")]
public class PhaseApproveCommand(
    ShadeKitCommandContext context,
    IPhaseWorkflow phaseWorkflow)
    : ICliCommand<PhaseApproveCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="PhaseCommandGroup" />.
    /// </summary>
    public const string Name = "approve";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IPhaseWorkflow _phaseWorkflow = Ensure.NotNull(phaseWorkflow);

    /// <summary>
    /// Approves the pending transition to the phase, records it in the history, and writes the new current phase, as
    /// plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// An approved reopen sets every phase after the reopened one to <c>pending</c>; artifact files are not touched.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, no current feature is resolved, <c>git</c> cannot be run, the
    /// <c>feature.json</c> of the current feature is missing, invalid, too new, or too old, names an unknown workflow
    /// profile, or holds an invalid phase state, the feature is done, the phase is not in its profile, no transition to
    /// the phase is pending, the profile no longer allows the pending reopen, or the git user is not configured.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(PhaseApproveCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) => _phaseWorkflow.ApproveAsync(store, options.Phase, cancellationToken))
            .ConfigureAwait(false);

        var data = PhaseApproveCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () =>
        {
            _context.Console.WriteLine($"Feature: {data.FeatureId}");
            _context.Console.WriteLine($"Approved: {PhaseTextFormat.FormatTransition(data.Transition)}");
            _context.Console.WriteLine($"Current phase: {PhaseTextFormat.FormatPhase(data.CurrentPhase)}");
        });

        return CommandResult.Success;
    }
}
