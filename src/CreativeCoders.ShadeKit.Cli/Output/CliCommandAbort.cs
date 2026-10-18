using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Reports an expected failure of a command in plain or JSON mode and creates the exception that aborts the command.
/// </summary>
internal static class CliCommandAbort
{
    /// <summary>
    /// Reports the specified error and creates the exception that aborts the command with its exit code.
    /// </summary>
    /// <remarks>
    /// In plain mode, nothing is written; the returned exception carries the markup-escaped message, which the CLI
    /// host prints. In JSON mode, a failure envelope with the error is written to <paramref name="jsonWriter" />, and
    /// the returned exception carries the raw message and does not print it, so that standard output contains only the
    /// envelope.
    /// </remarks>
    /// <param name="error">The mapped error that stops the command.</param>
    /// <param name="exception">The exception that caused the error.</param>
    /// <param name="json">
    /// <see langword="true" /> if the command runs in JSON mode; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="jsonWriter">The writer that receives the failure envelope in JSON mode.</param>
    /// <returns>
    /// The exception to throw; its exit code is the <see cref="CliError.ExitCode" /> of <paramref name="error" /> and
    /// its inner exception is <paramref name="exception" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="error" />, <paramref name="exception" />, or <paramref name="jsonWriter" /> is
    /// <see langword="null" />.
    /// </exception>
    public static CliCommandAbortException Create(CliError error, Exception exception, bool json,
        ICliJsonWriter jsonWriter)
    {
        Ensure.NotNull(error);
        Ensure.NotNull(exception);
        Ensure.NotNull(jsonWriter);

        if (!json)
        {
            return new CliCommandAbortException(Markup.Escape(error.Message), error.ExitCode, exception);
        }

        jsonWriter.Write(CliJsonEnvelope.Failure(error.ToJsonMessage()));

        return new CliCommandAbortException(error.Message, error.ExitCode, exception) { PrintMessage = false };
    }
}
