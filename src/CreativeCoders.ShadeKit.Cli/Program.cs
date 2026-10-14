using System.Reflection;
using CreativeCoders.Cli.Hosting;
using CreativeCoders.Cli.Hosting.Help;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        return RunAsync(args, Console.Out, Console.Error);
    }

    /// <summary>
    /// Runs the SHADE kit CLI with the specified arguments and output writers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// JSON mode is on exactly when the option parser sets <see cref="JsonCommandOptions.Json" />: the first argument
    /// that starts with <c>--</c> and names <c>json</c> decides. It turns JSON mode on when no argument follows it,
    /// when the next argument starts with <c>-</c>, or when the next argument parses as <see langword="true" />. Any
    /// other next argument, such as <c>false</c>, <c>yes</c>, or a positional value, is taken as the value of the
    /// switch and turns JSON mode off. <c>--json=&lt;value&gt;</c>, which the option parser does not read, counts
    /// only without a bare <c>--json</c>; it turns JSON mode on unless the value is <c>false</c>, so that the run
    /// reports a <c>host-error</c> envelope instead of plain text.
    /// </para>
    /// <para>
    /// In plain mode, all console output goes to <paramref name="output" />, and failures of the CLI host itself,
    /// such as an unknown command, are reported as text by the host. An unexpected exception is written to
    /// <paramref name="error" />, as a short message followed by its details, and ends the run with
    /// <see cref="ShadeKitCliExitCodes.HostError" />.
    /// </para>
    /// <para>
    /// In JSON mode, <paramref name="output" /> receives exactly one JSON envelope and nothing else. Console output
    /// of the host, such as its error text or help, goes to <paramref name="error" />. If the host fails, an
    /// unexpected exception escapes (its details go to <paramref name="error" />), or the run ends without writing
    /// an envelope, as help does, a <c>host-error</c> envelope is written and
    /// <see cref="ShadeKitCliExitCodes.HostError" /> is returned. Once an envelope is written, it is the result of
    /// the run and no second one is written. A failure after a success envelope, for example in a post-processor, is
    /// therefore only reported on <paramref name="error" />, and the run returns <c>0</c> so that the exit code agrees
    /// with the envelope.
    /// </para>
    /// <para>
    /// Failures to write to <paramref name="output" /> or <paramref name="error" /> while reporting a failure, for
    /// example because the reading end of a pipe was closed, do not escape; the run then returns
    /// <see cref="ShadeKitCliExitCodes.HostError" />.
    /// </para>
    /// </remarks>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">The writer for standard output.</param>
    /// <param name="error">The writer for standard error.</param>
    /// <returns>A task that represents the run. The task result contains the process exit code.</returns>
    internal static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var jsonMode = IsJsonMode(args);
        var jsonWriter = new CliJsonWriter(output);
        int exitCode;

        try
        {
            exitCode = await CreateHost(jsonWriter, jsonMode ? error : output).RunMainAsync(args)
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // The host calls commands with options through reflection, which wraps exceptions of synchronous commands.
            var cause = e is TargetInvocationException { InnerException: { } innerException } ? innerException : e;

            await TryWriteErrorAsync(error, $"The command failed unexpectedly: {cause.Message}", cause.ToString())
                .ConfigureAwait(false);

            return jsonMode
                ? await CompleteFailedJsonRunAsync(jsonWriter, error,
                        $"The command failed unexpectedly: {cause.Message} Details were written to standard error.")
                    .ConfigureAwait(false)
                : ShadeKitCliExitCodes.HostError;
        }

        if (!jsonMode)
        {
            return ShadeKitCliExitCodes.ToProcessExitCode(exitCode);
        }

        if (exitCode < 0)
        {
            return await CompleteFailedJsonRunAsync(jsonWriter, error,
                    $"The command could not be run (host exit code {exitCode}). Details were written to standard " +
                    "error.")
                .ConfigureAwait(false);
        }

        return jsonWriter.HasWritten
            ? exitCode
            : await CompleteFailedJsonRunAsync(jsonWriter, error,
                    "The command wrote no JSON result. Help and other plain-text output are not available in JSON " +
                    "mode; they were written to standard error.")
                .ConfigureAwait(false);
    }

    private static bool IsJsonMode(string[] args)
    {
        // Mirrors how the option parser of CreativeCoders.SysConsole.Cli.Parsing reads a bool switch: a long name is
        // every argument that starts with "--" with all leading dashes removed, and the next argument is its value
        // unless it starts with '-'. A missing value means true; any other value counts only if it parses as true.
        var index = Array.FindIndex(args, x => x.StartsWith("--", StringComparison.Ordinal) &&
                                               x.TrimStart('-') == JsonCommandOptions.JsonOptionName);

        if (index >= 0)
        {
            return index + 1 >= args.Length || args[index + 1].StartsWith('-') || IsTrue(args[index + 1]);
        }

        var jsonAssignment = "--" + JsonCommandOptions.JsonOptionName + "=";
        var assignment = args.FirstOrDefault(x => x.StartsWith(jsonAssignment, StringComparison.Ordinal));

        return assignment is not null && !IsFalse(assignment[jsonAssignment.Length..]);
    }

    private static bool IsTrue(string value)
    {
        return bool.TryParse(value, out var flag) && flag;
    }

    private static bool IsFalse(string value)
    {
        return bool.TryParse(value, out var flag) && !flag;
    }

    private static ICliHost CreateHost(ICliJsonWriter jsonWriter, TextWriter consoleOutput)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(consoleOutput) });

        return CliHostBuilder.Create()
            .ConfigureServices(x => x
                .AddSingleton(console)
                .AddShadeKitCore()
                .AddShadeKitCli(jsonWriter))
            .ScanAssemblies(typeof(Program).Assembly)
            .SkipScanEntryAssembly()
            .UseValidation()
            .PrintFooterText([""])
            .EnableHelp(HelpCommandKind.CommandOrArgument, HelpCommandKind.EmptyArgs)
            .Build();
    }

    private static async Task<int> CompleteFailedJsonRunAsync(CliJsonWriter jsonWriter, TextWriter error,
        string message)
    {
        if (jsonWriter.WrittenEnvelope is { Ok: true })
        {
            return CliExitCodes.Success;
        }

        try
        {
            jsonWriter.WriteIfNothingWritten(
                CliJsonEnvelope.Failure(new CliJsonMessage(ShadeKitCliErrorCodes.HostError, message)));
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            await TryWriteErrorAsync(error, $"The JSON result could not be written: {e.Message}")
                .ConfigureAwait(false);
        }

        return ShadeKitCliExitCodes.HostError;
    }

    private static async Task TryWriteErrorAsync(TextWriter error, params string[] lines)
    {
        try
        {
            foreach (var line in lines)
            {
                await error.WriteLineAsync(line).ConfigureAwait(false);
            }

            await error.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            // Standard error is the last place to report to; if it is gone, the exit code is all that is left.
        }
    }
}
