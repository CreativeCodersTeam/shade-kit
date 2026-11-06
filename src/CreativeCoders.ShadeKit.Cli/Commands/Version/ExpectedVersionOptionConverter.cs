using CreativeCoders.SysConsole.Cli.Parsing;
using CreativeCoders.SysConsole.Cli.Parsing.OptionProperties;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Converts the value of <c>--expect</c> and rejects the option when it is given without a value.
/// </summary>
/// <remarks>
/// <para>
/// The option parser of CreativeCoders.SysConsole.Cli.Parsing calls the converter only for an option that is on the
/// command line (or that has a default value, which <c>--expect</c> has not). It passes <see langword="null" /> as the
/// value when the option is the last argument or the next argument starts with <c>-</c>; without this converter the
/// property would stay <see langword="null" />, exactly as if the option were missing.
/// </para>
/// <para>
/// A value, the empty string included, is converted by <see cref="CliValueConverters.Default" /> and checked by the
/// command later.
/// </para>
/// </remarks>
public sealed class ExpectedVersionOptionConverter : ICliValueConverter
{
    /// <summary>
    /// Converts the specified value of <c>--expect</c> to the type of the option property.
    /// </summary>
    /// <param name="value">The value that follows <c>--expect</c>, or <see langword="null" /> if there is none.</param>
    /// <param name="targetType">The type of the option property.</param>
    /// <param name="optionAttribute">The attribute that declares the option.</param>
    /// <returns>The value converted by <see cref="CliValueConverters.Default" />.</returns>
    /// <exception cref="ExpectedVersionMissingException"><paramref name="value" /> is <see langword="null" />.</exception>
    public object? Convert(object? value, Type targetType, OptionBaseAttribute optionAttribute)
    {
        return value is null
            ? throw new ExpectedVersionMissingException()
            : CliValueConverters.Default.Convert(value, targetType, optionAttribute);
    }
}
