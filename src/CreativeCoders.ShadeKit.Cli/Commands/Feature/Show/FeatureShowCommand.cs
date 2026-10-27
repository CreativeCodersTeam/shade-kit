using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;

/// <summary>
/// Shows the complete state of a feature of the SHADE kit store of the current repository.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="featureStore">The store that reads the feature.</param>
[UsedImplicitly]
[CliCommand([FeatureCommandGroup.Name, Name], Description = "Shows the complete state of a feature")]
public class FeatureShowCommand(
    ShadeKitCommandContext context,
    IFeatureStore featureStore)
    : ICliCommand<FeatureShowCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeatureCommandGroup" />.
    /// </summary>
    public const string Name = "show";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    /// <summary>
    /// Reads the feature and writes its state, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// The phase state is normalized against the workflow profile, so a feature without phase fields shows the
    /// initial state of its profile; <c>feature.json</c> is not changed. The history is written as it is stored.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, the id is not valid, the feature does not exist, or its <c>feature.json</c> is
    /// missing, invalid, or too new, names an unknown workflow profile, or holds an invalid phase state.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(FeatureShowCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) => _featureStore.ShowAsync(store, options.FeatureId, cancellationToken))
            .ConfigureAwait(false);

        var data = FeatureShowCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () => WriteFeature(data, result.Feature));

        return CommandResult.Success;
    }

    private void WriteFeature(FeatureShowCommandData data, FeatureDocument feature)
    {
        var source = FeatureTextFormat.FormatSource(feature.Source);
        var created = FeatureTextFormat.FormatCreated(feature);

        _context.Console.WriteLine($"Id: {data.Id}");
        _context.Console.WriteLine($"Title: {data.Title}");
        _context.Console.WriteLine($"Profile: {data.Profile}");
        _context.Console.WriteLine($"Status: {data.Status.ToJsonName()}");
        _context.Console.WriteLine($"Current phase: {data.CurrentPhase}");

        if (data.PendingTransition is not null)
        {
            _context.Console.WriteLine($"Pending transition: {data.PendingTransition}");
        }

        _context.Console.WriteLine($"Source: {source}");
        _context.Console.WriteLine($"Created: {created}");
        _context.Console.WriteLine($"Feature directory: {data.Paths.FeatureDir}");
        _context.Console.WriteLine($"Feature file: {data.Paths.FeatureFile}");
        _context.Console.WriteLine($"Request: {data.Paths.Request}");

        WritePhases(data);
        WriteHistory(feature);
    }

    private void WritePhases(FeatureShowCommandData data)
    {
        _context.Console.WriteLine("Phases:");

        foreach (var (phase, state) in data.Phases)
        {
            _context.Console.WriteLine($"  {phase}: {state.ToJsonName()}");
        }
    }

    // The entries are formatted from the stored document, so the plain text and a report use the same lines.
    private void WriteHistory(FeatureDocument feature)
    {
        if (feature.History is not { Count: > 0 } history)
        {
            _context.Console.WriteLine("History: none");

            return;
        }

        _context.Console.WriteLine("History:");

        foreach (var entry in history)
        {
            _context.Console.WriteLine($"  {FeatureTextFormat.FormatHistoryEntry(entry)}");
        }
    }
}
