using System.Diagnostics.CodeAnalysis;
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
        EnsurePositionalArgument(source, nameof(source));

        var reader = await RunAsync(
                ["plugin", "marketplace", "add", source, ScopeOption, ClaudeInstalledPlugin.UserScope, JsonOption],
                cancellationToken)
            .ConfigureAwait(false);

        reader.ReadResultObject();
    }

    /// <inheritdoc />
    public async Task RemoveMarketplaceAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(name, nameof(name));

        var reader = await RunAsync(["plugin", "marketplace", "remove", name, JsonOption], cancellationToken)
            .ConfigureAwait(false);

        reader.ReadResultObject();
    }

    /// <inheritdoc />
    public async Task UpdateMarketplaceAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(name, nameof(name));

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
        EnsurePositionalArgument(pluginId, nameof(pluginId));

        var reader = await RunAsync(
                ["plugin", "install", pluginId, ScopeOption, ClaudeInstalledPlugin.UserScope, JsonOption],
                cancellationToken)
            .ConfigureAwait(false);

        var resultObject = reader.ReadResultObject();

        return new ClaudePluginInstallResult(
            reader.ReadOptionalString(resultObject, "installedVersion", "result"),
            reader.ReadOptionalString(resultObject, "availableVersion", "result"));
    }

    /// <inheritdoc />
    public async Task<ClaudePluginUpdateResult> UpdatePluginAsync(string pluginId,
        CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(pluginId, nameof(pluginId));

        var reader = await RunAsync(
                ["plugin", "update", pluginId, ScopeOption, ClaudeInstalledPlugin.UserScope, JsonOption],
                cancellationToken)
            .ConfigureAwait(false);

        var resultObject = reader.ReadResultObject();

        return new ClaudePluginUpdateResult(
            reader.ReadOptionalString(resultObject, "updateOutcome", "result"),
            reader.ReadOptionalString(resultObject, "oldVersion", "result"),
            reader.ReadOptionalString(resultObject, "newVersion", "result"));
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
        reader.EnsureObject(entry, entryName);

        return new ClaudeMarketplace(
            reader.ReadRequiredString(entry, "name", entryName),
            reader.ReadOptionalString(entry, "source", entryName),
            reader.ReadOptionalString(entry, "repo", entryName),
            reader.ReadOptionalString(entry, "ref", entryName),
            reader.ReadOptionalString(entry, "path", entryName));
    }

    private static ClaudeInstalledPlugin ReadInstalledPlugin(ClaudeJsonOutputReader reader, JsonElement entry,
        string entryName)
    {
        reader.EnsureObject(entry, entryName);

        return new ClaudeInstalledPlugin(
            reader.ReadRequiredString(entry, "id", entryName),
            reader.ReadOptionalString(entry, "version", entryName),
            reader.ReadOptionalString(entry, "scope", entryName),
            reader.ReadRequiredBoolean(entry, "enabled", entryName));
    }

    // claude would read a value that starts with '-' as an option instead of the positional argument.
    [SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
        Justification = "The caller's parameter name is forwarded on purpose, so an invalid value names the " +
                        "caller's parameter instead of this helper's.")]
    private static void EnsurePositionalArgument(string value, string paramName)
    {
        Ensure.IsNotNullOrWhitespace(value, paramName);

        if (value.StartsWith('-'))
        {
            throw new ArgumentException($"The value '{value}' starts with '-', so claude would read it as an option.",
                paramName);
        }
    }
}
