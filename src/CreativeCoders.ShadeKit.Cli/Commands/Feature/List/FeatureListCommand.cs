using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
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
                $"{feature.Id}  {CliJsonWriter.GetEnumName(feature.Status)}  {feature.Profile}  {feature.Title}");
        }
    }
}
