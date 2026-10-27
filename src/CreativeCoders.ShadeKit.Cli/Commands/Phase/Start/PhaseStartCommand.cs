using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Start;

/// <summary>
/// Starts a phase of the current feature: requests the transition to the next phase or the reopen of an earlier
/// phase, or re-enters the current phase.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="phaseWorkflow">The workflow that applies the request to the current feature.</param>
[UsedImplicitly]
[CliCommand([PhaseCommandGroup.Name, Name], Description = "Starts a phase of the current feature")]
public class PhaseStartCommand(
    ShadeKitCommandContext context,
    IPhaseWorkflow phaseWorkflow)
    : ICliCommand<PhaseStartCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="PhaseCommandGroup" />.
    /// </summary>
    public const string Name = "start";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IPhaseWorkflow _phaseWorkflow = Ensure.NotNull(phaseWorkflow);

    /// <summary>
    /// Applies the request to start the phase and writes the outcome with the gate and the skills to load, as plain
    /// text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// A repeated request for a pending transition, a re-entry of the current phase, and a resume of a phase whose
    /// transition is pending change nothing and do not write <c>feature.json</c>.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, no current feature is resolved, <c>git</c> cannot be run, the
    /// <c>feature.json</c> of the current feature is missing, invalid, too new, or too old, names an unknown workflow
    /// profile, or holds an invalid phase state, the feature is done, the phase is not in its profile, the profile
    /// does not allow the transition, or the git user is not configured.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(PhaseStartCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) => _phaseWorkflow.StartAsync(store, options.Phase, cancellationToken))
            .ConfigureAwait(false);

        var data = PhaseStartCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () => WriteResult(data));

        return CommandResult.Success;
    }

    private void WriteResult(PhaseStartCommandData data)
    {
        var transition = data.Transition is null
            ? PhaseTextFormat.None
            : PhaseTextFormat.FormatTransition(data.Transition);
        var gate = data.Gate is null
            ? PhaseTextFormat.None
            : $"{data.Gate.Phase} ({PhaseTextFormat.FormatGate(data.Gate.Approval, data.Gate.Checklist)})";

        _context.Console.WriteLine($"Feature: {data.FeatureId} ({data.Profile})");
        _context.Console.WriteLine($"Outcome: {data.Outcome.ToJsonName()}");
        _context.Console.WriteLine($"Transition: {transition}");
        _context.Console.WriteLine($"Current phase: {PhaseTextFormat.FormatPhase(data.CurrentPhase)}");
        _context.Console.WriteLine($"Gate: {gate}");
        _context.Console.WriteLine($"Phase skill: {data.Skills.PhaseSkill}");
        _context.Console.WriteLine($"General skills: {PhaseTextFormat.FormatSkills(data.Skills.GeneralSkills)}");

        if (data is { CurrentPhase.State: PhaseState.AwaitingApproval, Transition.To: { } pending })
        {
            _context.Console.WriteLine(
                $"Approve with 'shade-kit phase approve {pending}' or reject with 'shade-kit phase reject {pending}'.");
        }
    }
}
