using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
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

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) => _featureStore.ShowAsync(store, options.FeatureId, cancellationToken))
            .ConfigureAwait(false);

        var data = FeatureShowCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () => WriteFeature(data, result.Feature));

        return CommandResult.Success;
    }

    private void WriteFeature(FeatureShowCommandData data, FeatureDocument feature)
    {
        var source = data.Source.Value is null
            ? CliJsonWriter.GetEnumName(data.Source.Kind)
            : $"{CliJsonWriter.GetEnumName(data.Source.Kind)} {data.Source.Value}";
        var created = FeatureTextFormat.FormatCreated(feature);

        _context.Console.WriteLine($"Id: {data.Id}");
        _context.Console.WriteLine($"Title: {data.Title}");
        _context.Console.WriteLine($"Profile: {data.Profile}");
        _context.Console.WriteLine($"Status: {CliJsonWriter.GetEnumName(data.Status)}");
        _context.Console.WriteLine($"Source: {source}");
        _context.Console.WriteLine($"Created: {created}");
        _context.Console.WriteLine($"Feature directory: {data.Paths.FeatureDir}");
        _context.Console.WriteLine($"Feature file: {data.Paths.FeatureFile}");
        _context.Console.WriteLine($"Request: {data.Paths.Request}");
    }
}
