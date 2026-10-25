namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Represents the outcome of a finished <c>gh</c> process.
/// </summary>
/// <param name="ExitCode">The exit code of the <c>gh</c> process.</param>
/// <param name="StandardOutput">
/// The complete text that the process wrote to its standard output, decoded as UTF-8.
/// </param>
/// <param name="StandardError">
/// The complete text that the process wrote to its standard error, decoded as UTF-8.
/// </param>
public sealed record GhCommandResult(int ExitCode, string StandardOutput, string StandardError);
