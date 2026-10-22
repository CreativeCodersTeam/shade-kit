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
    /// If the current branch selects a different feature, the override is still written and a
    /// <c>current-feature-mismatch</c> warning is reported, because the branch wins when the current feature is
    /// resolved. Plain output is one line <c>Warning: &lt;message&gt;</c> per warning and the line
    /// <c>Current feature: &lt;id&gt;.</c>. JSON output is an envelope whose <c>data</c> is a
    /// <see cref="FeatureSetCommandData" />. In JSON mode, an expected failure is written as an error envelope and the
    /// abort message is not printed.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found (exit codes <see cref="ShadeKitCliExitCodes.NotAGitRepository" />,
    /// <see cref="ShadeKitCliExitCodes.KitRootNotFound" />, or <see cref="ShadeKitCliExitCodes.KitRootNotADirectory" />);
    /// the id is not valid (exit code <see cref="ShadeKitCliExitCodes.InvalidFeatureId" />); the feature does not exist
    /// (exit code <see cref="ShadeKitCliExitCodes.FeatureNotFound" />); or <c>git</c> cannot be run (exit code
    /// <see cref="ShadeKitCliExitCodes.GitUnavailable" />). An existing override that is invalid, outdated, or too new is
    /// replaced.
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
