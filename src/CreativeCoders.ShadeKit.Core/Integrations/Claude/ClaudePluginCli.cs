using System.Text.Json;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Runs the plugin and marketplace commands of the Claude Code CLI <c>claude</c> with the
/// <see cref="IClaudeCommandRunner" /> and reads their JSON output.
/// </summary>
/// <remarks>
/// <para>
/// <c>claude</c> runs in the home directory of the user (or the temporary directory if the home directory is not
/// known or does not exist), not in the current directory, so the settings of the current project take no part and
/// no file is written into a repository.
/// </para>
/// <para>
/// A list command must print a JSON array whose entries are objects. A marketplace entry needs the string
/// <c>name</c>; <c>source</c>, <c>repo</c>, <c>ref</c>, and <c>path</c> are optional strings. A plugin entry needs the
/// string <c>id</c> and the Boolean <c>enabled</c>; <c>version</c> and <c>scope</c> are optional strings. A value of
/// another type is a failure; unknown fields are ignored.
/// </para>
/// </remarks>
public sealed class ClaudePluginCli : IClaudePluginCli
{
    private const string JsonOption = "--json";

    private const string ScopeOption = "--scope";

    private const string ToolName = "claude";

    private readonly IClaudeCommandRunner _claudeCommandRunner;

    private readonly string _workingDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudePluginCli" /> class that runs <c>claude</c> in the home
    /// directory of the user.
    /// </summary>
    /// <param name="claudeCommandRunner">The runner that starts <c>claude</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="claudeCommandRunner" /> is <see langword="null" />.
    /// </exception>
    public ClaudePluginCli(IClaudeCommandRunner claudeCommandRunner)
        : this(claudeCommandRunner, UserWorkingDirectory.Get())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudePluginCli" /> class that runs <c>claude</c> in the specified
    /// directory.
    /// </summary>
    /// <param name="claudeCommandRunner">The runner that starts <c>claude</c>.</param>
    /// <param name="workingDirectory">The directory in which <c>claude</c> runs.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="claudeCommandRunner" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="workingDirectory" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    internal ClaudePluginCli(IClaudeCommandRunner claudeCommandRunner, string workingDirectory)
    {
        _claudeCommandRunner = Ensure.NotNull(claudeCommandRunner);
        _workingDirectory = Ensure.IsNotNullOrWhitespace(workingDirectory);
    }

    /// <summary>
    /// Gets the directory in which <c>claude</c> runs.
    /// </summary>
    /// <value>
    /// The home directory of the user, the temporary directory, or the directory passed to the constructor.
    /// </value>
    internal string WorkingDirectory => _workingDirectory;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClaudeMarketplace>> ListMarketplacesAsync(
        CancellationToken cancellationToken = default)
    {
        var reader = await RunAsync(["plugin", "marketplace", "list", JsonOption], cancellationToken)
            .ConfigureAwait(false);

        return reader.ReadArray((entry, entryName) => ReadMarketplace(reader, entry, entryName));
    }

    /// <inheritdoc />
    public async Task AddMarketplaceAsync(string source, CancellationToken cancellationToken = default)
    {
        PositionalArgument.Ensure(source, ToolName, nameof(source));

        var reader = await RunAsync(
                ["plugin", "marketplace", "add", source, ScopeOption, ClaudeInstalledPlugin.UserScope, JsonOption],
                cancellationToken)
            .ConfigureAwait(false);

        reader.ReadResultObject();
    }

    /// <inheritdoc />
    public async Task RemoveMarketplaceAsync(string name, CancellationToken cancellationToken = default)
    {
        PositionalArgument.Ensure(name, ToolName, nameof(name));

        var reader = await RunAsync(["plugin", "marketplace", "remove", name, JsonOption], cancellationToken)
            .ConfigureAwait(false);

        reader.ReadResultObject();
    }

    /// <inheritdoc />
    public async Task UpdateMarketplaceAsync(string name, CancellationToken cancellationToken = default)
    {
        PositionalArgument.Ensure(name, ToolName, nameof(name));

        var reader = await RunAsync(["plugin", "marketplace", "update", name, JsonOption], cancellationToken)
            .ConfigureAwait(false);

        reader.ReadResultObject();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClaudeInstalledPlugin>> ListPluginsAsync(
        CancellationToken cancellationToken = default)
    {
        var reader = await RunAsync(["plugin", "list", JsonOption], cancellationToken).ConfigureAwait(false);

        return reader.ReadArray((entry, entryName) => ReadInstalledPlugin(reader, entry, entryName));
    }

    /// <inheritdoc />
    public async Task<ClaudePluginInstallResult> InstallPluginAsync(string pluginId,
        CancellationToken cancellationToken = default)
    {
        PositionalArgument.Ensure(pluginId, ToolName, nameof(pluginId));

        var reader = await RunAsync(
                ["plugin", "install", pluginId, ScopeOption, ClaudeInstalledPlugin.UserScope, JsonOption],
                cancellationToken)
            .ConfigureAwait(false);

        var resultObject = reader.ReadResultObject();

        return new ClaudePluginInstallResult(
            reader.Fields.ReadOptionalString(resultObject, "installedVersion", "result"),
            reader.Fields.ReadOptionalString(resultObject, "availableVersion", "result"));
    }

    /// <inheritdoc />
    public async Task<ClaudePluginUpdateResult> UpdatePluginAsync(string pluginId,
        CancellationToken cancellationToken = default)
    {
        PositionalArgument.Ensure(pluginId, ToolName, nameof(pluginId));

        var reader = await RunAsync(
                ["plugin", "update", pluginId, ScopeOption, ClaudeInstalledPlugin.UserScope, JsonOption],
                cancellationToken)
            .ConfigureAwait(false);

        var resultObject = reader.ReadResultObject();

        return new ClaudePluginUpdateResult(
            reader.Fields.ReadOptionalString(resultObject, "updateOutcome", "result"),
            reader.Fields.ReadOptionalString(resultObject, "oldVersion", "result"),
            reader.Fields.ReadOptionalString(resultObject, "newVersion", "result"));
    }

    private async Task<ClaudeJsonOutputReader> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var result = await _claudeCommandRunner.RunAsync(_workingDirectory, arguments, cancellationToken)
            .ConfigureAwait(false);

        return new ClaudeJsonOutputReader(arguments, result);
    }

    private static ClaudeMarketplace ReadMarketplace(ClaudeJsonOutputReader reader, JsonElement entry,
        string entryName)
    {
        reader.Fields.EnsureObject(entry, entryName);

        return new ClaudeMarketplace(
            reader.Fields.ReadRequiredString(entry, "name", entryName),
            reader.Fields.ReadOptionalString(entry, "source", entryName),
            reader.Fields.ReadOptionalString(entry, "repo", entryName),
            reader.Fields.ReadOptionalString(entry, "ref", entryName),
            reader.Fields.ReadOptionalString(entry, "path", entryName));
    }

    private static ClaudeInstalledPlugin ReadInstalledPlugin(ClaudeJsonOutputReader reader, JsonElement entry,
        string entryName)
    {
        reader.Fields.EnsureObject(entry, entryName);

        return new ClaudeInstalledPlugin(
            reader.Fields.ReadRequiredString(entry, "id", entryName),
            reader.Fields.ReadOptionalString(entry, "version", entryName),
            reader.Fields.ReadOptionalString(entry, "scope", entryName),
            reader.Fields.ReadRequiredBoolean(entry, "enabled", entryName));
    }
}
