using System.Reflection;
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
    /// When the option parser rejects the arguments of a command, for example because a required argument is missing,
    /// the run returns <see cref="ShadeKitCliExitCodes.InvalidArguments" /> with the error that
    /// <see cref="CliErrorMapper.MapArgumentError" /> builds, also when the exception is wrapped in a
    /// <see cref="TargetInvocationException" />. In plain mode, its message, which names the argument and the
    /// <c>--help</c> of the command, is written to <paramref name="error" /> as one line without exception details.
    /// In JSON mode, an <c>invalid-arguments</c> envelope is written instead and nothing else.
    /// </para>
    /// <para>
    /// For the duration of the run, a <see cref="ConsoleCancellation" /> handles <see cref="Console.CancelKeyPress" />
    /// and is registered as the <see cref="ICliCancellation" /> whose token the commands pass to the core. The first
    /// Ctrl+C cancels the token and keeps the process alive; a second Ctrl+C ends the process with the default handling
    /// of the runtime. The handler is removed when the run ends. A run that stops because of the token returns
    /// <see cref="ShadeKitCliExitCodes.Canceled" /> with the error that <see cref="CliErrorMapper.MapCancellation" />
    /// builds: in plain mode, its message <c>The command was canceled.</c> is written to <paramref name="error" /> as
    /// one line without exception details; in JSON mode, a <c>canceled</c> envelope is written instead and nothing
    /// else.
    /// </para>
    /// <para>
    /// Failures to write to <paramref name="output" /> or <paramref name="error" /> while reporting a failure, for
    /// example because the reading end of a pipe was closed, do not escape. If the JSON envelope cannot be written,
    /// the run returns <see cref="ShadeKitCliExitCodes.HostError" />; a failure to write to <paramref name="error" />
    /// does not change the exit code.
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
    /// <para>
    /// The run behaves exactly like <see cref="RunAsync(string[], TextWriter, TextWriter)" />.
    /// </para>
    /// <para>
    /// <paramref name="configureServices" /> is the seam for end-to-end tests, for example to register a fake
    /// <see cref="CreativeCoders.ShadeKit.Core.Git.IGitCommandRunner" />, <see cref="IConsoleConfirmation" />, or an
    /// <see cref="ICliCancellation" /> whose token is canceled already.
    /// It is called before the default services are added. Because
    /// <see cref="ShadeKitCoreServiceCollectionExtensions.AddShadeKitCore" /> and
    /// <see cref="ShadeKitCliServiceCollectionExtensions.AddShadeKitCli" /> keep existing registrations, the services
    /// it registers take precedence over the defaults. The <see cref="IAnsiConsole" /> and the
    /// <see cref="ICliJsonWriter" /> belong to the run and cannot be replaced: registrations of them made by the
    /// action are removed, so that output still reaches <paramref name="output" /> and <paramref name="error" /> and
    /// JSON mode still writes exactly one envelope. An exception thrown by the action is handled like any other
    /// unexpected exception of the run.
    /// </para>
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
