using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Copilot;

/// <summary>
/// Runs the plugin and marketplace commands of the GitHub Copilot CLI <c>copilot</c> with the
/// <see cref="ICopilotCommandRunner" /> and reads their output.
/// </summary>
/// <remarks>
/// <para>
/// <c>copilot</c> runs in the home directory of the user (or the temporary directory if the home directory is not
/// known or does not exist), not in the current directory, so the settings of the current project take no part and
/// no file is written into a repository.
/// </para>
/// <para>
/// A list command must print one JSON array whose entries are objects. A plugin entry needs the string <c>name</c>
/// and the Boolean <c>enabled</c>; <c>marketplace</c> and <c>version</c> are optional strings. <c>source</c> and
/// <c>installedFrom</c> are not read, because their types are not documented. A marketplace entry needs the string
/// <c>name</c>; its source is read from the field <c>source</c>: a JSON object with the kind in <c>source</c> (or
/// <c>type</c>) and the optional strings <c>repo</c>, <c>ref</c>, and <c>path</c>, or a string with the kind and the
/// optional strings <c>repo</c>, <c>ref</c>, and <c>path</c> next to it in the entry, as Claude Code reports them. A
/// known field with a value of another type is a failure; unknown fields are ignored.
/// </para>
/// <para>
/// Unverified: the marketplace entry shape is an assumption, because the GitHub Docs do not document the output of
/// <c>copilot plugin marketplace list --json</c>; the plugin entry shape follows the GitHub Docs plugin reference.
/// </para>
/// </remarks>
public sealed class CopilotPluginCli : ICopilotPluginCli
{
    private const string JsonOption = "--json";

    private const string SourceProperty = "source";

    private readonly ICopilotCommandRunner _copilotCommandRunner;

    private readonly string _workingDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotPluginCli" /> class that runs <c>copilot</c> in the home
    /// directory of the user.
    /// </summary>
    /// <param name="copilotCommandRunner">The runner that starts <c>copilot</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="copilotCommandRunner" /> is <see langword="null" />.
    /// </exception>
    public CopilotPluginCli(ICopilotCommandRunner copilotCommandRunner)
        : this(copilotCommandRunner, UserWorkingDirectory.Get())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotPluginCli" /> class that runs <c>copilot</c> in the
    /// specified directory.
    /// </summary>
    /// <param name="copilotCommandRunner">The runner that starts <c>copilot</c>.</param>
    /// <param name="workingDirectory">The directory in which <c>copilot</c> runs.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="copilotCommandRunner" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="workingDirectory" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    internal CopilotPluginCli(ICopilotCommandRunner copilotCommandRunner, string workingDirectory)
    {
        _copilotCommandRunner = Ensure.NotNull(copilotCommandRunner);
        _workingDirectory = Ensure.IsNotNullOrWhitespace(workingDirectory);
    }

    /// <summary>
    /// Gets the directory in which <c>copilot</c> runs.
    /// </summary>
    /// <value>
    /// The home directory of the user, the temporary directory, or the directory passed to the constructor.
    /// </value>
    internal string WorkingDirectory => _workingDirectory;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CopilotMarketplace>> ListMarketplacesAsync(
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

        await RunChangeAsync(["plugin", "marketplace", "add", source], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveMarketplaceAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(name, nameof(name));

        await RunChangeAsync(["plugin", "marketplace", "remove", name], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateMarketplaceAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(name, nameof(name));

        await RunChangeAsync(["plugin", "marketplace", "update", name], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CopilotInstalledPlugin>> ListPluginsAsync(
        CancellationToken cancellationToken = default)
    {
        var reader = await RunAsync(["plugin", "list", JsonOption], cancellationToken).ConfigureAwait(false);

        return reader.ReadArray((entry, entryName) => ReadInstalledPlugin(reader, entry, entryName));
    }

    /// <inheritdoc />
    public async Task InstallPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(pluginId, nameof(pluginId));

        await RunChangeAsync(["plugin", "install", pluginId], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdatePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(pluginId, nameof(pluginId));

        await RunChangeAsync(["plugin", "update", pluginId], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UninstallPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(pluginId, nameof(pluginId));

        await RunChangeAsync(["plugin", "uninstall", pluginId], cancellationToken).ConfigureAwait(false);
    }

    private async Task RunChangeAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var reader = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);

        reader.EnsureSuccess();
    }

    private async Task<CopilotJsonOutputReader> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var result = await _copilotCommandRunner.RunAsync(_workingDirectory, arguments, cancellationToken)
            .ConfigureAwait(false);

        return new CopilotJsonOutputReader(arguments, result);
    }

    private static CopilotMarketplace ReadMarketplace(CopilotJsonOutputReader reader, JsonElement entry,
        string entryName)
    {
        reader.EnsureObject(entry, entryName);

        var name = reader.ReadRequiredString(entry, "name", entryName);

        // The shape is not documented: the source is either an object (as in the marketplace settings) or a kind
        // string with the details next to it (as Claude Code reports it).
        if (reader.ReadOptionalObjectOrString(entry, SourceProperty, entryName) is { } source)
        {
            var subject = $"source of the {entryName}";

            return new CopilotMarketplace(name,
                reader.ReadOptionalString(source, SourceProperty, subject)
                ?? reader.ReadOptionalString(source, "type", subject),
                reader.ReadOptionalString(source, "repo", subject),
                reader.ReadOptionalString(source, "ref", subject),
                reader.ReadOptionalString(source, "path", subject));
        }

        return new CopilotMarketplace(name,
            reader.ReadOptionalString(entry, SourceProperty, entryName),
            reader.ReadOptionalString(entry, "repo", entryName),
            reader.ReadOptionalString(entry, "ref", entryName),
            reader.ReadOptionalString(entry, "path", entryName));
    }

    private static CopilotInstalledPlugin ReadInstalledPlugin(CopilotJsonOutputReader reader, JsonElement entry,
        string entryName)
    {
        reader.EnsureObject(entry, entryName);

        return new CopilotInstalledPlugin(
            reader.ReadRequiredString(entry, "name", entryName),
            reader.ReadOptionalString(entry, "marketplace", entryName),
            reader.ReadOptionalString(entry, "version", entryName),
            reader.ReadRequiredBoolean(entry, "enabled", entryName));
    }

    // copilot would read a value that starts with '-' as an option instead of the positional argument.
    [SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
        Justification = "The caller's parameter name is forwarded on purpose, so an invalid value names the " +
                        "caller's parameter instead of this helper's.")]
    private static void EnsurePositionalArgument(string value, string paramName)
    {
        Ensure.IsNotNullOrWhitespace(value, paramName);

        if (value.StartsWith('-'))
        {
            throw new ArgumentException(
                $"The value '{value}' starts with '-', so copilot would read it as an option.", paramName);
        }
    }
}
