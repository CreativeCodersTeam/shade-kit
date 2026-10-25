using System.ComponentModel;
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
    private readonly IGitExecutableLocator _gitExecutableLocator = Ensure.NotNull(gitExecutableLocator);

    /// <inheritdoc />
    public async Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        Ensure.IsNotNullOrWhitespace(workingDirectory);
        Ensure.NotNull(arguments);

        cancellationToken.ThrowIfCancellationRequested();

        var gitExecutablePath = _gitExecutableLocator.FindGitExecutable();

        if (string.IsNullOrEmpty(gitExecutablePath))
        {
            throw new GitNotFoundException(arguments,
                "git was not found in any absolute directory of the PATH environment variable.");
        }

        if (!Path.IsPathFullyQualified(gitExecutablePath))
        {
            throw new GitUnavailableException(arguments,
                $"the git executable path '{gitExecutablePath}' is not absolute, so it is not started.");
        }

        ExternalProcessResult result;

        try
        {
            result = await ExternalProcessRunner
                .RunAsync(gitExecutablePath, workingDirectory, arguments, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Win32Exception exception)
        {
            throw new GitUnavailableException(arguments, $"git could not be started: {exception.Message}", exception);
        }

        return new GitCommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }
}
