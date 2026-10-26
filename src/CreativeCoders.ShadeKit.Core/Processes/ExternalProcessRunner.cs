using System.ComponentModel;
using System.Diagnostics;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Runs an external executable as a child process and captures its output.
/// </summary>
/// <remarks>
/// Starts only an absolute executable path and runs it without a shell; the output is read concurrently and decoded
/// as UTF-8. On cancellation the process and all of its descendants are killed.
/// </remarks>
internal static class ExternalProcessRunner
{
    /// <summary>
    /// Runs the specified executable with the specified arguments in the specified directory and waits until it exits.
    /// </summary>
    /// <remarks>
    /// The arguments are passed to the process one by one, without a shell, so they are neither split nor expanded.
    /// The process inherits the environment of the current process; <paramref name="environmentVariables" /> adds
    /// variables to it or overrides them. A non-zero exit code is not an error of this method; it is returned in the
    /// result for the caller to interpret.
    /// </remarks>
    /// <param name="executablePath">The absolute path of the executable to start.</param>
    /// <param name="workingDirectory">The directory in which the process runs.</param>
    /// <param name="arguments">The arguments that are passed to the process.</param>
    /// <param name="environmentVariables">
    /// The environment variables that are set for the process in addition to the inherited ones, or
    /// <see langword="null" /> to pass the inherited environment unchanged.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous run. The task result contains the exit code and the captured standard
    /// output and standard error of the process.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="executablePath" /> or <paramref name="workingDirectory" /> is <see langword="null" />, empty,
    /// or whitespace, or <paramref name="executablePath" /> is not fully qualified.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="arguments" /> is <see langword="null" />.</exception>
    /// <exception cref="Win32Exception">The process cannot be started.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed together with its descendants.
    /// </exception>
    public static async Task<ExternalProcessResult> RunAsync(string executablePath, string workingDirectory,
        IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        Ensure.IsNotNullOrWhitespace(executablePath);
        Ensure.IsNotNullOrWhitespace(workingDirectory);
        Ensure.NotNull(arguments);

        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException($"The executable path '{executablePath}' is not fully qualified.",
                nameof(executablePath));
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process();
        process.StartInfo = CreateStartInfo(executablePath, workingDirectory, arguments, environmentVariables);

        process.Start();

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

        return new ExternalProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static ProcessStartInfo CreateStartInfo(string executablePath, string workingDirectory,
        IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environmentVariables)
    {
        var startInfo = new ProcessStartInfo(executablePath)
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

        if (environmentVariables is not null)
        {
            foreach (var (name, value) in environmentVariables)
            {
                startInfo.Environment[name] = value;
            }
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
