using System.Globalization;
using System.Text.RegularExpressions;
using CreativeCoders.SysConsole.Cli.Parsing;
using CreativeCoders.SysConsole.Cli.Parsing.OptionProperties;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Converts the value of <c>--expect-contract</c> to a CLI contract number and rejects the option when it is given
/// without a value or with a value that is not a CLI contract number.
/// </summary>
/// <remarks>
/// <para>
/// The option parser of CreativeCoders.SysConsole.Cli.Parsing calls the converter only for an option that is on the
/// command line (or that has a default value, which <c>--expect-contract</c> has not). It passes
/// <see langword="null" /> as the value when the option is the last argument or the next argument starts with
/// <c>-</c>, so <c>--expect-contract -1</c> counts as an option without a value.
/// </para>
/// <para>
/// A CLI contract number is written in ASCII digits only, without a sign, leading zeros, or whitespace, and fits into
/// an <see cref="int" />: <c>0</c>, <c>1</c>, and <c>42</c> are accepted; <c>01</c>, <c>+1</c>, <c> 1</c>,
/// <c>1.0</c>, and <c>2147483648</c> are rejected. Whether the number lies within the contract window of the engine is
/// checked later by the command.
/// </para>
/// </remarks>
public sealed partial class ExpectedContractOptionConverter : ICliValueConverter
{
    /// <summary>
    /// Converts the specified value of <c>--expect-contract</c> to a CLI contract number.
    /// </summary>
    /// <param name="value">
    /// The value that follows <c>--expect-contract</c>, or <see langword="null" /> if there is none.
    /// </param>
    /// <param name="targetType">The type of the option property.</param>
    /// <param name="optionAttribute">The attribute that declares the option.</param>
    /// <returns>The CLI contract number as an <see cref="int" />.</returns>
    /// <exception cref="ExpectedContractInvalidException">
    /// <paramref name="value" /> is <see langword="null" />, or it is not a CLI contract number.
    /// </exception>
    public object? Convert(object? value, Type targetType, OptionBaseAttribute optionAttribute)
    {
        if (value is null)
        {
            throw new ExpectedContractInvalidException(null);
        }

        var text = value.ToString() ?? string.Empty;

        return ContractNumberRegex().IsMatch(text)
               && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var contract)
            ? contract
            : throw new ExpectedContractInvalidException(text);
    }

    // [0-9] matches only the ASCII digits, unlike \d; \z, unlike $, does not match before a final line break.
    [GeneratedRegex(@"^(0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex ContractNumberRegex();
}
