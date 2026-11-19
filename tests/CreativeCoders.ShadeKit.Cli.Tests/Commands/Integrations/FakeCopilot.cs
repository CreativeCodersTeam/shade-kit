using System.Text.Json.Nodes;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations;

/// <summary>
/// Simulates the plugin commands of the GitHub Copilot CLI <c>copilot</c> behind the fake copilot runner and locator of
/// a <see cref="CliEndToEndHarness" />, so the end-to-end tests run the real integration service without starting
/// <c>copilot</c>.
/// </summary>
/// <remarks>
/// The answers follow the shapes that <c>CopilotPluginCli</c> reads, which rest on the GitHub Docs plugin reference
/// (Unverified against a real <c>copilot</c>): only <c>plugin list</c> and <c>plugin marketplace list</c> print JSON,
/// the change commands print text and report success by the exit code, <c>plugin marketplace add</c> refuses a
/// marketplace that exists, and <c>plugin marketplace remove</c> without <c>--force</c> refuses a marketplace whose
/// plugins are still installed. The simulator knows one other marketplace and one other plugin, which the integration
/// must leave alone.
/// </remarks>
internal sealed class FakeCopilot
{
    public const string ExecutablePath = "/usr/local/bin/copilot";

    public const string MarketplaceName = "creativecoders-shade-kit";

    public const string PluginName = "shade-kit";

    public const string PluginId = PluginName + "@" + MarketplaceName;

    public const string Repo = "CreativeCodersTeam/shade-kit";

    /// <summary>
    /// Gets or sets the marketplace <see cref="MarketplaceName" />, or <see langword="null" /> if copilot does not know
    /// it.
    /// </summary>
    public FakeCopilotMarketplace? Marketplace { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether the plugin <see cref="PluginId" /> is installed.
    /// </summary>
    public bool IsPluginInstalled { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether the installed plugin is enabled.
    /// </summary>
    public bool IsPluginEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a function that returns the failed result of a call, or <see langword="null" /> to answer it.
    /// </summary>
    public Func<IReadOnlyList<string>, ExternalProcessResult?>? Fail { get; set; }

    /// <summary>
    /// Gets the arguments of every call, in the order of the calls.
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// Gets the working directory of every call.
    /// </summary>
    public List<string> WorkingDirectories { get; } = [];

    /// <summary>
    /// Makes the fake locator of the harness find <c>copilot</c> and the fake runner answer with this simulator.
    /// </summary>
    public FakeCopilot AttachTo(CliEndToEndHarness harness)
    {
        A.CallTo(() => harness.CopilotExecutableLocator.FindCopilotExecutable())
            .Returns(ExecutablePath);

        A.CallTo(() => harness.CopilotCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
                A<CancellationToken>._))
            .ReturnsLazily((string workingDirectory, IReadOnlyList<string> arguments, CancellationToken _) =>
                Task.FromResult(Answer(workingDirectory, arguments)));

        return this;
    }

    /// <summary>
    /// Sets the state after an install of the plugin from the SHADE kit repository with the specified ref.
    /// </summary>
    public FakeCopilot WithInstalledPlugin(string? @ref, bool enabled = true)
    {
        Marketplace = FakeCopilotMarketplace.ForGitHub(Repo, @ref);
        IsPluginInstalled = true;
        IsPluginEnabled = enabled;

        return this;
    }

    private ExternalProcessResult Answer(string workingDirectory, IReadOnlyList<string> arguments)
    {
        Calls.Add(string.Join(' ', arguments));
        WorkingDirectories.Add(workingDirectory);

        if (Fail?.Invoke(arguments) is { } failure)
        {
            return failure;
        }

        return arguments switch
        {
            ["plugin", "list", "--json"] => Ok(ListPlugins()),
            ["plugin", "marketplace", "list", "--json"] => Ok(ListMarketplaces()),
            ["plugin", "marketplace", "add", var source] => AddMarketplace(source),
            ["plugin", "marketplace", "remove", MarketplaceName] => RemoveMarketplace(),
            ["plugin", "marketplace", "update", MarketplaceName] => UpdateMarketplace(),
            ["plugin", "install", PluginId] => InstallPlugin(),
            ["plugin", "update", PluginId] => UpdatePlugin(),
            ["plugin", "uninstall", PluginId] => UninstallPlugin(),
            _ => Error($"unexpected copilot call: {string.Join(' ', arguments)}")
        };
    }

    private string ListPlugins()
    {
        var plugins = new JsonArray(new JsonObject
        {
            ["name"] = "cc-ai-dotnet",
            ["marketplace"] = "creativecoders-ai-store",
            ["version"] = "1.4.0",
            ["enabled"] = true,
            ["source"] = "marketplace"
        });

        if (IsPluginInstalled)
        {
            plugins.Add(new JsonObject
            {
                ["name"] = PluginName,
                ["marketplace"] = MarketplaceName,
                ["enabled"] = IsPluginEnabled,
                ["source"] = "marketplace"
            });
        }

        return plugins.ToJsonString();
    }

    private string ListMarketplaces()
    {
        var marketplaces = new JsonArray(new JsonObject
        {
            ["name"] = "creativecoders-ai-store",
            ["source"] = new JsonObject
            {
                ["source"] = "github", ["repo"] = "CreativeCodersTeam/ai-store", ["ref"] = "main"
            }
        });

        if (Marketplace is { } marketplace)
        {
            var source = new JsonObject { ["source"] = marketplace.SourceKind };

            if (marketplace.Repo is not null)
            {
                source["repo"] = marketplace.Repo;
            }

            if (marketplace.Ref is not null)
            {
                source["ref"] = marketplace.Ref;
            }

            if (marketplace.Path is not null)
            {
                source["path"] = marketplace.Path;
            }

            marketplaces.Add(new JsonObject { ["name"] = MarketplaceName, ["source"] = source });
        }

        return marketplaces.ToJsonString();
    }

    private ExternalProcessResult AddMarketplace(string source)
    {
        if (Marketplace is not null)
        {
            return Error($"Error: Marketplace '{MarketplaceName}' already exists.");
        }

        Marketplace = FakeCopilotMarketplace.Parse(source);

        return Ok($"Marketplace '{MarketplaceName}' added.\n");
    }

    private ExternalProcessResult RemoveMarketplace()
    {
        if (Marketplace is null)
        {
            return Error($"Error: Marketplace '{MarketplaceName}' not found.");
        }

        if (IsPluginInstalled)
        {
            return Error($"Error: Marketplace '{MarketplaceName}' has installed plugins ({PluginName}). Pass --force " +
                         "to also uninstall them.");
        }

        Marketplace = null;

        return Ok($"Marketplace '{MarketplaceName}' removed.\n");
    }

    private ExternalProcessResult UpdateMarketplace()
    {
        return Marketplace is null
            ? Error($"Error: Marketplace '{MarketplaceName}' not found.")
            : Ok($"Marketplace '{MarketplaceName}' updated.\n");
    }

    private ExternalProcessResult InstallPlugin()
    {
        if (Marketplace is null)
        {
            return Error($"Error: Marketplace '{MarketplaceName}' not found.");
        }

        IsPluginInstalled = true;
        IsPluginEnabled = true;

        return Ok($"Plugin '{PluginId}' installed.\n");
    }

    private ExternalProcessResult UpdatePlugin()
    {
        return IsPluginInstalled
            ? Ok($"Plugin '{PluginId}' updated.\n")
            : Error($"Error: Plugin '{PluginId}' is not installed.");
    }

    private ExternalProcessResult UninstallPlugin()
    {
        if (!IsPluginInstalled)
        {
            return Error($"Error: Plugin '{PluginId}' is not installed.");
        }

        IsPluginInstalled = false;

        return Ok($"Plugin '{PluginId}' uninstalled.\n");
    }

    private static ExternalProcessResult Ok(string output)
    {
        return new ExternalProcessResult(0, output, string.Empty);
    }

    private static ExternalProcessResult Error(string message)
    {
        return new ExternalProcessResult(1, string.Empty, message + "\n");
    }
}

/// <summary>
/// Represents the marketplace <see cref="FakeCopilot.MarketplaceName" /> as the simulator stores it.
/// </summary>
internal sealed record FakeCopilotMarketplace(string SourceKind, string? Repo, string? Ref, string? Path)
{
    public static FakeCopilotMarketplace ForGitHub(string repo, string? @ref)
    {
        return new FakeCopilotMarketplace("github", repo, @ref, null);
    }

    public static FakeCopilotMarketplace Parse(string source)
    {
        if (System.IO.Path.IsPathFullyQualified(source))
        {
            return new FakeCopilotMarketplace("directory", null, null, source);
        }

        var parts = source.Split('#', 2);

        return ForGitHub(parts[0], parts.Length == 2 ? parts[1] : null);
    }
}
