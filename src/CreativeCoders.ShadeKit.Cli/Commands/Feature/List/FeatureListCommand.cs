using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.List;

/// <summary>
/// Lists all features in the SHADE kit store of the current repository.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="featureStore">The store that reads the features.</param>
[UsedImplicitly]
[CliCommand([FeatureCommandGroup.Name, Name], Description = "Lists all features of the SHADE kit store")]
public class FeatureListCommand(
    ShadeKitCommandContext context,
    IFeatureStore featureStore)
    : ICliCommand<FeatureListCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeatureCommandGroup" />.
    /// </summary>
    public const string Name = "list";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    /// <summary>
    /// Writes the id, status, profile, and title of every feature, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// A feature folder without <c>feature.json</c> is skipped with a warning. A store without features is a result,
    /// not an error.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, or a <c>feature.json</c> is invalid or too new.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(FeatureListCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) => _featureStore.ListAsync(store, cancellationToken))
            .ConfigureAwait(false);

        var data = new FeatureListCommandData(result.Features);

        _context.WriteSuccess(options.Json, data, result.Warnings, () => WriteFeatures(data));

        return CommandResult.Success;
    }

    private void WriteFeatures(FeatureListCommandData data)
    {
        if (data.Features.Count == 0)
        {
            _context.Console.WriteLine("No features found.");

            return;
        }

        foreach (var feature in data.Features)
        {
            _context.Console.WriteLine(
                $"{feature.Id}  {feature.Status.ToJsonName()}  {feature.Profile}  {feature.Title}");
        }
    }
}
