using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Store;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Features.List;

/// <summary>
/// Lists the names of all features in the SHADE kit store of the current repository.
/// </summary>
/// <param name="ansiConsole">The console that receives the plain feature names.</param>
/// <param name="kitStoreFactory">The factory that locates the SHADE kit store.</param>
/// <param name="jsonWriter">The writer that receives the JSON output.</param>
/// <param name="errorMapper">The mapper that turns store failures into exit codes, error codes, and messages.</param>
[UsedImplicitly]
[CliCommand([FeaturesCommandGroup.Name, Name], Description = "Lists all features of the SHADE kit store")]
public class FeaturesListCommand(
    IAnsiConsole ansiConsole,
    IKitStoreFactory kitStoreFactory,
    ICliJsonWriter jsonWriter,
    CliErrorMapper errorMapper)
    : ICliCommand<FeaturesListCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeaturesCommandGroup" />.
    /// </summary>
    public const string Name = "list";

    private readonly IAnsiConsole _ansiConsole = Ensure.NotNull(ansiConsole);

    private readonly IKitStoreFactory _kitStoreFactory = Ensure.NotNull(kitStoreFactory);

    private readonly ICliJsonWriter _jsonWriter = Ensure.NotNull(jsonWriter);

    private readonly CliErrorMapper _errorMapper = Ensure.NotNull(errorMapper);

    /// <summary>
    /// Writes the names of all features of the SHADE kit store in ordinal order, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// The store is the <c>.shadekit</c> directory at the root of the git repository that contains the current
    /// directory. Plain output is one name per line, or a hint if the store has no features. JSON output is an
    /// envelope whose <c>data</c> is a <see cref="FeaturesListCommandData" />; a store without features is a result,
    /// not an error. In JSON mode, an expected failure is written as an error envelope and the abort message is not
    /// printed.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit root is found (exit code <see cref="ShadeKitCliExitCodes.KitRootNotFound" />), or the SHADE kit
    /// root directory contains no <c>features</c> directory (exit code
    /// <see cref="ShadeKitCliExitCodes.FeaturesDirectoryNotFound" />).
    /// </exception>
    public async Task<CommandResult> ExecuteAsync(FeaturesListCommandOptions options)
    {
        Ensure.NotNull(options);

        var featureNames = GetFeatureNames(options.Json);

        if (options.Json)
        {
            _jsonWriter.Write(CliJsonEnvelope.Success(new FeaturesListCommandData(featureNames)));

            return CommandResult.Success;
        }

        if (featureNames.Length == 0)
        {
            _ansiConsole.WriteLine("No features found.");

            return CommandResult.Success;
        }

        foreach (var featureName in featureNames)
        {
            _ansiConsole.WriteLine(featureName);
        }

        return CommandResult.Success;
    }

    private string[] GetFeatureNames(bool json)
    {
        try
        {
            return [.. _kitStoreFactory.Create().GetFeatureNames().Order(StringComparer.Ordinal)];
        }
        catch (Exception e) when (_errorMapper.Map(e) is { } error)
        {
            throw CreateAbortException(error, json, e);
        }
    }

    private CliCommandAbortException CreateAbortException(CliError error, bool json, Exception exception)
    {
        if (!json)
        {
            return new CliCommandAbortException(Markup.Escape(error.Message), error.ExitCode, exception);
        }

        _jsonWriter.Write(CliJsonEnvelope.Failure(error.ToJsonMessage()));

        return new CliCommandAbortException(error.Message, error.ExitCode, exception) { PrintMessage = false };
    }
}
