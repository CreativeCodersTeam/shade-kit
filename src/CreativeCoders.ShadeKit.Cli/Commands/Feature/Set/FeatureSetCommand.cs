using System.Diagnostics.CodeAnalysis;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
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
/// <param name="ansiConsole">The console that receives the plain result.</param>
/// <param name="kitStoreFactory">The factory that locates the SHADE kit store.</param>
/// <param name="featureStore">The store that writes the local override.</param>
/// <param name="jsonWriter">The writer that receives the JSON output.</param>
/// <param name="errorMapper">The mapper that turns failures into exit codes, error codes, and messages.</param>
/// <param name="cancellation">The cancellation of the run, whose token the command passes to the core.</param>
[UsedImplicitly]
[CliCommand([FeatureCommandGroup.Name, Name], Description = "Selects the current feature through the local override")]
public class FeatureSetCommand(
    IAnsiConsole ansiConsole,
    IKitStoreFactory kitStoreFactory,
    IFeatureStore featureStore,
    ICliJsonWriter jsonWriter,
    CliErrorMapper errorMapper,
    ICliCancellation cancellation)
    : ICliCommand<FeatureSetCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeatureCommandGroup" />.
    /// </summary>
    public const string Name = "set";

    private readonly IAnsiConsole _ansiConsole = Ensure.NotNull(ansiConsole);

    private readonly IKitStoreFactory _kitStoreFactory = Ensure.NotNull(kitStoreFactory);

    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    private readonly ICliJsonWriter _jsonWriter = Ensure.NotNull(jsonWriter);

    private readonly CliErrorMapper _errorMapper = Ensure.NotNull(errorMapper);

    private readonly ICliCancellation _cancellation = Ensure.NotNull(cancellation);

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
    /// (exit code <see cref="ShadeKitCliExitCodes.FeatureNotFound" />); <c>git</c> cannot be run (exit code
    /// <see cref="ShadeKitCliExitCodes.GitUnavailable" />); or the existing override is too new to be replaced (exit
    /// codes <see cref="ShadeKitCliExitCodes.FormatTooNew" /> and <see cref="ShadeKitCliExitCodes.UpgradeNeeded" />).
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(FeatureSetCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await CliCommandAbort.RunAsync(
                () => _featureStore.SetCurrentAsync(_kitStoreFactory.Create(), options.FeatureId, _cancellation.Token),
                _errorMapper, options.Json, _jsonWriter)
            .ConfigureAwait(false);

        var data = new FeatureSetCommandData(result.FeatureId);

        FeatureCommandOutput.Write(_ansiConsole, _jsonWriter, options.Json, data,
            FeatureCommandOutput.CreateWarnings([], result.Warnings),
            () => FeatureCommandOutput.WriteLine(_ansiConsole, $"Current feature: {data.Id}."));

        return CommandResult.Success;
    }
}
