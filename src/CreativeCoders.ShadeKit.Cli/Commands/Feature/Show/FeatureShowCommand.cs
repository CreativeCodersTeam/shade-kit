using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.SysConsole.Core;
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

        _context.Console.MarkupLine($"Id: {data.Id.ToEscapedMarkup().ToBoldMarkup()}");
        _context.Console.MarkupLine($"Title: {data.Title.ToEscapedMarkup()}");
        _context.Console.MarkupLine($"Profile: {data.Profile.ToEscapedMarkup()}");
        _context.Console.MarkupLine($"Status: {data.Status.ToMarkup()}");
        _context.Console.MarkupLine($"Current phase: {data.CurrentPhase.ToEscapedMarkup().ToInfoMarkup()}");

        if (data.PendingTransition is not null)
        {
            _context.Console.MarkupLine(
                $"Pending transition: {data.PendingTransition.ToEscapedMarkup().ToWarningMarkup()}");
        }

        _context.Console.MarkupLine($"Source: {source.ToEscapedMarkup()}");
        _context.Console.MarkupLine($"Created: {created.ToEscapedMarkup()}");
        _context.Console.MarkupLine($"Feature directory: {data.Paths.FeatureDir.ToEscapedMarkup()}");
        _context.Console.MarkupLine($"Feature file: {data.Paths.FeatureFile.ToEscapedMarkup()}");
        _context.Console.MarkupLine($"Request: {data.Paths.Request.ToEscapedMarkup()}");

        WritePhases(data);
        WriteHistory(feature);
    }

    private void WritePhases(FeatureShowCommandData data)
    {
        _context.Console.WriteLine("Phases:");

        foreach (var phase in data.Phases)
        {
            _context.Console.MarkupLine($"  {phase.Name.ToEscapedMarkup().ToInfoMarkup()}: {phase.State.ToMarkup()}");
        }
    }

    // The entries are formatted from the stored document, so the plain text and a report use the same lines.
    private void WriteHistory(FeatureDocument feature)
    {
        if (feature.History is not { Count: > 0 } history)
        {
            _context.Console.MarkupLine($"History: {"none".ToMutedMarkup()}");

            return;
        }

        _context.Console.WriteLine("History:");

        foreach (var entry in history)
        {
            _context.Console.MarkupLine($"  {FeatureTextFormat.FormatHistoryEntry(entry).ToEscapedMarkup()}");
        }
    }
}
