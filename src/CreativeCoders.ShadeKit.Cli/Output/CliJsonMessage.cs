namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Represents an error or a warning in the JSON output of a command.
/// </summary>
/// <param name="Code">The stable kebab-case code of the error or warning, for example <c>upgrade-needed</c>.</param>
/// <param name="Message">The human-readable description of the error or warning.</param>
public sealed record CliJsonMessage(string Code, string Message);
