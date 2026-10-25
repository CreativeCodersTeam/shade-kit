namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Represents the outcome of a finished external process.
/// </summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="StandardOutput">
/// The complete text that the process wrote to its standard output, decoded as UTF-8.
/// </param>
/// <param name="StandardError">
/// The complete text that the process wrote to its standard error, decoded as UTF-8.
/// </param>
internal sealed record ExternalProcessResult(int ExitCode, string StandardOutput, string StandardError);
