using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Copilot;

/// <summary>
/// Runs the GitHub Copilot CLI executable <c>copilot</c> as a child process and captures its output.
/// </summary>
/// <remarks>
/// <para>
/// Starts only the absolute path from the <see cref="ICopilotExecutableLocator" />, never a <c>copilot</c> in the
/// current directory; the output is read concurrently and decoded as UTF-8. The process inherits the environment of
/// the current process plus <see cref="EnvironmentVariables" />.
/// </para>
/// <para>
/// An <c>.exe</c> or Unix executable is started without a shell. A Windows <c>copilot.cmd</c> shim of an npm
/// installation is started through <c>cmd.exe</c>, because only <c>cmd.exe</c> can run it; a shim path or an argument
/// with a character that <c>cmd.exe</c> would interpret is refused with a <see cref="CopilotCommandFailedException" />
/// before anything is started (see <see cref="ExternalTool.CmdShims" />). Unverified: the shim
/// start has not been run on Windows.
/// </para>
/// </remarks>
/// <param name="copilotExecutableLocator">
/// The locator that resolves the absolute path of the <c>copilot</c> executable.
/// </param>
public sealed class CopilotCommandRunner(ICopilotExecutableLocator copilotExecutableLocator) : ICopilotCommandRunner
{
    private readonly ICopilotExecutableLocator _copilotExecutableLocator = Ensure.NotNull(copilotExecutableLocator);

    /// <summary>
    /// Gets the environment variables that are set for every <c>copilot</c> process in addition to the inherited ones.
    /// </summary>
    /// <value>
    /// <c>NO_COLOR=1</c>, the common convention to turn colors off. Unverified: that the GitHub Copilot CLI evaluates
    /// it; the output that SHADE kit reads is JSON or a plain message, so colors would only reach failure messages.
    /// </value>
    internal static IReadOnlyDictionary<string, string> EnvironmentVariables { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NO_COLOR"] = "1"
        };

    /// <summary>
    /// Gets the description of the <c>copilot</c> tool that the <see cref="ExternalToolRunner" /> starts.
    /// </summary>
    /// <value>
    /// The tool with the name <c>copilot</c>, the <see cref="EnvironmentVariables" />, and the copilot exceptions;
    /// it runs Windows <c>.cmd</c> shims, and a refused shim argument is a <see cref="CopilotCommandFailedException" />
    /// without an exit code.
    /// </value>
    /// <remarks>
    /// Declared after <see cref="EnvironmentVariables" />, because static initializers run in the order of
    /// declaration.
    /// </remarks>
    internal static ExternalTool Tool { get; } = new("copilot", "The GitHub Copilot CLI copilot",
        EnvironmentVariables,
        static (arguments, reason) => new CopilotNotFoundException(arguments, reason),
        static (arguments, reason, innerException) =>
            new CopilotUnavailableException(arguments, reason, innerException),
        new CmdShimPolicy(static (arguments, reason) => new CopilotCommandFailedException(arguments, null, reason)));

    /// <inheritdoc />
    public Task<ExternalProcessResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return ExternalToolRunner.RunAsync(Tool, _copilotExecutableLocator.FindCopilotExecutable, workingDirectory,
            arguments, cancellationToken);
    }
}
