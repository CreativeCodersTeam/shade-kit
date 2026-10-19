using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Store;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Init;

/// <summary>
/// Initialises SHADE kit in the git repository that contains the current directory.
/// </summary>
/// <param name="ansiConsole">The console that receives the plain summary.</param>
/// <param name="repositoryInitializer">The initializer that writes the SHADE kit files into the repository.</param>
/// <param name="jsonWriter">The writer that receives the JSON output.</param>
/// <param name="errorMapper">
/// The mapper that turns initialisation failures into exit codes, error codes, and messages.
/// </param>
[UsedImplicitly]
[CliCommand([Name], Description = "Initialises SHADE kit in the current git repository")]
public class InitCommand(
    IAnsiConsole ansiConsole,
    IRepositoryInitializer repositoryInitializer,
    ICliJsonWriter jsonWriter,
    CliErrorMapper errorMapper)
    : ICliCommand<InitCommandOptions>
{
    /// <summary>
    /// Represents the command name used on the command line.
    /// </summary>
    public const string Name = "init";

    private readonly IAnsiConsole _ansiConsole = Ensure.NotNull(ansiConsole);

    private readonly IRepositoryInitializer _repositoryInitializer = Ensure.NotNull(repositoryInitializer);

    private readonly ICliJsonWriter _jsonWriter = Ensure.NotNull(jsonWriter);

    private readonly CliErrorMapper _errorMapper = Ensure.NotNull(errorMapper);

    /// <summary>
    /// Initialises the repository and writes what was created or changed, as plain text or as a JSON envelope.
    /// </summary>
    /// <remarks>
    /// The initialisation works at the root of the git repository that contains the current directory. Plain output is
    /// the line <c>Initialised SHADE kit in &lt;root&gt;.</c>, one line <c>&lt;action&gt;  &lt;path&gt;</c> per file,
    /// and a hint to commit the files. JSON output is an envelope whose <c>data</c> is an
    /// <see cref="InitCommandData" />. In JSON mode, an expected failure is written as an error envelope and the abort
    /// message is not printed.
    /// </remarks>
    /// <param name="options">The options of the command.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// The current directory is not inside a git repository (exit code
    /// <see cref="ShadeKitCliExitCodes.NotAGitRepository" />), the repository is already initialised (exit code
    /// <see cref="ShadeKitCliExitCodes.KitAlreadyInitialized" />), or the managed section markers in <c>CLAUDE.md</c>
    /// or <c>AGENTS.md</c> are invalid (exit code <see cref="ShadeKitCliExitCodes.InvalidManagedSection" />).
    /// </exception>
    public async Task<CommandResult> ExecuteAsync(InitCommandOptions options)
    {
        Ensure.NotNull(options);

        var result = await CliCommandAbort.RunAsync(
                () => _repositoryInitializer.InitializeAsync(Directory.GetCurrentDirectory()), _errorMapper,
                options.Json, _jsonWriter)
            .ConfigureAwait(false);
        var data = InitCommandData.FromResult(result);

        if (options.Json)
        {
            _jsonWriter.Write(CliJsonEnvelope.Success(data));
        }
        else
        {
            WriteSummary(data);
        }

        return CommandResult.Success;
    }

    private void WriteSummary(InitCommandData data)
    {
        _ansiConsole.MarkupLine($"Initialised SHADE kit in {Markup.Escape(data.RepositoryRoot)}.");

        foreach (var file in data.Files)
        {
            _ansiConsole.MarkupLine(
                $"{Markup.Escape(CliJsonWriter.GetEnumName(file.Action))}  {Markup.Escape(file.Path)}");
        }

        _ansiConsole.MarkupLine(
            $"Commit {Markup.Escape($"'{KitStorePaths.RootPath}/'")} and the changed files together with your code.");
    }
}
