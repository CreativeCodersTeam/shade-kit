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
    /// Converts the error into the error object of a JSON envelope.
    /// </summary>
    /// <returns>A message with the <see cref="Code" /> and <see cref="Message" /> of the error.</returns>
    public CliJsonMessage ToJsonMessage()
    {
        return new CliJsonMessage(Code, Message);
    }
}
