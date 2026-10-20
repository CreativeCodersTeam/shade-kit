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
    /// Runs an operation of a command and aborts the command if the operation fails with an expected failure.
    /// </summary>
    /// <typeparam name="T">The type of the result of the operation.</typeparam>
    /// <param name="operation">The operation to run.</param>
    /// <param name="errorMapper">The mapper that decides which exceptions are expected failures.</param>
    /// <param name="json">
    /// <see langword="true" /> if the command runs in JSON mode; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="jsonWriter">The writer that receives the failure envelope in JSON mode.</param>
    /// <returns>The result of <paramref name="operation" />.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation" />, <paramref name="errorMapper" />, or <paramref name="jsonWriter" /> is
    /// <see langword="null" />.
    /// </exception>
    /// <exception cref="CliCommandAbortException">
    /// <paramref name="operation" /> threw an exception that <paramref name="errorMapper" /> maps; it is reported as
    /// <see cref="Create" /> describes. Any other exception escapes unchanged.
    /// </exception>
    public static T Run<T>(Func<T> operation, CliErrorMapper errorMapper, bool json, ICliJsonWriter jsonWriter)
    {
        Ensure.NotNull(operation);
        Ensure.NotNull(errorMapper);
        Ensure.NotNull(jsonWriter);

        try
        {
            return operation();
        }
        catch (Exception e) when (errorMapper.Map(e) is { } error)
        {
            throw Create(error, e, json, jsonWriter);
        }
    }

    /// <summary>
    /// Runs an asynchronous operation of a command and aborts the command if the operation fails with an expected
    /// failure.
    /// </summary>
    /// <typeparam name="T">The type of the result of the operation.</typeparam>
    /// <param name="operation">The operation to run.</param>
    /// <param name="errorMapper">The mapper that decides which exceptions are expected failures.</param>
    /// <param name="json">
    /// <see langword="true" /> if the command runs in JSON mode; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="jsonWriter">The writer that receives the failure envelope in JSON mode.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the result of
    /// <paramref name="operation" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation" />, <paramref name="errorMapper" />, or <paramref name="jsonWriter" /> is
    /// <see langword="null" />.
    /// </exception>
    /// <exception cref="CliCommandAbortException">
    /// <paramref name="operation" /> threw an exception that <paramref name="errorMapper" /> maps; it is reported as
    /// <see cref="Create" /> describes. Any other exception escapes unchanged.
    /// </exception>
    public static async Task<T> RunAsync<T>(Func<Task<T>> operation, CliErrorMapper errorMapper, bool json,
        ICliJsonWriter jsonWriter)
    {
        Ensure.NotNull(operation);
        Ensure.NotNull(errorMapper);
        Ensure.NotNull(jsonWriter);

        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception e) when (errorMapper.Map(e) is { } error)
        {
            throw Create(error, e, json, jsonWriter);
        }
    }

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
