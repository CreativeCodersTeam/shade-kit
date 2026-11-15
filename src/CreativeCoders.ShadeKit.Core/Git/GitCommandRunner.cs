using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Runs the <c>git</c> executable as a child process and captures its output.
/// </summary>
/// <remarks>
/// Starts only the absolute path from the <see cref="IGitExecutableLocator" />, never a <c>git</c> in the current
/// directory, and runs it without a shell; the output is read concurrently and decoded as UTF-8. The process inherits
/// the environment of the current process plus <see cref="EnvironmentVariables" />, so <c>git</c> never asks for
/// credentials on the terminal or through an askpass program.
/// </remarks>
/// <param name="gitExecutableLocator">The locator that resolves the absolute path of the <c>git</c> executable.</param>
public sealed class GitCommandRunner(IGitExecutableLocator gitExecutableLocator) : IGitCommandRunner
{
    private readonly IGitExecutableLocator _gitExecutableLocator = Ensure.NotNull(gitExecutableLocator);

    /// <summary>
    /// Gets the environment variables that are set for every <c>git</c> process in addition to the inherited ones.
    /// </summary>
    /// <value>
    /// <c>GIT_TERMINAL_PROMPT=0</c>, which makes <c>git</c> fail instead of prompting on the terminal, and an empty
    /// <c>GIT_ASKPASS</c>, which makes <c>git</c> ignore <c>core.askPass</c> and <c>SSH_ASKPASS</c>, so no askpass
    /// program is started.
    /// </value>
    internal static IReadOnlyDictionary<string, string> EnvironmentVariables { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GIT_ASKPASS"] = string.Empty
        };

    /// <summary>
    /// Gets the description of the <c>git</c> tool that the <see cref="ExternalToolRunner" /> starts.
    /// </summary>
    /// <value>The tool with the name <c>git</c>, the <see cref="EnvironmentVariables" />, and the git exceptions.</value>
    /// <remarks>
    /// Declared after <see cref="EnvironmentVariables" />, because static initializers run in the order of
    /// declaration.
    /// </remarks>
    internal static ExternalTool Tool { get; } = new("git", "git", EnvironmentVariables,
        static (arguments, reason) => new GitNotFoundException(arguments, reason),
        static (arguments, reason, innerException) => new GitUnavailableException(arguments, reason, innerException));

    /// <inheritdoc />
    public Task<ExternalProcessResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return ExternalToolRunner.RunAsync(Tool, _gitExecutableLocator.FindGitExecutable, workingDirectory, arguments,
            cancellationToken);
    }
}
