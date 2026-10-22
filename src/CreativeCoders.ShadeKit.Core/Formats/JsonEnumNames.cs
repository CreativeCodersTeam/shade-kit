using System.Text.Json;

namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Provides the names with which the JSON files in <c>.shadekit/</c> and the JSON output of the CLI write enumeration
/// values, so that text output can use the same names.
/// </summary>
public static class JsonEnumNames
{
    /// <summary>
    /// Gets the naming policy that converts an enumeration member name to its JSON name, lower-case kebab case.
    /// </summary>
    public static JsonNamingPolicy NamingPolicy { get; } = JsonNamingPolicy.KebabCaseLower;

    /// <summary>
    /// Gets the name with which JSON writes the specified enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The type of the enumeration.</typeparam>
    /// <param name="value">The enumeration value.</param>
    /// <returns>The name of <paramref name="value" /> in lower-case kebab case, for example <c>upgrade-needed</c>.</returns>
    public static string ToJsonName<TEnum>(this TEnum value)
        where TEnum : struct, Enum
    {
        return NamingPolicy.ConvertName(value.ToString());
    }
}
