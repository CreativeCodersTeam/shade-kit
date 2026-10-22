using System.ComponentModel;
using System.Diagnostics;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Runs the <c>git</c> executable as a child process and captures its output.
/// </summary>
/// <remarks>
/// Before each run, the absolute path of the executable is resolved through the <see cref="IGitExecutableLocator" />
/// and that path is started, so the operating system never searches the current directory for <c>git</c>. A path
/// that is not fully qualified is rejected with a <see cref="GitUnavailableException" /> instead of being started.
/// The process runs without a shell and without a window; standard output and standard error are redirected, read
/// concurrently, and decoded as UTF-8.
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
            throw new GitUnavailableException(arguments,
                "git was not found in any absolute directory of the PATH environment variable.");
        }

        if (!Path.IsPathFullyQualified(gitExecutablePath))
        {
            throw new GitUnavailableException(arguments,
                $"the git executable path '{gitExecutablePath}' is not absolute, so it is not started.");
        }

        using var process = new Process();
        process.StartInfo = CreateStartInfo(gitExecutablePath, workingDirectory, arguments);

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new GitUnavailableException(arguments, $"git could not be started: {exception.Message}", exception);
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);

            throw;
        }

        var standardOutput = await standardOutputTask.ConfigureAwait(false);
        var standardError = await standardErrorTask.ConfigureAwait(false);

        return new GitCommandResult(process.ExitCode, standardOutput, standardError);
    }

    private static ProcessStartInfo CreateStartInfo(string gitExecutablePath, string workingDirectory,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(gitExecutablePath)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = TextFileEncoding.Utf8WithoutBom,
            StandardErrorEncoding = TextFileEncoding.Utf8WithoutBom
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process has already exited; there is nothing left to kill.
        }
        catch (Win32Exception)
        {
            // The process or one of its children could not be killed; cancellation is reported regardless.
        }
    }
}
