using System.Text.Json.Nodes;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations;

/// <summary>
/// Simulates the plugin commands of the OpenAI Codex CLI <c>codex</c> behind the fake codex runner and locator of a
/// <see cref="CliEndToEndHarness" />, so the end-to-end tests run the real integration service without starting
/// <c>codex</c>.
/// </summary>
/// <remarks>
/// The answers follow the shapes that <c>CodexPluginCli</c> reads, which rest on the source of the Codex CLI
/// (Unverified against a real <c>codex</c>): every command takes <c>--json</c> and prints one JSON object, a failing
/// command prints no JSON and its message on standard error, the marketplace list reports the source but not the ref,
/// <c>plugin marketplace add</c> of the marketplace with the same source and ref answers <c>alreadyAdded</c> and fails
/// for another source or ref, <c>plugin marketplace upgrade</c> refuses a local marketplace, <c>plugin marketplace
/// remove</c> does not refuse a marketplace whose plugins are installed, and <c>plugin add</c> enables the plugin. The
/// simulator knows one other marketplace and one other plugin, which the integration must leave alone.
/// </remarks>
internal sealed class FakeCodex
{
    public const string ExecutablePath = "/usr/local/bin/codex";

    public const string MarketplaceName = "creativecoders-shade-kit";

    public const string PluginName = "shade-kit";

    public const string PluginId = PluginName + "@" + MarketplaceName;

    public const string Repo = "CreativeCodersTeam/shade-kit";

    private const string MarketplaceRoot = "/home/me/.codex/.tmp/marketplaces/" + MarketplaceName;

    /// <summary>
    /// Gets or sets the marketplace <see cref="MarketplaceName" />, or <see langword="null" /> if codex does not know
    /// it.
    /// </summary>
    public FakeCodexMarketplace? Marketplace { get; set; }

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
    /// Makes the fake locator of the harness find <c>codex</c> and the fake runner answer with this simulator.
    /// </summary>
    public FakeCodex AttachTo(CliEndToEndHarness harness)
    {
        A.CallTo(() => harness.CodexExecutableLocator.FindCodexExecutable())
            .Returns(ExecutablePath);

        A.CallTo(() => harness.CodexCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
                A<CancellationToken>._))
            .ReturnsLazily((string workingDirectory, IReadOnlyList<string> arguments, CancellationToken _) =>
                Task.FromResult(Answer(workingDirectory, arguments)));

        return this;
    }

    /// <summary>
    /// Sets the state after an install of the plugin from the SHADE kit repository with the specified ref.
    /// </summary>
    public FakeCodex WithInstalledPlugin(string? @ref, bool enabled = true)
    {
        Marketplace = FakeCodexMarketplace.ForGitHub(Repo, @ref);
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
            ["plugin", "marketplace", "add", var repo, "--ref", var @ref, "--json"] =>
                AddMarketplace(FakeCodexMarketplace.ForGitHub(repo, @ref)),
            ["plugin", "marketplace", "add", var source, "--json"] => AddMarketplace(FakeCodexMarketplace.Parse(source)),
            ["plugin", "marketplace", "upgrade", MarketplaceName, "--json"] => UpgradeMarketplace(),
            ["plugin", "marketplace", "remove", MarketplaceName, "--json"] => RemoveMarketplace(),
            ["plugin", "add", PluginId, "--json"] => AddPlugin(),
            ["plugin", "remove", PluginId, "--json"] => RemovePlugin(),
            _ => Error($"Error: unexpected codex call: {string.Join(' ', arguments)}")
        };
    }

    private string ListPlugins()
    {
        var installed = new JsonArray(new JsonObject
        {
            ["pluginId"] = "cc-ai-dotnet@creativecoders-ai-store",
            ["name"] = "cc-ai-dotnet",
            ["marketplaceName"] = "creativecoders-ai-store",
            ["version"] = "local",
            ["installed"] = true,
            ["enabled"] = true,
            ["source"] = "marketplace",
            ["installPolicy"] = "AVAILABLE",
            ["authPolicy"] = "ON_INSTALL"
        });

        if (IsPluginInstalled)
        {
            installed.Add(new JsonObject
            {
                ["pluginId"] = PluginId,
                ["name"] = PluginName,
                ["marketplaceName"] = MarketplaceName,
                ["version"] = "local",
                ["installed"] = true,
                ["enabled"] = IsPluginEnabled,
                ["source"] = "marketplace",
                ["installPolicy"] = "AVAILABLE",
                ["authPolicy"] = "ON_INSTALL"
            });
        }

        return new JsonObject { ["installed"] = installed, ["available"] = new JsonArray() }.ToJsonString();
    }

    private string ListMarketplaces()
    {
        var marketplaces = new JsonArray(new JsonObject
        {
            ["name"] = "creativecoders-ai-store",
            ["root"] = "/home/me/.codex/.tmp/marketplaces/creativecoders-ai-store",
            ["marketplaceSource"] = new JsonObject
            {
                ["sourceType"] = "git", ["source"] = "https://github.com/CreativeCodersTeam/ai-store.git"
            }
        });

        if (Marketplace is { } marketplace)
        {
            marketplaces.Add(new JsonObject
            {
                ["name"] = MarketplaceName,
                ["root"] = marketplace.Path ?? MarketplaceRoot,
                ["marketplaceSource"] = new JsonObject
                {
                    ["sourceType"] = marketplace.SourceType, ["source"] = marketplace.Source
                }
            });
        }

        return new JsonObject { ["marketplaces"] = marketplaces }.ToJsonString();
    }

    private ExternalProcessResult AddMarketplace(FakeCodexMarketplace requested)
    {
        if (Marketplace is { } existing)
        {
            return existing == requested
                ? Ok(AddMarketplaceOutput(alreadyAdded: true))
                : Error($"Error: marketplace '{MarketplaceName}' is already added from a different source; remove " +
                        "it before adding it again.");
        }

        Marketplace = requested;

        return Ok(AddMarketplaceOutput(alreadyAdded: false));
    }

    private string AddMarketplaceOutput(bool alreadyAdded)
    {
        return new JsonObject
        {
            ["marketplaceName"] = MarketplaceName,
            ["installedRoot"] = Marketplace?.Path ?? MarketplaceRoot,
            ["alreadyAdded"] = alreadyAdded
        }.ToJsonString();
    }

    private ExternalProcessResult UpgradeMarketplace()
    {
        if (Marketplace is null)
        {
            return Error($"Error: marketplace `{MarketplaceName}` is not configured.");
        }

        if (Marketplace.SourceType != "git")
        {
            return Error($"Error: marketplace `{MarketplaceName}` is not configured as a Git marketplace");
        }

        return Ok(new JsonObject
        {
            ["selectedMarketplaces"] = new JsonArray(MarketplaceName),
            ["upgradedRoots"] = new JsonArray(),
            ["errors"] = new JsonArray()
        }.ToJsonString());
    }

    private ExternalProcessResult RemoveMarketplace()
    {
        if (Marketplace is null)
        {
            return Error($"Error: marketplace `{MarketplaceName}` is not configured.");
        }

        Marketplace = null;

        return Ok(new JsonObject { ["marketplaceName"] = MarketplaceName }.ToJsonString());
    }

    private ExternalProcessResult AddPlugin()
    {
        if (Marketplace is null)
        {
            return Error($"Error: plugin `{PluginId}` was not found in any configured marketplace.");
        }

        IsPluginInstalled = true;
        IsPluginEnabled = true;

        return Ok(new JsonObject
        {
            ["pluginId"] = PluginId,
            ["name"] = PluginName,
            ["marketplaceName"] = MarketplaceName,
            ["version"] = "local",
            ["installedPath"] = $"/home/me/.codex/plugins/cache/{MarketplaceName}/{PluginName}/local",
            ["authPolicy"] = "ON_INSTALL"
        }.ToJsonString());
    }

    private ExternalProcessResult RemovePlugin()
    {
        if (!IsPluginInstalled)
        {
            return Error($"Error: plugin `{PluginId}` is not installed.");
        }

        IsPluginInstalled = false;

        return Ok(new JsonObject { ["pluginId"] = PluginId, ["name"] = PluginName }.ToJsonString());
    }

    private static ExternalProcessResult Ok(string output)
    {
        return new ExternalProcessResult(0, output + "\n", string.Empty);
    }

    private static ExternalProcessResult Error(string message)
    {
        return new ExternalProcessResult(1, string.Empty, message + "\n");
    }
}

/// <summary>
/// Represents the marketplace <see cref="FakeCodex.MarketplaceName" /> as the simulator stores it; codex reports the
/// source type and the source, but not the <see cref="Ref" />.
/// </summary>
internal sealed record FakeCodexMarketplace(string SourceType, string Source, string? Ref, string? Path)
{
    public static FakeCodexMarketplace ForGitHub(string repo, string? @ref)
    {
        return new FakeCodexMarketplace("git", $"https://github.com/{repo}.git", @ref, null);
    }

    public static FakeCodexMarketplace Parse(string source)
    {
        return System.IO.Path.IsPathFullyQualified(source)
            ? new FakeCodexMarketplace("local", source, null, source)
            : ForGitHub(source, null);
    }
}
