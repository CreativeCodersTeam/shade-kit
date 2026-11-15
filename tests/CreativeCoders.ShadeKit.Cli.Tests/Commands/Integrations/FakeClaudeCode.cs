using System.Text.Json;
using System.Text.Json.Nodes;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations;

/// <summary>
/// Simulates the plugin commands of the Claude Code CLI <c>claude</c> behind the fake claude runner and locator of a
/// <see cref="CliEndToEndHarness" />, so the end-to-end tests run the real integration service without starting
/// <c>claude</c>.
/// </summary>
/// <remarks>
/// The answers follow the output of Claude Code 2.1.323: removing a marketplace also uninstalls its plugins, a plugin
/// without a version is listed with the commit SHA as its version, and a plugin update sees new commits of the
/// marketplace source (<see cref="RemoteRevision" />) only after the marketplace was added or refreshed with
/// <c>claude plugin marketplace update</c>. The simulator knows one other marketplace
/// and one other plugin, which the integration must leave alone.
/// </remarks>
internal sealed class FakeClaudeCode
{
    public const string ExecutablePath = "/usr/local/bin/claude";

    public const string MarketplaceName = "creativecoders-shade-kit";

    public const string PluginId = "shade-kit@" + MarketplaceName;

    public const string Repo = "CreativeCodersTeam/shade-kit";

    private string? _installedRef;

    private int _marketplaceRevision;

    private int _installedRevision;

    /// <summary>
    /// Gets or sets the revision of the marketplace source, which a new commit on a branch raises.
    /// </summary>
    public int RemoteRevision { get; set; }

    /// <summary>
    /// Gets or sets the marketplace <see cref="MarketplaceName" />, or <see langword="null" /> if claude does not know
    /// it.
    /// </summary>
    public FakeClaudeMarketplace? Marketplace { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether the plugin <see cref="PluginId" /> is installed with the scope
    /// <c>user</c>.
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
    /// Makes the fake locator of the harness find <c>claude</c> and the fake runner answer with this simulator.
    /// </summary>
    public FakeClaudeCode AttachTo(CliEndToEndHarness harness)
    {
        A.CallTo(() => harness.ClaudeExecutableLocator.FindClaudeExecutable())
            .Returns(ExecutablePath);

        A.CallTo(() => harness.ClaudeCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
                A<CancellationToken>._))
            .ReturnsLazily((string workingDirectory, IReadOnlyList<string> arguments, CancellationToken _) =>
                Task.FromResult(Answer(workingDirectory, arguments)));

        return this;
    }

    /// <summary>
    /// Sets the state after an install of the plugin from the SHADE kit repository with the specified ref.
    /// </summary>
    public FakeClaudeCode WithInstalledPlugin(string? @ref, bool enabled = true)
    {
        Marketplace = FakeClaudeMarketplace.ForGitHub(Repo, @ref);
        IsPluginInstalled = true;
        IsPluginEnabled = enabled;
        _installedRef = @ref;
        _marketplaceRevision = RemoteRevision;
        _installedRevision = RemoteRevision;

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
            ["plugin", "marketplace", "list", "--json"] => Ok(ListMarketplaces()),
            ["plugin", "marketplace", "add", var source, "--scope", "user", "--json"] => AddMarketplace(source),
            ["plugin", "marketplace", "remove", MarketplaceName, "--json"] => RemoveMarketplace(),
            ["plugin", "marketplace", "update", MarketplaceName, "--json"] => UpdateMarketplace(),
            ["plugin", "list", "--json"] => Ok(ListPlugins()),
            ["plugin", "install", PluginId, "--scope", "user", "--json"] => InstallPlugin(),
            ["plugin", "update", PluginId, "--scope", "user", "--json"] => UpdatePlugin(),
            _ => new ExternalProcessResult(1, string.Empty, $"unexpected claude call: {string.Join(' ', arguments)}")
        };
    }

    private string ListMarketplaces()
    {
        var marketplaces = new JsonArray(new JsonObject
        {
            ["name"] = "creativecoders-ai-store",
            ["source"] = "github",
            ["repo"] = "CreativeCodersTeam/ai-store",
            ["ref"] = "main",
            ["installLocation"] = "/home/me/.claude/plugins/marketplaces/creativecoders-ai-store"
        });

        if (Marketplace is { } marketplace)
        {
            var entry = new JsonObject { ["name"] = MarketplaceName, ["source"] = marketplace.SourceKind };

            if (marketplace.Repo is not null)
            {
                entry["repo"] = marketplace.Repo;
            }

            if (marketplace.Ref is not null)
            {
                entry["ref"] = marketplace.Ref;
            }

            if (marketplace.Path is not null)
            {
                entry["path"] = marketplace.Path;
            }

            entry["installLocation"] = marketplace.Path ?? $"/home/me/.claude/plugins/marketplaces/{MarketplaceName}";
            marketplaces.Add(entry);
        }

        return marketplaces.ToJsonString();
    }

    private string ListPlugins()
    {
        var plugins = new JsonArray(new JsonObject
        {
            ["id"] = "cc-ai-dotnet@creativecoders-ai-store",
            ["version"] = "4972bfa14908",
            ["scope"] = "user",
            ["enabled"] = true
        });

        if (IsPluginInstalled)
        {
            plugins.Add(new JsonObject
            {
                ["id"] = PluginId,
                ["version"] = "1a2b3c4d5e6f",
                ["scope"] = "user",
                ["enabled"] = IsPluginEnabled,
                ["installPath"] = $"/home/me/.claude/plugins/cache/{MarketplaceName}/shade-kit/1a2b3c4d5e6f",
                ["projectEnabled"] = false
            });
        }

        return plugins.ToJsonString();
    }

    private ExternalProcessResult AddMarketplace(string source)
    {
        Marketplace = FakeClaudeMarketplace.Parse(source);
        _marketplaceRevision = RemoteRevision;

        return Ok(Line(new JsonObject
        {
            ["command"] = "marketplace-add",
            ["outcome"] = "ok",
            ["marketplace"] = MarketplaceName,
            ["message"] = $"Successfully added marketplace: {MarketplaceName}"
        }));
    }

    private ExternalProcessResult RemoveMarketplace()
    {
        Marketplace = null;
        IsPluginInstalled = false;
        _installedRef = null;

        return Ok(Line(new JsonObject
        {
            ["command"] = "marketplace-remove",
            ["outcome"] = "ok",
            ["marketplace"] = MarketplaceName,
            ["message"] = $"Successfully removed marketplace: {MarketplaceName}. Also uninstalled 1 plugin."
        }));
    }

    private ExternalProcessResult UpdateMarketplace()
    {
        if (Marketplace is null)
        {
            return new ExternalProcessResult(1, Line(new JsonObject
            {
                ["command"] = "marketplace-update",
                ["marketplace"] = MarketplaceName,
                ["outcome"] = "failed",
                ["message"] = $"Marketplace '{MarketplaceName}' not found.",
                ["failureCode"] = "not_configured"
            }), "✘ Failed to update marketplace(s)\n");
        }

        _marketplaceRevision = RemoteRevision;

        return Ok(Line(new JsonObject
        {
            ["command"] = "marketplace-update",
            ["outcome"] = "ok",
            ["marketplace"] = MarketplaceName,
            ["message"] = $"Successfully updated marketplace: {MarketplaceName}"
        }));
    }

    private ExternalProcessResult InstallPlugin()
    {
        IsPluginInstalled = true;
        IsPluginEnabled = true;
        _installedRef = Marketplace?.Ref;
        _installedRevision = _marketplaceRevision;

        return Ok(Line(new JsonObject
        {
            ["command"] = "install",
            ["outcome"] = "ok",
            ["plugin"] = PluginId,
            ["pluginId"] = PluginId,
            ["scope"] = "user",
            ["message"] = $"Successfully installed plugin: {PluginId} (scope: user)"
        }));
    }

    private ExternalProcessResult UpdatePlugin()
    {
        var isUpToDate = _installedRef == Marketplace?.Ref && _installedRevision == _marketplaceRevision;
        _installedRef = Marketplace?.Ref;
        _installedRevision = _marketplaceRevision;

        var result = new JsonObject
        {
            ["command"] = "update",
            ["outcome"] = "ok",
            ["plugin"] = PluginId,
            ["pluginId"] = PluginId,
            ["scope"] = "user",
            ["updateOutcome"] = isUpToDate ? "up_to_date" : "updated"
        };

        if (!isUpToDate)
        {
            result["oldVersion"] = "0a1b2c3d4e5f";
            result["newVersion"] = "1a2b3c4d5e6f";
        }

        return Ok(Line(result));
    }

    private static string Line(JsonObject result)
    {
        return result.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + "\n";
    }

    private static ExternalProcessResult Ok(string output)
    {
        return new ExternalProcessResult(0, output, string.Empty);
    }
}

/// <summary>
/// Represents the marketplace <see cref="FakeClaudeCode.MarketplaceName" /> as the simulator stores it.
/// </summary>
internal sealed record FakeClaudeMarketplace(string SourceKind, string? Repo, string? Ref, string? Path)
{
    public static FakeClaudeMarketplace ForGitHub(string repo, string? @ref)
    {
        return new FakeClaudeMarketplace("github", repo, @ref, null);
    }

    public static FakeClaudeMarketplace Parse(string source)
    {
        if (System.IO.Path.IsPathFullyQualified(source))
        {
            return new FakeClaudeMarketplace("directory", null, null, source);
        }

        var parts = source.Split('#', 2);

        return ForGitHub(parts[0], parts.Length == 2 ? parts[1] : null);
    }
}
