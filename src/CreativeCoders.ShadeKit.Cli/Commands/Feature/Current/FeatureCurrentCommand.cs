using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Current;

/// <summary>
/// Resolves the current feature of the current repository from the branch name or the local override.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="featureStore">The store that resolves the current feature.</param>
[UsedImplicitly]
[CliCommand([FeatureCommandGroup.Name, Name], Description = "Shows the current feature")]
public class FeatureCurrentCommand(
    ShadeKitCommandContext context,
    IFeatureStore featureStore)
    : ICliCommand<FeatureCurrentCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeatureCommandGroup" />.
    /// </summary>
    public const string Name = "current";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    /// <summary>
    /// Resolves the current feature and writes it, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// The branch wins over the local override. A feature that cannot be resolved is a result, not an error. The
    /// current phase and its state are normalized against the workflow profile; <c>feature.json</c> is not changed.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />,
    /// also when no current feature is resolved.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, <c>git</c> cannot be run, a kit file is missing, invalid, or too new, or the
    /// <c>feature.json</c> of the current feature names an unknown workflow profile or holds an invalid phase state.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(FeatureCurrentCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) => _featureStore.GetCurrentAsync(store, cancellationToken))
            .ConfigureAwait(false);

        var data = FeatureCurrentCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () => WriteResult(data));

        return CommandResult.Success;
    }

    private void WriteResult(FeatureCurrentCommandData data)
    {
        if (data is not { Feature: { } feature, ResolvedFrom: { } resolvedFrom })
        {
            _context.Console.WriteLine("No current feature.");
            _context.Console.WriteLine("Run 'shade-kit feature set <id>' to select one.");

            return;
        }

        _context.Console.WriteLine($"{feature.Id} ({feature.Profile}, {feature.Status.ToJsonName()})");
        _context.Console.WriteLine($"Current phase: {feature.CurrentPhase} ({feature.PhaseState.ToJsonName()})");
        _context.Console.WriteLine($"Resolved from: {resolvedFrom.ToJsonName()}");
    }
}
