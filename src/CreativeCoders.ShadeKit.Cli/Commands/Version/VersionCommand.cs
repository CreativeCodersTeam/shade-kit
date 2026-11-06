using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Versions;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Prints the version of the SHADE kit engine and, in JSON mode, the format version of every file type it supports,
/// and checks the engine against the version that the kit skills expect if one is given.
/// </summary>
/// <param name="context">The context that runs the version check and writes the result or the failure.</param>
/// <param name="engineVersionProvider">The provider of the running engine version.</param>
/// <param name="formatRegistry">The registry of the file types the engine supports.</param>
/// <param name="engineVersionChecker">The checker that compares the engine version with the expected version.</param>
[UsedImplicitly]
[CliCommand([Name], Description = "Shows the version of SHADE kit")]
public class VersionCommand(
    ShadeKitCommandContext context,
    IEngineVersionProvider engineVersionProvider,
    IFormatRegistry formatRegistry,
    IEngineVersionChecker engineVersionChecker)
    : ICliCommand<VersionCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line.
    /// </summary>
    public const string Name = "version";

    private const string ToolName = "shade-kit";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IEngineVersionProvider _engineVersionProvider = Ensure.NotNull(engineVersionProvider);

    private readonly IFormatRegistry _formatRegistry = Ensure.NotNull(formatRegistry);

    private readonly IEngineVersionChecker _engineVersionChecker = Ensure.NotNull(engineVersionChecker);

    /// <summary>
    /// Checks the engine against the expected version if <see cref="VersionCommandOptions.Expect" /> is set, and
    /// writes the engine version, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// A prerelease engine that does not fit the expected version is reported with a warning and still succeeds.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// The engine is a release whose major or minor version differs from that of the expected version (exit code
    /// <see cref="ShadeKitCliExitCodes.VersionMismatch" />), or the expected version is not a valid SemVer 2.0.0
    /// version (exit code <see cref="ShadeKitCliExitCodes.InvalidArguments" />).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The version of the running engine is not a valid SemVer 2.0.0 version, which is a defect of the engine build.
    /// </exception>
    public async Task<CommandResult> ExecuteAsync(VersionCommandOptions options)
    {
        Ensure.NotNull(options);

        var data = CreateData();

        if (options.Expect is not { } expectedVersion)
        {
            _context.WriteSuccess(options.Json, data, () => WritePlain(data));

            return CommandResult.Success;
        }

        var result = await _context.RunAsync(options.Json,
                _ => Task.FromResult(_engineVersionChecker.Check(expectedVersion)))
            .ConfigureAwait(false);

        var checkedData = data with { ExpectedVersion = expectedVersion };

        _context.WriteSuccess(options.Json, checkedData, result.Warnings, () => WritePlain(checkedData));

        return CommandResult.Success;
    }

    private void WritePlain(VersionCommandData data)
    {
        _context.Console.WriteLine($"{ToolName} {data.EngineVersion}");
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
