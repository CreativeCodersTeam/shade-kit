namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Represents the exception that is thrown when <c>--expect</c> is given without a value.
/// </summary>
/// <remarks>
/// The option parser throws it while it reads the arguments of <c>shade-kit version</c>, before the command runs, so
/// it reaches the CLI host as an argument error.
/// </remarks>
public class ExpectedVersionMissingException()
    : Exception("The option '--expect' was given without a version.");
