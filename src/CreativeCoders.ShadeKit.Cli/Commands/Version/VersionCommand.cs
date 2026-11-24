using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Commands.Integrations;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Versions;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Prints the version of the SHADE kit engine and the CLI contracts it supports, in JSON mode also the format version
/// of every file type the engine supports, and either checks the CLI contract that the kit skills are written for or
/// lists the SHADE kit integrations installed in the agents.
/// </summary>
/// <param name="context">The context that runs the contract check and writes the result or the failure.</param>
/// <param name="engineVersionProvider">The provider of the running engine version.</param>
/// <param name="formatRegistry">The registry of the file types the engine supports.</param>
/// <param name="cliContractChecker">
/// The checker that compares the CLI contract of the kit skills with the contract window of the engine; its
/// <see cref="ICliContractChecker.Window" /> is also the window that the command reports.
/// </param>
/// <param name="integrationService">The service that reads the installed integrations.</param>
[UsedImplicitly]
[CliCommand([Name], Description = "Shows the version of SHADE kit")]
public class VersionCommand(
    ShadeKitCommandContext context,
    IEngineVersionProvider engineVersionProvider,
    IFormatRegistry formatRegistry,
    ICliContractChecker cliContractChecker,
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

    private readonly ICliContractChecker _cliContractChecker = Ensure.NotNull(cliContractChecker);

    private readonly IIntegrationService _integrationService = Ensure.NotNull(integrationService);

    /// <summary>
    /// Checks the CLI contract of the kit skills if <see cref="VersionCommandOptions.ExpectContract" /> is set, or
    /// reads the installed integrations otherwise, and writes the engine version, the CLI contract window, and the
    /// result, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With <see cref="VersionCommandOptions.ExpectContract" /> the command is the handshake of the kit skills: it
    /// checks the contract against the window of the engine, echoes it as <c>expectedContract</c>, and does not read
    /// the integrations, so <c>integrations</c> is left out and no agent CLI is started. A contract outside the window
    /// always fails; there is no leniency for prerelease engines.
    /// </para>
    /// <para>
    /// Without it, the command reads the installed integrations; an agent whose CLI is missing or fails is left out
    /// with an <c>agent-unavailable</c> warning, so the integrations never make the command fail.
    /// </para>
    /// <para>
    /// The plain output starts with the line <c>shade-kit &lt;version&gt;</c>, followed by the line
    /// <c>CLI contract &lt;current&gt; (supported: &lt;min&gt; to &lt;current&gt;)</c> and, without
    /// <see cref="VersionCommandOptions.ExpectContract" />, one line per installed integration.
    /// </para>
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// The kit skills need a CLI contract newer than the engine supports (exit code
    /// <see cref="ShadeKitCliExitCodes.EngineOutdated" />), or they are written for a CLI contract older than the
    /// engine supports (exit code <see cref="ShadeKitCliExitCodes.SkillsOutdated" />).
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(VersionCommandOptions options)
    {
        Ensure.NotNull(options);

        var data = CreateData();

        if (options.ExpectContract is { } expectedContract)
        {
            await WriteCheckedContractAsync(options.Json, data, expectedContract).ConfigureAwait(false);
        }
        else
        {
            await WriteInstalledIntegrationsAsync(options.Json, data).ConfigureAwait(false);
        }

        return CommandResult.Success;
    }

    // The handshake path: the integrations are not read, so no agent CLI is started and data.Integrations stays null.
    private async Task WriteCheckedContractAsync(bool json, VersionCommandData data, int expectedContract)
    {
        var checkedContract = await _context.RunAsync(json, _ =>
            {
                _cliContractChecker.Check(expectedContract);

                return Task.FromResult(expectedContract);
            })
            .ConfigureAwait(false);

        var checkedData = data with { ExpectedContract = checkedContract };

        _context.WriteSuccess(json, checkedData, () => WritePlain(checkedData, []));
    }

    private async Task WriteInstalledIntegrationsAsync(bool json, VersionCommandData data)
    {
        var installed = await _context.RunAsync(json,
                cancellationToken => _integrationService.GetInstalledAsync(cancellationToken))
            .ConfigureAwait(false);

        var completeData = data with
        {
            Integrations = [.. installed.Integrations.Select(VersionCommandIntegrationData.From)]
        };

        _context.WriteSuccess(json, completeData, installed.Warnings,
            () => WritePlain(completeData, installed.Integrations));
    }

    private void WritePlain(VersionCommandData data, IReadOnlyList<InstalledIntegration> integrations)
    {
        _context.Console.WriteLine($"{ToolName} {data.EngineVersion}");
        _context.Console.WriteLine(
            $"CLI contract {data.Contract.Current} (supported: {data.Contract.MinSupported} to {data.Contract.Current})");

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

        return new VersionCommandData(_engineVersionProvider.Version, formats,
            VersionCommandContractData.From(_cliContractChecker.Window));
    }
}
