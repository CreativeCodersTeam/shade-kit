using System.Reflection;
using CreativeCoders.Cli.Core;
using CreativeCoders.Core;
using JetBrains.Annotations;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Prints the version of the SHADE kit CLI.
/// </summary>
/// <param name="ansiConsole">The console that receives the version output.</param>
[UsedImplicitly]
[CliCommand([Name], Description = "Shows the version of SHADE kit")]
public class VersionCommand(IAnsiConsole ansiConsole) : ICliCommand
{
    /// <summary>
    /// Represents the command name used on the command line.
    /// </summary>
    public const string Name = "version";

    private const string ToolName = "shade-kit";

    private readonly IAnsiConsole _ansiConsole = Ensure.NotNull(ansiConsole);

    /// <summary>
    /// Writes the tool name and the informational version of the CLI assembly to the console.
    /// </summary>
    /// <remarks>
    /// The version is taken from the <see cref="AssemblyInformationalVersionAttribute" /> of the CLI assembly without
    /// its build metadata, such as the source revision after the first <c>+</c> sign. If the attribute is missing or
    /// yields an empty version, the assembly version is used instead.
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation. The task result is <see cref="CommandResult.Success" />.</returns>
    public Task<CommandResult> ExecuteAsync()
    {
        _ansiConsole.WriteLine($"{ToolName} {GetVersion(typeof(VersionCommand).Assembly)}");

        return Task.FromResult(CommandResult.Success);
    }

    internal static string GetVersion(Assembly assembly)
    {
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+', 2)[0];

        return string.IsNullOrEmpty(version)
            ? assembly.GetName().Version?.ToString() ?? string.Empty
            : version;
    }
}
