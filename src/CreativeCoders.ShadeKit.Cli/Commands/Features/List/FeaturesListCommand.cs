using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Store;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Features.List;

/// <summary>
/// Lists the names of all features in the SHADE kit store of the current directory.
/// </summary>
/// <param name="ansiConsole">The console that receives the feature names.</param>
/// <param name="kitStoreFactory">The factory that locates the SHADE kit store.</param>
[UsedImplicitly]
[CliCommand([FeaturesCommandGroup.Name, Name], Description = "Lists all features of the SHADE kit store")]
public class FeaturesListCommand(IAnsiConsole ansiConsole, IKitStoreFactory kitStoreFactory) : ICliCommand
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeaturesCommandGroup" />.
    /// </summary>
    public const string Name = "list";

    private readonly IAnsiConsole _ansiConsole = Ensure.NotNull(ansiConsole);

    private readonly IKitStoreFactory _kitStoreFactory = Ensure.NotNull(kitStoreFactory);

    /// <summary>
    /// Writes the names of all features of the SHADE kit store to the console, one name per line in ordinal order.
    /// </summary>
    /// <remarks>
    /// The store is located by searching the current directory and its parent directories for a <c>.shadekit</c>
    /// directory. If the store contains no features, a hint is written instead.
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.</returns>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit root directory is found (exit code <see cref="ShadeKitCliExitCodes.KitRootNotFound" />), or the
    /// SHADE kit root directory contains no <c>features</c> directory (exit code
    /// <see cref="ShadeKitCliExitCodes.FeaturesDirectoryNotFound" />).
    /// </exception>
    public async Task<CommandResult> ExecuteAsync()
    {
        var featureNames = GetFeatureNames();

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

    private string[] GetFeatureNames()
    {
        var kitStore = CreateKitStore();

        try
        {
            return [.. kitStore.GetFeatureNames().Order(StringComparer.Ordinal)];
        }
        catch (FeaturesDirectoryNotFoundException e)
        {
            throw new CliCommandAbortException(
                Markup.Escape(
                    $"The SHADE kit root directory contains no '{KitStorePaths.FeaturesDirName}' directory " +
                    $"('{e.FeaturesPath}')."),
                ShadeKitCliExitCodes.FeaturesDirectoryNotFound, e);
        }
    }

    private IKitStore CreateKitStore()
    {
        try
        {
            return _kitStoreFactory.Create();
        }
        catch (KitRootPathNotFoundException e)
        {
            throw new CliCommandAbortException(
                Markup.Escape(
                    $"No SHADE kit root directory '{KitStorePaths.RootPath}' found in " +
                    $"'{Directory.GetCurrentDirectory()}' or any of its parent directories."),
                ShadeKitCliExitCodes.KitRootNotFound, e);
        }
    }
}
