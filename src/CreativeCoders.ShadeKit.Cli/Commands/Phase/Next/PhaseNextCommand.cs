using System.Diagnostics.CodeAnalysis;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Workflow;
using JetBrains.Annotations;
using Spectre.Console;

[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace",
    Target = "~N:CreativeCoders.ShadeKit.Cli.Commands.Phase.Next",
    Justification = "The namespace follows the command folder Commands/<Group>/<Command>/; the CLI assembly is "
                    + "an application and is not consumed from Visual Basic.")]

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Next;

/// <summary>
/// Lists the possible next steps of the current feature.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="phaseWorkflow">The workflow that derives the next steps from the phase state.</param>
[UsedImplicitly]
[CliCommand([PhaseCommandGroup.Name, Name], Description = "Lists the possible next steps of the current feature")]
public class PhaseNextCommand(
    ShadeKitCommandContext context,
    IPhaseWorkflow phaseWorkflow)
    : ICliCommand<PhaseNextCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="PhaseCommandGroup" />.
    /// </summary>
    public const string Name = "next";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IPhaseWorkflow _phaseWorkflow = Ensure.NotNull(phaseWorkflow);

    /// <summary>
    /// Derives the possible next steps from the phase state of the current feature and writes them, as plain text or
    /// as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// The command changes nothing and also works for a done feature, for which it returns only the current state. In
    /// JSON mode a step that does not apply is written as <see langword="null" />.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, no current feature is resolved, <c>git</c> cannot be run, or the
    /// <c>feature.json</c> of the current feature is missing, invalid, or too new, names an unknown workflow profile,
    /// or holds an invalid phase state.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(PhaseNextCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) => _phaseWorkflow.GetNextAsync(store, cancellationToken))
            .ConfigureAwait(false);

        var data = PhaseNextCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () => WriteResult(data));

        return CommandResult.Success;
    }

    private void WriteResult(PhaseNextCommandData data)
    {
        var current = data.Current;

        _context.Console.WriteLine($"Feature: {current.FeatureId} ({current.Status.ToJsonName()})");
        _context.Console.WriteLine($"Current phase: {current.Phase} ({current.State.ToJsonName()})");

        if (data.Continue is { } continueStep)
        {
            _context.Console.WriteLine($"Continue: {FormatStep(continueStep)}");
        }

        if (data.Pending is { } pendingStep)
        {
            _context.Console.WriteLine($"Pending: {FormatGatedStep(pendingStep)}");
        }

        if (data.Forward is { } forwardStep)
        {
            _context.Console.WriteLine($"Forward: {FormatGatedStep(forwardStep)}");
        }

        WriteReopen(data.Reopen);
    }

    private void WriteReopen(IReadOnlyList<PhaseNextCommandStepData>? reopen)
    {
        switch (reopen)
        {
            case null:
                return;
            case []:
                _context.Console.WriteLine($"Reopen: {PhaseTextFormat.None}");

                return;
        }

        _context.Console.WriteLine("Reopen:");

        foreach (var step in reopen)
        {
            _context.Console.WriteLine($"  {FormatStep(step)}");
        }
    }

    private static string FormatStep(PhaseNextCommandStepData step)
    {
        return $"{step.Phase} (skill: {step.Skill})";
    }

    private static string FormatGatedStep(PhaseNextCommandGatedStepData step)
    {
        var gate = PhaseTextFormat.FormatGate(step.Gate.Approval, step.Gate.Checklist);

        return $"{step.Phase} (skill: {step.Skill}, {gate})";
    }
}
