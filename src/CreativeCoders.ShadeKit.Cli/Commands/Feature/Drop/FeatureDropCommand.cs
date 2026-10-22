using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Drop;

/// <summary>
/// Deletes a feature folder of the SHADE kit store of the current repository and clears the local override if it
/// selects that feature.
/// </summary>
/// <param name="context">The context that runs the core operations and writes the result or the failure.</param>
/// <param name="featureStore">The store that describes and deletes the feature.</param>
/// <param name="confirmation">The console confirmation that asks the user in plain mode.</param>
[UsedImplicitly]
[CliCommand([FeatureCommandGroup.Name, Name], Description = "Deletes a feature and clears the override if it selects it")]
public class FeatureDropCommand(
    ShadeKitCommandContext context,
    IFeatureStore featureStore,
    IConsoleConfirmation confirmation)
    : ICliCommand<FeatureDropCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeatureCommandGroup" />.
    /// </summary>
    public const string Name = "drop";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    private readonly IConsoleConfirmation _confirmation = Ensure.NotNull(confirmation);

    /// <summary>
    /// Drops the feature, or describes what would be dropped, and writes the result as plain text or as a JSON
    /// envelope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With <see cref="FeatureDropCommandOptions.Yes" />, the feature folder is deleted at once, and the local override
    /// is deleted if it selects the feature. Plain output is <c>Dropped feature &lt;id&gt;.</c>, plus a line if the
    /// override was cleared. In JSON mode, <c>overrideAffected</c> tells whether the override selected the feature and
    /// <c>overrideCleared</c> whether it was deleted.
    /// </para>
    /// <para>
    /// Without <see cref="FeatureDropCommandOptions.Yes" />, JSON mode deletes nothing and returns the files that would
    /// be deleted with <c>dropped</c> and <c>overrideCleared</c> set to <see langword="false" />, and
    /// <c>overrideAffected</c> set to <see langword="true" /> if the override selects the feature and would be
    /// cleared. Plain mode writes the files that would be deleted and, if the override selects the feature, that it
    /// would be cleared. If the user can be asked (<see cref="IConsoleConfirmation.CanAsk" />), the command asks for a
    /// confirmation with the default No and deletes only after a yes; otherwise it writes a hint to run the command
    /// with <c>--yes</c> and returns without waiting for input. Either way, a run that deletes nothing writes
    /// <c>Nothing was dropped.</c> and succeeds.
    /// </para>
    /// <para>
    /// JSON output is an envelope whose <c>data</c> is a <see cref="FeatureDropCommandData" />. In JSON mode, an
    /// expected failure is written as an error envelope and the abort message is not printed.
    /// </para>
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />,
    /// also when nothing was dropped.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found (exit codes <see cref="ShadeKitCliExitCodes.NotAGitRepository" />,
    /// <see cref="ShadeKitCliExitCodes.KitRootNotFound" />, or <see cref="ShadeKitCliExitCodes.KitRootNotADirectory" />);
    /// the id is not valid (exit code <see cref="ShadeKitCliExitCodes.InvalidFeatureId" />); or the feature does not
    /// exist (exit code <see cref="ShadeKitCliExitCodes.FeatureNotFound" />).
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(FeatureDropCommandOptions options)
    {
        Ensure.NotNull(options);

        var store = _context.CreateKitStore(options.Json);

        if (options.Yes)
        {
            await DropAsync(store, options).ConfigureAwait(false);

            return CommandResult.Success;
        }

        var preview = await _context.RunAsync(options.Json,
                cancellationToken => _featureStore.PreviewDropAsync(store, options.FeatureId, cancellationToken))
            .ConfigureAwait(false);

        _context.WriteSuccess(options.Json,
            new FeatureDropCommandData(preview.FeatureId, false, preview.Files, preview.OverrideAffected, false),
            () => WritePreview(preview));

        if (options.Json)
        {
            return CommandResult.Success;
        }

        if (!_confirmation.CanAsk)
        {
            _context.Console.WriteLine(
                $"Nothing was dropped. Run 'shade-kit feature drop {preview.FeatureId} --yes' to drop it.");

            return CommandResult.Success;
        }

        if (!await _confirmation.ConfirmAsync($"Drop feature {preview.FeatureId}?", _context.CancellationToken)
                .ConfigureAwait(false))
        {
            _context.Console.WriteLine("Nothing was dropped.");

            return CommandResult.Success;
        }

        await DropAsync(store, options).ConfigureAwait(false);

        return CommandResult.Success;
    }

    private async Task DropAsync(IKitStore store, FeatureDropCommandOptions options)
    {
        var result = await _context.RunAsync(options.Json,
                cancellationToken => _featureStore.DropAsync(store, options.FeatureId, cancellationToken))
            .ConfigureAwait(false);

        _context.WriteSuccess(options.Json,
            new FeatureDropCommandData(result.FeatureId, true, result.Files, result.OverrideAffected,
                result.OverrideAffected),
            () => WriteDropped(result));
    }

    private void WriteDropped(FeatureDropResult result)
    {
        _context.Console.WriteLine($"Dropped feature {result.FeatureId}.");

        if (result.OverrideAffected)
        {
            _context.Console.WriteLine("The local override pointed to this feature and was cleared.");
        }
    }

    private void WritePreview(FeatureDropPreview preview)
    {
        _context.Console.WriteLine($"Feature {preview.FeatureId} would be dropped. Files:");

        foreach (var file in preview.Files)
        {
            _context.Console.WriteLine($"  {file}");
        }

        if (preview.OverrideAffected)
        {
            _context.Console.WriteLine("The local override points to this feature and would be cleared.");
        }
    }
}
