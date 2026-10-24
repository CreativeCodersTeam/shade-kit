using CreativeCoders.Cli.Hosting;
using CreativeCoders.Cli.Hosting.Help;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// JSON mode is on when the option parser sets <see cref="JsonCommandOptions.Json" />. <c>--json=&lt;value&gt;</c>
    /// counts only without a bare <c>--json</c> and turns JSON mode on unless the value is <c>false</c>.
    /// </para>
    /// <para>
    /// In JSON mode, <paramref name="output" /> receives exactly one envelope and all other output goes to
    /// <paramref name="error" />. Host failures, unexpected exceptions, and runs without an envelope (such as help)
    /// get a <c>host-error</c> envelope and <see cref="ShadeKitCliExitCodes.HostError" />. A failure after a success
    /// envelope is only reported on <paramref name="error" /> and returns <c>0</c>.
    /// </para>
    /// <para>
    /// Rejected arguments return <see cref="ShadeKitCliExitCodes.InvalidArguments" /> and a canceled run (first
    /// Ctrl+C) <see cref="ShadeKitCliExitCodes.Canceled" />, both without exception details. Failures to write while a
    /// failure is reported do not escape.
    /// </para>
    /// </remarks>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">The writer for standard output.</param>
    /// <param name="error">The writer for standard error.</param>
    /// <returns>A task that represents the run. The task result contains the process exit code.</returns>
    internal static Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        return RunAsync(args, output, error, null);
    }

    /// <summary>
    /// Runs the SHADE kit CLI with the specified arguments and output writers, and lets the caller register services
    /// that replace the default ones.
    /// </summary>
    /// <remarks>
    /// Behaves like <see cref="RunAsync(string[], TextWriter, TextWriter)" />. <paramref name="configureServices" /> is
    /// the seam for end-to-end tests: it runs before the defaults are added, so its registrations win. Registrations of
    /// <see cref="IAnsiConsole" /> and <see cref="ICliJsonWriter" /> are removed, because both belong to the run.
    /// </remarks>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="output">The writer for standard output.</param>
    /// <param name="error">The writer for standard error.</param>
    /// <param name="configureServices">
    /// An action that registers services before the defaults, or <see langword="null" /> to use only the defaults.
    /// </param>
    /// <returns>A task that represents the run. The task result contains the process exit code.</returns>
    internal static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
        Action<IServiceCollection>? configureServices)
    {
        var jsonMode = IsJsonMode(args);
        var jsonWriter = new CliJsonWriter(output);
        using var cancellation = new ConsoleCancellation();
        int exitCode;

        cancellation.ListenToCancelKeyPress();

        try
        {
            exitCode = await CreateHost(jsonWriter, jsonMode ? error : output, cancellation, configureServices)
                .RunMainAsync(args)
                .ConfigureAwait(false);
        }
        catch (Exception e) when ((CliErrorMapper.MapArgumentError(e, args) ??
                                   CliErrorMapper.MapCancellation(e, cancellation.Token)) is { } expectedError)
        {
            if (jsonMode)
            {
                return await CompleteFailedJsonRunAsync(jsonWriter, error, expectedError).ConfigureAwait(false);
            }

            await TryWriteErrorAsync(error, expectedError.Message).ConfigureAwait(false);

            return expectedError.ExitCode;
        }
        catch (Exception e)
        {
            var cause = CliErrorMapper.Unwrap(e);

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

    private static ICliHost CreateHost(ICliJsonWriter jsonWriter, TextWriter consoleOutput,
        ICliCancellation cancellation, Action<IServiceCollection>? configureServices)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(consoleOutput) });

        // The host runs the ConfigureServices actions in the order they were added, so the overrides come first and
        // the TryAdd registrations of AddShadeKitCore and AddShadeKitCli keep them. The console and the JSON writer
        // belong to the run: Program reads the writer to keep the one-envelope contract, so overrides of them are
        // removed.
        return CliHostBuilder.Create()
            .ConfigureServices(x => configureServices?.Invoke(x))
            .ConfigureServices(x => x
                .RemoveAll<IAnsiConsole>()
                .RemoveAll<ICliJsonWriter>()
                .AddSingleton(console)
                .AddShadeKitCore()
                .AddShadeKitCli(jsonWriter, cancellation))
            .ScanAssemblies(typeof(Program).Assembly)
            .SkipScanEntryAssembly()
            .UseValidation()
            .PrintFooterText([""])
            .EnableHelp(HelpCommandKind.CommandOrArgument, HelpCommandKind.EmptyArgs)
            .Build();
    }

    private static Task<int> CompleteFailedJsonRunAsync(CliJsonWriter jsonWriter, TextWriter error,
        string message)
    {
        return CompleteFailedJsonRunAsync(jsonWriter, error,
            CliError.Create(ShadeKitCliExitCodes.HostError, message));
    }

    private static async Task<int> CompleteFailedJsonRunAsync(CliJsonWriter jsonWriter, TextWriter error,
        CliError failure)
    {
        switch (jsonWriter.WrittenEnvelope)
        {
            case { Ok: true }:
                return CliExitCodes.Success;
            case not null:
                // A failure envelope was already written; it stays the result, so the failure is not reported again.
                return ShadeKitCliExitCodes.HostError;
        }

        try
        {
            jsonWriter.WriteIfNothingWritten(CliJsonEnvelope.Failure(failure.ToJsonMessage()));
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            await TryWriteErrorAsync(error, $"The JSON result could not be written: {e.Message}")
                .ConfigureAwait(false);

            return ShadeKitCliExitCodes.HostError;
        }

        return failure.ExitCode;
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
