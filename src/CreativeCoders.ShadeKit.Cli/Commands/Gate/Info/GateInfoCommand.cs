using System.Globalization;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Commands.Phase;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.SysConsole.Core;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Gate.Info;

/// <summary>
/// Shows everything a gate check needs about the gate into a phase of a feature: the feature, its phases with their
/// state and artifacts, the configured gate with the feature's checklist copy, and the coverage threshold.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="phaseWorkflow">The phase workflow that collects the gate data of the feature.</param>
[UsedImplicitly]
[CliCommand([GateCommandGroup.Name, Name], Description = "Shows the gate into a phase of a feature")]
public class GateInfoCommand(
    ShadeKitCommandContext context,
    IPhaseWorkflow phaseWorkflow)
    : ICliCommand<GateInfoCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="GateCommandGroup" />.
    /// </summary>
    public const string Name = "info";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IPhaseWorkflow _phaseWorkflow = Ensure.NotNull(phaseWorkflow);

    /// <summary>
    /// Reads the gate data of the feature for the phase and writes it, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// The command changes no file. Without <see cref="GateInfoCommandOptions.FeatureId" /> it reads the current
    /// feature. The first phase of the profile has no gate, which is written as <see langword="null" /> in JSON mode; a
    /// done feature is answered like an active one.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, the feature id is not valid, the feature does not exist, no current feature is
    /// resolved, <c>git</c> cannot be run, <c>feature.json</c>, <c>gates.json</c>, or <c>config.json</c> is invalid or
    /// uses an unsupported format version, the profile of the feature is unknown or its phase state is invalid, or the
    /// phase is not in the profile.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(GateInfoCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) =>
                    _phaseWorkflow.GetGateInfoAsync(store, options.Phase, options.FeatureId, cancellationToken))
            .ConfigureAwait(false);

        var data = GateInfoCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () => WriteResult(data));

        return CommandResult.Success;
    }

    private void WriteResult(GateInfoCommandData data)
    {
        _context.Console.MarkupLine(
            $"Feature: {data.FeatureId.ToEscapedMarkup().ToBoldMarkup()} (profile {data.Profile.ToEscapedMarkup()})");
        _context.Console.MarkupLine($"Feature directory: {data.FeatureDir.ToEscapedMarkup()}");
        _context.Console.WriteLine("Phases:");

        foreach (var phase in data.Phases)
        {
            _context.Console.MarkupLine($"  {PhaseTextFormat.FormatPhaseName(phase.Name)}: {phase.State.ToMarkup()}");
        }

        WriteGate(data.Gate);

        var threshold = data.CoverageThreshold is { } value
            ? value.ToString(CultureInfo.InvariantCulture).ToEscapedMarkup()
            : "not set".ToMutedMarkup();

        _context.Console.MarkupLine($"Coverage threshold: {threshold}");
    }

    private void WriteGate(GateInfoCommandGateData? gate)
    {
        if (gate is null)
        {
            _context.Console.MarkupLine($"Gate: {PhaseTextFormat.NoneMarkup} (first phase of the profile)");

            return;
        }

        _context.Console.MarkupLine(
            $"Gate: {PhaseTextFormat.FormatPhaseName(gate.Phase)} " +
            $"({PhaseTextFormat.FormatGate(gate.Approval, gate.Checklist)})");

        if (gate.ChecklistPath is null)
        {
            return;
        }

        var existence = gate.ChecklistExists == true ? "exists" : "missing".ToWarningMarkup();

        _context.Console.MarkupLine($"Checklist copy: {gate.ChecklistPath.ToEscapedMarkup()} ({existence})");
    }
}
