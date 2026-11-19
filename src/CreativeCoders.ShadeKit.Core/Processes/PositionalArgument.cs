using System.Diagnostics.CodeAnalysis;

namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Checks values that are passed to an external tool as positional arguments.
/// </summary>
/// <remarks>
/// An external tool reads an argument that starts with <c>-</c> as an option instead of as the positional argument,
/// so such a value is refused before the tool is started.
/// </remarks>
internal static class PositionalArgument
{
    /// <summary>
    /// Ensures that a value can be passed to an external tool as a positional argument.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="toolName">The name of the tool that receives the value, for example <c>claude</c>, as used in the
    /// exception message.</param>
    /// <param name="paramName">The name of the caller's parameter that holds <paramref name="value" />, as reported by
    /// the exception.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value" /> is <see langword="null" />, empty, or consists only of white-space characters, or it
    /// starts with <c>-</c>.
    /// </exception>
    [SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
        Justification = "The caller's parameter name is forwarded on purpose, so an invalid value names the " +
                        "caller's parameter instead of this helper's.")]
    public static void Ensure(string value, string toolName, string paramName)
    {
        global::CreativeCoders.Core.Ensure.IsNotNullOrWhitespace(value, paramName);

        if (value.StartsWith('-'))
        {
            throw new ArgumentException(
                $"The value '{value}' starts with '-', so {toolName} would read it as an option.", paramName);
        }
    }
}
