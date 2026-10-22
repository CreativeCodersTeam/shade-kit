using System.Diagnostics.CodeAnalysis;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using JetBrains.Annotations;
using Spectre.Console;

[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace",
    Target = "~N:CreativeCoders.ShadeKit.Cli.Commands.Feature.New",
    Justification = "The namespace follows the command folder Commands/<Group>/<Command>/; the CLI assembly is "
                    + "an application and is not consumed from Visual Basic.")]

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.New;

/// <summary>
/// Creates a new feature in the SHADE kit store of the current repository and makes it the current feature.
/// </summary>
/// <param name="context">The context that runs the core operation and writes the result or the failure.</param>
/// <param name="featureStore">The store that creates the feature folder and its files.</param>
[UsedImplicitly]
[CliCommand([FeatureCommandGroup.Name, Name], Description = "Creates a new feature and makes it the current feature")]
public class FeatureNewCommand(
    ShadeKitCommandContext context,
    IFeatureStore featureStore)
    : ICliCommand<FeatureNewCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line within the <see cref="FeatureCommandGroup" />.
    /// </summary>
    public const string Name = "new";

    private readonly ShadeKitCommandContext _context = Ensure.NotNull(context);

    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    /// <summary>
    /// Creates the feature and writes its id, paths, and suggested branch, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// A relative file path in <see cref="FeatureNewCommandOptions.Source" /> is resolved against the current
    /// directory. Plain output is one line <c>Warning: &lt;message&gt;</c> per warning, the line
    /// <c>Created feature &lt;id&gt; (&lt;profile&gt;).</c>, one indented line each for the paths of
    /// <c>feature.json</c> and <c>request.md</c>, and the line <c>Suggested branch: feature/&lt;id&gt;</c>. JSON output
    /// is an envelope whose <c>data</c> is a <see cref="FeatureNewCommandData" /> and whose <c>warnings</c> contain the
    /// format warnings of the files read. In JSON mode, an expected failure is written as an error envelope and the
    /// abort message is not printed.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found (exit codes <see cref="ShadeKitCliExitCodes.NotAGitRepository" />,
    /// <see cref="ShadeKitCliExitCodes.KitRootNotFound" />, or <see cref="ShadeKitCliExitCodes.KitRootNotADirectory" />);
    /// the feature exists already (exit code <see cref="ShadeKitCliExitCodes.FeatureAlreadyExists" />); the source is
    /// empty or a tracker URL without an issue number (exit code <see cref="ShadeKitCliExitCodes.InvalidFeatureSource" />);
    /// no usable title is given or derived (exit code <see cref="ShadeKitCliExitCodes.InvalidFeatureTitle" />); the
    /// profile is unknown (exit code <see cref="ShadeKitCliExitCodes.UnknownProfile" />); the git identity is not set
    /// (exit code <see cref="ShadeKitCliExitCodes.GitIdentityMissing" />); <c>git</c> cannot be run (exit code
    /// <see cref="ShadeKitCliExitCodes.GitUnavailable" />); or a kit file is invalid or too new (exit codes
    /// <see cref="ShadeKitCliExitCodes.InvalidFile" /> and <see cref="ShadeKitCliExitCodes.FormatTooNew" />).
    /// </exception>
    /// <exception cref="OperationCanceledException">The user canceled the run.</exception>
    public async Task<CommandResult> ExecuteAsync(FeatureNewCommandOptions options)
    {
        Ensure.NotNull(options);

        var request = new NewFeatureRequest(options.Source, options.Title, options.Profile,
            Directory.GetCurrentDirectory());

        var result = await _context.RunAsync(options.Json,
                (store, cancellationToken) => _featureStore.CreateAsync(store, request, cancellationToken))
            .ConfigureAwait(false);

        var data = FeatureNewCommandData.FromResult(result);

        _context.WriteSuccess(options.Json, data, result.Warnings, () => WriteSummary(data));

        return CommandResult.Success;
    }

    private void WriteSummary(FeatureNewCommandData data)
    {
        _context.Console.WriteLine($"Created feature {data.Id} ({data.Profile}).");
        _context.Console.WriteLine($"  {data.Paths.FeatureFile}");
        _context.Console.WriteLine($"  {data.Paths.Request}");
        _context.Console.WriteLine($"Suggested branch: {data.SuggestedBranch}");
    }
}
