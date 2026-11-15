using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations;

/// <summary>
/// Creates the results of the integration service for the tests of the <c>shade-kit integrations</c> commands.
/// </summary>
internal static class IntegrationsCommandTestData
{
    public const string PluginId = "shade-kit@creativecoders-shade-kit";

    public static readonly MarketplaceSource ReleaseSource =
        MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit", "v0.8.0");

    public static IntegrationResult CreateResult(IntegrationOutcome outcome, string? previousKitVersion = null,
        bool enabled = true)
    {
        return new IntegrationResult("claude", PluginId, outcome, previousKitVersion, "0.8.0", "v0.8.0", enabled);
    }

    /// <summary>
    /// Creates the result of an update that found no installed integration, so it resolved no source.
    /// </summary>
    public static IntegrationRunResult CreateNothingInstalledRun()
    {
        return new IntegrationRunResult(null, [], []);
    }

    public static IntegrationRunResult CreateRun(params IntegrationResult[] results)
    {
        return CreateRun(results, []);
    }

    public static IntegrationRunResult CreateRun(IReadOnlyList<IntegrationResult> results,
        IReadOnlyList<KitWarning> warnings, MarketplaceSource? source = null)
    {
        return new IntegrationRunResult(source ?? ReleaseSource, results, warnings);
    }
}
