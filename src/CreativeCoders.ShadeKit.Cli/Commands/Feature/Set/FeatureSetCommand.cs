using System.Diagnostics.CodeAnalysis;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using JetBrains.Annotations;
using Spectre.Console;

[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace",
    Target = "~N:CreativeCoders.ShadeKit.Cli.Commands.Feature.Set",
    Justification = "The namespace follows the command folder Commands/<Group>/<Command>/; the CLI assembly is "
                    + "an application and is not consumed from Visual Basic.")]

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Set;

/// <summary>
/// Selects the current feature of the current repository through the local override.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="featureStore">The store that writes the local override.</param>
[UsedImplicitly]
[CliCommand([FeatureCommandGroup.Name, Name], Description = "Selects the current feature through the local override")]
public class FeatureSetCommand(
    ShadeKitCommandContext context,
    IFeatureStore featureStore)
    : ICliCommand<FeatureSetCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeatureCommandGroup" />.
    /// </summary>
    public const string Name = "set";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    /// <summary>
    /// Writes the local override <c>features/current-feature.json</c> and reports the selected feature, as plain text
    /// or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// If the current branch selects another feature, the override is still written with a
    /// <c>current-feature-mismatch</c> warning, because the branch wins. An unusable existing override is replaced.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found, the id is not valid, the feature does not exist, or <c>git</c> cannot be run.
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(FeatureSetCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) =>
                    _featureStore.SetCurrentAsync(store, options.FeatureId, cancellationToken))
            .ConfigureAwait(false);

        var data = new FeatureSetCommandData(result.FeatureId);

        _context.WriteSuccess(options.Json, data, result.Warnings,
            () => _context.Console.WriteLine($"Current feature: {data.Id}."));

        return CommandResult.Success;
    }
}
