using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Runs the <c>git</c> executable as a child process and captures its output.
/// </summary>
/// <remarks>
/// Starts only the absolute path from the <see cref="IGitExecutableLocator" />, never a <c>git</c> in the current
/// directory, and runs it without a shell; the output is read concurrently and decoded as UTF-8.
/// </remarks>
/// <param name="gitExecutableLocator">The locator that resolves the absolute path of the <c>git</c> executable.</param>
public sealed class GitCommandRunner(IGitExecutableLocator gitExecutableLocator) : IGitCommandRunner
{
    private static readonly ExternalTool Git = new("git", "git", null,
        static (arguments, reason) => new GitNotFoundException(arguments, reason),
        static (arguments, reason, innerException) => new GitUnavailableException(arguments, reason, innerException));

    private readonly IGitExecutableLocator _gitExecutableLocator = Ensure.NotNull(gitExecutableLocator);

    /// <inheritdoc />
    public Task<ExternalProcessResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return ExternalToolRunner.RunAsync(Git, _gitExecutableLocator.FindGitExecutable, workingDirectory, arguments,
            cancellationToken);
    }
}
