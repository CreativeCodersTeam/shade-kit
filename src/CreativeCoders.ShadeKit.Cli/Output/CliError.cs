namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Represents an expected failure of a command with its exit code, error code, and message.
/// </summary>
/// <param name="ExitCode">The process exit code from <see cref="ShadeKitCliExitCodes" />.</param>
/// <param name="Code">The error code from <see cref="ShadeKitCliErrorCodes" />.</param>
/// <param name="Message">The human-readable description of the failure.</param>
public sealed record CliError(int ExitCode, string Code, string Message)
{
    /// <summary>
    /// Creates an error with the specified exit code, the error code that belongs to it, and the specified message.
    /// </summary>
    /// <param name="exitCode">The process exit code from <see cref="ShadeKitCliExitCodes" />.</param>
    /// <param name="message">The human-readable description of the failure.</param>
    /// <returns>
    /// The error with <paramref name="exitCode" />, the error code that
    /// <see cref="ShadeKitCliErrorCodes.ForExitCode" /> returns for it, and <paramref name="message" />.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exitCode" /> is not defined in <see cref="ShadeKitCliExitCodes" />.
    /// </exception>
    public static CliError Create(int exitCode, string message)
    {
        return new CliError(exitCode, ShadeKitCliErrorCodes.ForExitCode(exitCode), message);
    }

    /// <summary>
    /// Converts the error into the error object of a JSON envelope.
    /// </summary>
    /// <returns>A message with the <see cref="Code" /> and <see cref="Message" /> of the error.</returns>
    public CliJsonMessage ToJsonMessage()
    {
        return new CliJsonMessage(Code, Message);
    }
}
