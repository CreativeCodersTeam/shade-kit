using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Workflow;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Skills;

/// <summary>
/// Lists the skills of a phase of the workflow profile of the current feature.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="phaseWorkflow">The workflow that reads the skills of the phase.</param>
[UsedImplicitly]
[CliCommand([PhaseCommandGroup.Name, Name], Description = "Lists the skills of a phase of the current feature")]
public class PhaseSkillsCommand(
    ShadeKitCommandContext context,
    IPhaseWorkflow phaseWorkflow)
    : ICliCommand<PhaseSkillsCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="PhaseCommandGroup" />.
    /// </summary>
    public const string Name = "skills";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IPhaseWorkflow _phaseWorkflow = Ensure.NotNull(phaseWorkflow);

    /// <summary>
    /// Reads the phase skill and the general skills of the phase and writes them, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>The command changes nothing and also works for a done feature.</remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, no current feature is resolved, <c>git</c> cannot be run, the
    /// <c>feature.json</c> of the current feature is missing, invalid, or too new, names an unknown workflow profile,
    /// or holds an invalid phase state, or the phase is not in its profile.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(PhaseSkillsCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) => _phaseWorkflow.GetSkillsAsync(store, options.Phase, cancellationToken))
            .ConfigureAwait(false);

        var data = PhaseSkillsCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () =>
        {
            _context.Console.WriteLine($"Feature: {data.FeatureId} ({data.Profile})");
            _context.Console.WriteLine($"Phase: {data.Phase}");
            _context.Console.WriteLine($"Phase skill: {data.PhaseSkill}");
            _context.Console.WriteLine($"General skills: {PhaseTextFormat.FormatSkills(data.GeneralSkills)}");
        });

        return CommandResult.Success;
    }
}
