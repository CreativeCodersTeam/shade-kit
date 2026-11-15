using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Commands.Integrations;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Versions;
using CreativeCoders.ShadeKit.Core.Warnings;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Prints the version of the SHADE kit engine, the SHADE kit integrations installed in the agents, and, in JSON mode,
/// the format version of every file type the engine supports, and checks the engine against the version that the kit
/// skills expect if one is given.
/// </summary>
/// <param name="context">The context that runs the version check and writes the result or the failure.</param>
/// <param name="engineVersionProvider">The provider of the running engine version.</param>
/// <param name="formatRegistry">The registry of the file types the engine supports.</param>
/// <param name="engineVersionChecker">The checker that compares the engine version with the expected version.</param>
/// <param name="integrationService">The service that reads the installed integrations.</param>
[UsedImplicitly]
[CliCommand([Name], Description = "Shows the version of SHADE kit")]
public class VersionCommand(
    ShadeKitCommandContext context,
    IEngineVersionProvider engineVersionProvider,
    IFormatRegistry formatRegistry,
    IEngineVersionChecker engineVersionChecker,
    IIntegrationService integrationService)
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

    private readonly IIntegrationService _integrationService = Ensure.NotNull(integrationService);

    /// <summary>
    /// Checks the engine against the expected version if <see cref="VersionCommandOptions.Expect" /> is set, reads the
    /// installed integrations, and writes the engine version and the integrations, as plain text or as a JSON
    /// envelope.
    /// </summary>
    /// <remarks>
    /// A prerelease engine that does not fit the expected version is reported with a warning and still succeeds. The
    /// integrations are read only after the check succeeded; an agent whose CLI is missing or fails is left out with
    /// an <c>agent-unavailable</c> warning, so the integrations never make the command fail. The plain output starts
    /// with the line <c>shade-kit &lt;version&gt;</c>, followed by one line per installed integration.
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
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(VersionCommandOptions options)
    {
        Ensure.NotNull(options);

        var data = CreateData();
        IReadOnlyList<KitWarning> checkWarnings = [];

        if (options.Expect is { } expectedVersion)
        {
            var result = await _context.RunAsync(options.Json,
                    _ => Task.FromResult(_engineVersionChecker.Check(expectedVersion)))
                .ConfigureAwait(false);

            checkWarnings = result.Warnings;
            data = data with { ExpectedVersion = expectedVersion };
        }

        var installed = await _context.RunAsync(options.Json,
                cancellationToken => _integrationService.GetInstalledAsync(cancellationToken))
            .ConfigureAwait(false);

        var completeData = data with
        {
            Integrations = [.. installed.Integrations.Select(VersionCommandIntegrationData.From)]
        };

        _context.WriteSuccess(options.Json, completeData, [.. checkWarnings, .. installed.Warnings],
            () => WritePlain(completeData.EngineVersion, installed.Integrations));

        return CommandResult.Success;
    }

    private void WritePlain(string engineVersion, IReadOnlyList<InstalledIntegration> integrations)
    {
        _context.Console.WriteLine($"{ToolName} {engineVersion}");

        foreach (var integration in integrations)
        {
            _context.Console.MarkupLine(IntegrationsTextFormat.FormatInstalled(integration));
        }
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
