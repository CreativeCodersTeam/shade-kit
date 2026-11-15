using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Runs the Claude Code CLI executable <c>claude</c> as a child process and captures its output.
/// </summary>
/// <remarks>
/// Starts only the absolute path from the <see cref="IClaudeExecutableLocator" />, never a <c>claude</c> in the
/// current directory, and runs it without a shell; the output is read concurrently and decoded as UTF-8. The process
/// inherits the environment of the current process plus <see cref="EnvironmentVariables" />, so <c>claude</c> writes
/// no color codes.
/// </remarks>
/// <param name="claudeExecutableLocator">
/// The locator that resolves the absolute path of the <c>claude</c> executable.
/// </param>
public sealed class ClaudeCommandRunner(IClaudeExecutableLocator claudeExecutableLocator) : IClaudeCommandRunner
{
    private readonly IClaudeExecutableLocator _claudeExecutableLocator = Ensure.NotNull(claudeExecutableLocator);

    /// <summary>
    /// Gets the environment variables that are set for every <c>claude</c> process in addition to the inherited ones.
    /// </summary>
    /// <value>
    /// <c>NO_COLOR=1</c>, which Claude Code evaluates to turn its colors off unless <c>FORCE_COLOR</c> is set.
    /// </value>
    internal static IReadOnlyDictionary<string, string> EnvironmentVariables { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NO_COLOR"] = "1"
        };

    /// <summary>
    /// Gets the description of the <c>claude</c> tool that the <see cref="ExternalToolRunner" /> starts.
    /// </summary>
    /// <value>
    /// The tool with the name <c>claude</c>, the <see cref="EnvironmentVariables" />, and the claude exceptions.
    /// </value>
    /// <remarks>
    /// Declared after <see cref="EnvironmentVariables" />, because static initializers run in the order of
    /// declaration.
    /// </remarks>
    internal static ExternalTool Tool { get; } = new("claude", "The Claude Code CLI claude", EnvironmentVariables,
        static (arguments, reason) => new ClaudeNotFoundException(arguments, reason),
        static (arguments, reason, innerException) =>
            new ClaudeUnavailableException(arguments, reason, innerException));

    /// <inheritdoc />
    public Task<ExternalProcessResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return ExternalToolRunner.RunAsync(Tool, _claudeExecutableLocator.FindClaudeExecutable, workingDirectory,
            arguments, cancellationToken);
    }
}
