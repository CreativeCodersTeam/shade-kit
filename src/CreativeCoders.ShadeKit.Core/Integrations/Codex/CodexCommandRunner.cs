using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Runs the OpenAI Codex CLI executable <c>codex</c> as a child process and captures its output.
/// </summary>
/// <remarks>
/// <para>
/// Starts only the absolute path from the <see cref="ICodexExecutableLocator" />, never a <c>codex</c> in the current
/// directory; the output is read concurrently and decoded as UTF-8. The process inherits the environment of the
/// current process plus <see cref="EnvironmentVariables" />.
/// </para>
/// <para>
/// An <c>.exe</c> or Unix executable is started without a shell. A Windows <c>codex.cmd</c> shim of an npm
/// installation is started through <c>cmd.exe</c>, because only <c>cmd.exe</c> can run it; a shim path or an argument
/// with a character that <c>cmd.exe</c> would interpret is refused with a <see cref="CodexCommandFailedException" />
/// before anything is started (see <see cref="ExternalTool.CreateCommandFailedException" />). Unverified: the shim
/// start has not been run on Windows.
/// </para>
/// </remarks>
/// <param name="codexExecutableLocator">
/// The locator that resolves the absolute path of the <c>codex</c> executable.
/// </param>
public sealed class CodexCommandRunner(ICodexExecutableLocator codexExecutableLocator) : ICodexCommandRunner
{
    private readonly ICodexExecutableLocator _codexExecutableLocator = Ensure.NotNull(codexExecutableLocator);

    /// <summary>
    /// Gets the environment variables that are set for every <c>codex</c> process in addition to the inherited ones.
    /// </summary>
    /// <value>
    /// <c>NO_COLOR=1</c>, the common convention to turn colors off. Unverified: that the <c>codex plugin</c> commands
    /// evaluate it; the output that SHADE kit reads is JSON or a plain message, so colors would only reach failure
    /// messages.
    /// </value>
    internal static IReadOnlyDictionary<string, string> EnvironmentVariables { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NO_COLOR"] = "1"
        };

    /// <summary>
    /// Gets the description of the <c>codex</c> tool that the <see cref="ExternalToolRunner" /> starts.
    /// </summary>
    /// <value>
    /// The tool with the name <c>codex</c>, the <see cref="EnvironmentVariables" />, and the codex exceptions; it runs
    /// Windows <c>.cmd</c> shims, and a refused shim argument is a <see cref="CodexCommandFailedException" /> without
    /// an exit code.
    /// </value>
    /// <remarks>
    /// Declared after <see cref="EnvironmentVariables" />, because static initializers run in the order of
    /// declaration.
    /// </remarks>
    internal static ExternalTool Tool { get; } = new("codex", "The OpenAI Codex CLI codex",
        EnvironmentVariables,
        static (arguments, reason) => new CodexNotFoundException(arguments, reason),
        static (arguments, reason, innerException) =>
            new CodexUnavailableException(arguments, reason, innerException),
        static (arguments, reason) => new CodexCommandFailedException(arguments, null, reason));

    /// <inheritdoc />
    public Task<ExternalProcessResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return ExternalToolRunner.RunAsync(Tool, _codexExecutableLocator.FindCodexExecutable, workingDirectory,
            arguments, cancellationToken);
    }
}
