using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Current;

/// <summary>
/// Resolves the current feature of the current repository from the branch name or the local override.
/// </summary>
/// <param name="ansiConsole">The console that receives the plain result.</param>
/// <param name="kitStoreFactory">The factory that locates the SHADE kit store.</param>
/// <param name="featureStore">The store that resolves the current feature.</param>
/// <param name="jsonWriter">The writer that receives the JSON output.</param>
/// <param name="errorMapper">The mapper that turns failures into exit codes, error codes, and messages.</param>
/// <param name="cancellation">The cancellation of the run, whose token the command passes to the core.</param>
[UsedImplicitly]
[CliCommand([FeatureCommandGroup.Name, Name], Description = "Shows the current feature")]
public class FeatureCurrentCommand(
    IAnsiConsole ansiConsole,
    IKitStoreFactory kitStoreFactory,
    IFeatureStore featureStore,
    ICliJsonWriter jsonWriter,
    CliErrorMapper errorMapper,
    ICliCancellation cancellation)
    : ICliCommand<FeatureCurrentCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeatureCommandGroup" />.
    /// </summary>
    public const string Name = "current";

    private readonly IAnsiConsole _ansiConsole = Ensure.NotNull(ansiConsole);

    private readonly IKitStoreFactory _kitStoreFactory = Ensure.NotNull(kitStoreFactory);

    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    private readonly ICliJsonWriter _jsonWriter = Ensure.NotNull(jsonWriter);

    private readonly CliErrorMapper _errorMapper = Ensure.NotNull(errorMapper);

    private readonly ICliCancellation _cancellation = Ensure.NotNull(cancellation);

    /// <summary>
    /// Resolves the current feature and writes it, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// The branch name wins over the local override; if both select different features, a
    /// <c>current-feature-mismatch</c> warning is reported. A feature that cannot be resolved is a result, not an
    /// error. Plain output is one line <c>Warning: &lt;message&gt;</c> per warning, then the lines
    /// <c>&lt;id&gt; (&lt;profile&gt;, &lt;status&gt;)</c> and <c>Resolved from: branch|override</c>, or the line
    /// <c>No current feature.</c> and a hint to run <c>shade-kit feature set &lt;id&gt;</c>. JSON output is an envelope
    /// whose <c>data</c> is a <see cref="FeatureCurrentCommandData" /> and whose <c>warnings</c> contain the format and
    /// feature warnings. In JSON mode, an expected failure is written as an error envelope and the abort message is not
    /// printed.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />,
    /// also when no current feature is resolved.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found (exit codes <see cref="ShadeKitCliExitCodes.NotAGitRepository" />,
    /// <see cref="ShadeKitCliExitCodes.KitRootNotFound" />, or <see cref="ShadeKitCliExitCodes.KitRootNotADirectory" />);
    /// <c>git</c> cannot be run (exit code <see cref="ShadeKitCliExitCodes.GitUnavailable" />); or a kit file is
    /// missing, invalid, or too new (exit codes <see cref="ShadeKitCliExitCodes.InvalidFile" /> and
    /// <see cref="ShadeKitCliExitCodes.FormatTooNew" />).
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(FeatureCurrentCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await CliCommandAbort.RunAsync(
                () => _featureStore.GetCurrentAsync(_kitStoreFactory.Create(), _cancellation.Token), _errorMapper,
                options.Json, _jsonWriter)
            .ConfigureAwait(false);

        var data = FeatureCurrentCommandData.FromResult(result);

        FeatureCommandOutput.Write(_ansiConsole, _jsonWriter, options.Json, data,
            FeatureCommandOutput.CreateWarnings(result.FormatWarnings, result.Warnings), () => WriteResult(data));

        return CommandResult.Success;
    }

    private void WriteResult(FeatureCurrentCommandData data)
    {
        if (data is not { Feature: { } feature, ResolvedFrom: { } resolvedFrom })
        {
            FeatureCommandOutput.WriteLine(_ansiConsole, "No current feature.");
            FeatureCommandOutput.WriteLine(_ansiConsole, "Run 'shade-kit feature set <id>' to select one.");

            return;
        }

        FeatureCommandOutput.WriteLine(_ansiConsole,
            $"{feature.Id} ({feature.Profile}, {CliJsonWriter.GetEnumName(feature.Status)})");
        FeatureCommandOutput.WriteLine(_ansiConsole, $"Resolved from: {CliJsonWriter.GetEnumName(resolvedFrom)}");
    }
}
