using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;

/// <summary>
/// Shows the complete state of a feature of the SHADE kit store of the current repository.
/// </summary>
/// <param name="ansiConsole">The console that receives the plain result.</param>
/// <param name="kitStoreFactory">The factory that locates the SHADE kit store.</param>
/// <param name="featureStore">The store that reads the feature.</param>
/// <param name="jsonWriter">The writer that receives the JSON output.</param>
/// <param name="errorMapper">The mapper that turns failures into exit codes, error codes, and messages.</param>
/// <param name="cancellation">The cancellation of the run, whose token the command passes to the core.</param>
[UsedImplicitly]
[CliCommand([FeatureCommandGroup.Name, Name], Description = "Shows the complete state of a feature")]
public class FeatureShowCommand(
    IAnsiConsole ansiConsole,
    IKitStoreFactory kitStoreFactory,
    IFeatureStore featureStore,
    ICliJsonWriter jsonWriter,
    CliErrorMapper errorMapper,
    ICliCancellation cancellation)
    : ICliCommand<FeatureShowCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeatureCommandGroup" />.
    /// </summary>
    public const string Name = "show";

    private readonly IAnsiConsole _ansiConsole = Ensure.NotNull(ansiConsole);

    private readonly IKitStoreFactory _kitStoreFactory = Ensure.NotNull(kitStoreFactory);

    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    private readonly ICliJsonWriter _jsonWriter = Ensure.NotNull(jsonWriter);

    private readonly CliErrorMapper _errorMapper = Ensure.NotNull(errorMapper);

    private readonly ICliCancellation _cancellation = Ensure.NotNull(cancellation);

    /// <summary>
    /// Reads the feature and writes its state, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// Plain output is one line <c>Warning: &lt;message&gt;</c> per warning, then the key/value lines <c>Id</c>,
    /// <c>Title</c>, <c>Profile</c>, <c>Status</c>, <c>Source</c> (<c>&lt;kind&gt; &lt;value&gt;</c>), <c>Created</c>
    /// (<c>&lt;createdAt&gt; by &lt;name&gt; &lt;&lt;email&gt;&gt;</c>), <c>Feature directory</c>,
    /// <c>Feature file</c>, and <c>Request</c>. JSON output is an envelope whose <c>data</c> is a
    /// <see cref="FeatureShowCommandData" /> and whose <c>warnings</c> contain the format warnings and the feature
    /// warnings, such as <c>feature-id-mismatch</c>. In JSON mode, an expected failure is written as an error envelope
    /// and the abort message is not printed.
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
    /// (exit code <see cref="ShadeKitCliExitCodes.FeatureNotFound" />); or its <c>feature.json</c> is missing,
    /// invalid, or too new (exit codes <see cref="ShadeKitCliExitCodes.InvalidFile" /> and
    /// <see cref="ShadeKitCliExitCodes.FormatTooNew" />).
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(FeatureShowCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await CliCommandAbort.RunAsync(
                () => _featureStore.ShowAsync(_kitStoreFactory.Create(), options.FeatureId, _cancellation.Token),
                _errorMapper, options.Json, _jsonWriter)
            .ConfigureAwait(false);

        var data = FeatureShowCommandData.FromResult(result);

        FeatureCommandOutput.Write(_ansiConsole, _jsonWriter, options.Json, data,
            FeatureCommandOutput.CreateWarnings(result.FormatWarnings, result.Warnings), () => WriteFeature(data));

        return CommandResult.Success;
    }

    private void WriteFeature(FeatureShowCommandData data)
    {
        var source = data.Source.Value is null
            ? CliJsonWriter.GetEnumName(data.Source.Kind)
            : $"{CliJsonWriter.GetEnumName(data.Source.Kind)} {data.Source.Value}";
        var created = FeatureTextFormat.FormatCreated(data.CreatedAt, data.CreatedBy.Name, data.CreatedBy.Email);

        FeatureCommandOutput.WriteLine(_ansiConsole, $"Id: {data.Id}");
        FeatureCommandOutput.WriteLine(_ansiConsole, $"Title: {data.Title}");
        FeatureCommandOutput.WriteLine(_ansiConsole, $"Profile: {data.Profile}");
        FeatureCommandOutput.WriteLine(_ansiConsole, $"Status: {CliJsonWriter.GetEnumName(data.Status)}");
        FeatureCommandOutput.WriteLine(_ansiConsole, $"Source: {source}");
        FeatureCommandOutput.WriteLine(_ansiConsole, $"Created: {created}");
        FeatureCommandOutput.WriteLine(_ansiConsole, $"Feature directory: {data.Paths.FeatureDir}");
        FeatureCommandOutput.WriteLine(_ansiConsole, $"Feature file: {data.Paths.FeatureFile}");
        FeatureCommandOutput.WriteLine(_ansiConsole, $"Request: {data.Paths.Request}");
    }
}
