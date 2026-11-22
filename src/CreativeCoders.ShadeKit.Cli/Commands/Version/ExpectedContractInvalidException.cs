namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Represents the exception that is thrown when <c>--expect-contract</c> is given without a value or with a value that
/// is not a CLI contract number.
/// </summary>
/// <remarks>
/// The option parser throws it while it reads the arguments of <c>shade-kit version</c>, before the command runs, so
/// it reaches the CLI host as an argument error. A CLI contract number is a non-negative <see cref="int" /> written
/// in ASCII digits, without a sign, leading zeros, or whitespace (see <see cref="ExpectedContractOptionConverter" />).
/// </remarks>
/// <param name="value">
/// The value that follows <c>--expect-contract</c>, or <see langword="null" /> if there is none.
/// </param>
public sealed class ExpectedContractInvalidException(string? value)
    : Exception(value is null
        ? "The option '--expect-contract' was given without a CLI contract number."
        : $"The value '{value}' of the option '--expect-contract' is not a CLI contract number.")
{
    /// <summary>
    /// Gets the value that follows <c>--expect-contract</c>.
    /// </summary>
    /// <value>
    /// The value as passed, or <see langword="null" /> if <c>--expect-contract</c> was given without a value.
    /// </value>
    public string? Value { get; } = value;
}
