using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Runs the plugin and marketplace commands of the OpenAI Codex CLI <c>codex</c> with the
/// <see cref="ICodexCommandRunner" /> and reads their output.
/// </summary>
/// <remarks>
/// <para>
/// <c>codex</c> runs in the home directory of the user (or the temporary directory if the home directory is not
/// known or does not exist), not in the current directory, so the settings of the current project take no part and no
/// file is written into a repository.
/// </para>
/// <para>
/// Every command must print one JSON object. In <c>codex plugin list --json</c> the array <c>installed</c> is needed,
/// and each of its entries needs the strings <c>pluginId</c>, <c>name</c>, and <c>marketplaceName</c> and the Booleans
/// <c>installed</c> and <c>enabled</c>; <c>version</c> is an optional string. In
/// <c>codex plugin marketplace list --json</c> the array <c>marketplaces</c> is needed, and each of its entries needs
/// the strings <c>name</c> and <c>root</c>; the optional object <c>marketplaceSource</c> needs the strings
/// <c>sourceType</c> and <c>source</c>. <c>codex plugin marketplace add --json</c> needs the strings
/// <c>marketplaceName</c> and <c>installedRoot</c> and the Boolean <c>alreadyAdded</c>. The output of the other
/// commands is checked to be one JSON object, but not read. A known field with a value of another type is a failure;
/// unknown fields are ignored.
/// </para>
/// <para>
/// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>, <c>codex-rs/cli/src/plugin_cmd.rs</c> and
/// <c>codex-rs/cli/src/marketplace_cmd.rs</c>): the shapes above are the serialized output types of these commands,
/// and the fields read as needed are never omitted. Unverified: none of it has been read from a real <c>codex</c>.
/// </para>
/// </remarks>
public sealed class CodexPluginCli : ICodexPluginCli
{
    private const string JsonOption = "--json";

    private const string NameProperty = "name";

    private readonly ICodexCommandRunner _codexCommandRunner;

    private readonly string _workingDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexPluginCli" /> class that runs <c>codex</c> in the home
    /// directory of the user.
    /// </summary>
    /// <param name="codexCommandRunner">The runner that starts <c>codex</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="codexCommandRunner" /> is <see langword="null" />.
    /// </exception>
    public CodexPluginCli(ICodexCommandRunner codexCommandRunner)
        : this(codexCommandRunner, UserWorkingDirectory.Get())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexPluginCli" /> class that runs <c>codex</c> in the specified
    /// directory.
    /// </summary>
    /// <param name="codexCommandRunner">The runner that starts <c>codex</c>.</param>
    /// <param name="workingDirectory">The directory in which <c>codex</c> runs.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="codexCommandRunner" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="workingDirectory" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    internal CodexPluginCli(ICodexCommandRunner codexCommandRunner, string workingDirectory)
    {
        _codexCommandRunner = Ensure.NotNull(codexCommandRunner);
        _workingDirectory = Ensure.IsNotNullOrWhitespace(workingDirectory);
    }

    /// <summary>
    /// Gets the directory in which <c>codex</c> runs.
    /// </summary>
    /// <value>
    /// The home directory of the user, the temporary directory, or the directory passed to the constructor.
    /// </value>
    internal string WorkingDirectory => _workingDirectory;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CodexInstalledPlugin>> ListPluginsAsync(
        CancellationToken cancellationToken = default)
    {
        var reader = await RunAsync(["plugin", "list", JsonOption], cancellationToken).ConfigureAwait(false);

        return reader.ReadObject(output => reader.ReadRequiredObjectArray(output, "installed",
            (entry, subject) => new CodexInstalledPlugin(
                reader.ReadRequiredString(entry, "pluginId", subject),
                reader.ReadRequiredString(entry, NameProperty, subject),
                reader.ReadRequiredString(entry, "marketplaceName", subject),
                reader.ReadOptionalString(entry, "version", subject),
                reader.ReadRequiredBoolean(entry, "installed", subject),
                reader.ReadRequiredBoolean(entry, "enabled", subject))));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CodexMarketplace>> ListMarketplacesAsync(
        CancellationToken cancellationToken = default)
    {
        var reader = await RunAsync(["plugin", "marketplace", "list", JsonOption], cancellationToken)
            .ConfigureAwait(false);

        return reader.ReadObject(output => reader.ReadRequiredObjectArray(output, "marketplaces",
            (entry, subject) => ReadMarketplace(reader, entry, subject)));
    }

    /// <inheritdoc />
    public async Task<CodexMarketplaceAddResult> AddMarketplaceAsync(MarketplaceSource source,
        CancellationToken cancellationToken = default)
    {
        var arguments = CreateAddMarketplaceArguments(Ensure.NotNull(source));

        var reader = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);

        return reader.ReadObject(output => new CodexMarketplaceAddResult(
            reader.ReadRequiredString(output, "marketplaceName", null),
            reader.ReadRequiredString(output, "installedRoot", null),
            reader.ReadRequiredBoolean(output, "alreadyAdded", null)));
    }

    /// <inheritdoc />
    public async Task UpgradeMarketplaceAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(name, nameof(name));

        await RunChangeAsync(["plugin", "marketplace", "upgrade", name, JsonOption], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveMarketplaceAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(name, nameof(name));

        await RunChangeAsync(["plugin", "marketplace", "remove", name, JsonOption], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(pluginId, nameof(pluginId));

        await RunChangeAsync(["plugin", "add", pluginId, JsonOption], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemovePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        EnsurePositionalArgument(pluginId, nameof(pluginId));

        await RunChangeAsync(["plugin", "remove", pluginId, JsonOption], cancellationToken).ConfigureAwait(false);
    }

    private static string[] CreateAddMarketplaceArguments(MarketplaceSource source)
    {
        if (source.Repo is { } repo)
        {
            if (source.Ref is not { } @ref)
            {
                return ["plugin", "marketplace", "add", repo, JsonOption];
            }

            if (@ref.StartsWith('-'))
            {
                throw new ArgumentException(
                    $"The ref '{@ref}' of the source starts with '-', so codex would read it as an option.",
                    nameof(source));
            }

            return ["plugin", "marketplace", "add", repo, "--ref", @ref, JsonOption];
        }

        // A MarketplaceSource is either a GitHub repository or a directory, so Path is set here.
        var path = source.Path!;
        string[] arguments = ["plugin", "marketplace", "add", path, JsonOption];

        // codex splits a source at its last '#' into source and ref, also a local path, and then refuses the ref.
        return path.Contains('#', StringComparison.Ordinal)
            ? throw new CodexCommandFailedException(arguments, null,
                $"The marketplace directory '{path}' contains the character '#', which codex reads as the start of " +
                "a Git ref, so it is not passed to codex.")
            : arguments;
    }

    private async Task RunChangeAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var reader = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);

        reader.EnsureObject();
    }

    private async Task<CodexJsonOutputReader> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var result = await _codexCommandRunner.RunAsync(_workingDirectory, arguments, cancellationToken)
            .ConfigureAwait(false);

        return new CodexJsonOutputReader(arguments, result);
    }

    private static CodexMarketplace ReadMarketplace(CodexJsonOutputReader reader, JsonElement entry, string subject)
    {
        var name = reader.ReadRequiredString(entry, NameProperty, subject);
        var root = reader.ReadRequiredString(entry, "root", subject);

        if (reader.ReadOptionalObject(entry, "marketplaceSource", subject) is not { } marketplaceSource)
        {
            return new CodexMarketplace(name, root, null, null);
        }

        var sourceSubject = $"'marketplaceSource' of the {subject}";

        return new CodexMarketplace(name, root,
            reader.ReadRequiredString(marketplaceSource, "sourceType", sourceSubject),
            reader.ReadRequiredString(marketplaceSource, "source", sourceSubject));
    }

    // codex would read a value that starts with '-' as an option instead of the positional argument.
    [SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
        Justification = "The caller's parameter name is forwarded on purpose, so an invalid value names the " +
                        "caller's parameter instead of this helper's.")]
    private static void EnsurePositionalArgument(string value, string paramName)
    {
        Ensure.IsNotNullOrWhitespace(value, paramName);

        if (value.StartsWith('-'))
        {
            throw new ArgumentException(
                $"The value '{value}' starts with '-', so codex would read it as an option.", paramName);
        }
    }
}
