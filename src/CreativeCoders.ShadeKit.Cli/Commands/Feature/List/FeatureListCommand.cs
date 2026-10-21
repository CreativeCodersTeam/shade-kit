using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.List;

/// <summary>
/// Lists all features in the SHADE kit store of the current repository.
/// </summary>
/// <param name="ansiConsole">The console that receives the plain feature list.</param>
/// <param name="kitStoreFactory">The factory that locates the SHADE kit store.</param>
/// <param name="featureStore">The store that reads the features.</param>
/// <param name="jsonWriter">The writer that receives the JSON output.</param>
/// <param name="errorMapper">The mapper that turns store failures into exit codes, error codes, and messages.</param>
/// <param name="cancellation">The cancellation of the run, whose token the command passes to the core.</param>
[UsedImplicitly]
[CliCommand([FeatureCommandGroup.Name, Name], Description = "Lists all features of the SHADE kit store")]
public class FeatureListCommand(
    IAnsiConsole ansiConsole,
    IKitStoreFactory kitStoreFactory,
    IFeatureStore featureStore,
    ICliJsonWriter jsonWriter,
    CliErrorMapper errorMapper,
    ICliCancellation cancellation)
    : ICliCommand<FeatureListCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeatureCommandGroup" />.
    /// </summary>
    public const string Name = "list";

    private readonly IAnsiConsole _ansiConsole = Ensure.NotNull(ansiConsole);

    private readonly IKitStoreFactory _kitStoreFactory = Ensure.NotNull(kitStoreFactory);

    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    private readonly ICliJsonWriter _jsonWriter = Ensure.NotNull(jsonWriter);

    private readonly CliErrorMapper _errorMapper = Ensure.NotNull(errorMapper);

    private readonly ICliCancellation _cancellation = Ensure.NotNull(cancellation);

    /// <summary>
    /// Writes the id, status, profile, and title of every feature of the SHADE kit store in ordinal order of the ids,
    /// as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// The store is the <c>.shadekit</c> directory at the root of the git repository that contains the current
    /// directory. A feature folder without <c>feature.json</c> is skipped with a <c>feature-file-missing</c> warning.
    /// Plain output is one line <c>Warning: &lt;message&gt;</c> per warning, then one line
    /// <c>&lt;id&gt;  &lt;status&gt;  &lt;profile&gt;  &lt;title&gt;</c> per feature, or a hint if the store has no
    /// features. JSON output is an envelope whose <c>data</c> is a <see cref="FeatureListCommandData" /> and whose
    /// <c>warnings</c> contain the format and feature warnings; a store without features, also one without a
    /// <c>features</c> directory, is a result, not an error. In JSON mode, an expected failure is written as an error
    /// envelope and the abort message is not printed.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// The current directory is not inside a git repository (exit code
    /// <see cref="ShadeKitCliExitCodes.NotAGitRepository" />), no SHADE kit root is found (exit code
    /// <see cref="ShadeKitCliExitCodes.KitRootNotFound" />), <c>.shadekit</c> at the repository root is a file (exit
    /// code <see cref="ShadeKitCliExitCodes.KitRootNotADirectory" />), or a <c>feature.json</c> is invalid or too new
    /// (exit codes <see cref="ShadeKitCliExitCodes.InvalidFile" /> and <see cref="ShadeKitCliExitCodes.FormatTooNew" />).
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(FeatureListCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await CliCommandAbort.RunAsync(
                () => _featureStore.ListAsync(_kitStoreFactory.Create(), _cancellation.Token), _errorMapper,
                options.Json, _jsonWriter)
            .ConfigureAwait(false);

        var data = new FeatureListCommandData(result.Features);

        FeatureCommandOutput.Write(_ansiConsole, _jsonWriter, options.Json, data,
            FeatureCommandOutput.CreateWarnings(result.FormatWarnings, result.Warnings), () => WriteFeatures(data));

        return CommandResult.Success;
    }

    private void WriteFeatures(FeatureListCommandData data)
    {
        if (data.Features.Count == 0)
        {
            FeatureCommandOutput.WriteLine(_ansiConsole, "No features found.");

            return;
        }

        foreach (var feature in data.Features)
        {
            FeatureCommandOutput.WriteLine(_ansiConsole,
                $"{feature.Id}  {CliJsonWriter.GetEnumName(feature.Status)}  {feature.Profile}  {feature.Title}");
        }
    }
}
