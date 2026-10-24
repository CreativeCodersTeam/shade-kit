using CreativeCoders.Cli.Core;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Prints the version of the SHADE kit engine and, in JSON mode, the format version of every file type it supports.
/// </summary>
/// <param name="ansiConsole">The console that receives the plain version output.</param>
/// <param name="engineVersionProvider">The provider of the running engine version.</param>
/// <param name="formatRegistry">The registry of the file types the engine supports.</param>
/// <param name="jsonWriter">The writer that receives the JSON output.</param>
[UsedImplicitly]
[CliCommand([Name], Description = "Shows the version of SHADE kit")]
public class VersionCommand(
    IAnsiConsole ansiConsole,
    IEngineVersionProvider engineVersionProvider,
    IFormatRegistry formatRegistry,
    ICliJsonWriter jsonWriter)
    : ICliCommand<VersionCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line.
    /// </summary>
    public const string Name = "version";

    private const string ToolName = "shade-kit";

    private readonly IAnsiConsole _ansiConsole = Ensure.NotNull(ansiConsole);

    private readonly IEngineVersionProvider _engineVersionProvider = Ensure.NotNull(engineVersionProvider);

    private readonly IFormatRegistry _formatRegistry = Ensure.NotNull(formatRegistry);

    private readonly ICliJsonWriter _jsonWriter = Ensure.NotNull(jsonWriter);

    /// <summary>
    /// Writes the engine version, as plain text or as a JSON envelope.
    /// </summary>
    /// <param name="options">The options of the command.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    public Task<CommandResult> ExecuteAsync(VersionCommandOptions options)
    {
        Ensure.NotNull(options);

        if (options.Json)
        {
            _jsonWriter.Write(CliJsonEnvelope.Success(CreateData()));
        }
        else
        {
            _ansiConsole.WriteLine($"{ToolName} {_engineVersionProvider.Version}");
        }

        return Task.FromResult(CommandResult.Success);
    }

    private VersionCommandData CreateData()
    {
        var formats = new OrderedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var format in _formatRegistry.Formats)
        {
            formats.Add(format.Id, format.CurrentVersion);
        }

        return new VersionCommandData(_engineVersionProvider.Version, formats);
    }
}
