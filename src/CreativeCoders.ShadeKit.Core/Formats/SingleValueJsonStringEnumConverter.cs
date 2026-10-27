using System.Text.Json;
using System.Text.Json.Serialization;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Converts enumeration values to and from JSON strings like <see cref="JsonStringEnumConverter" />, but reads only a
/// single value: a string that lists several values separated by commas, such as <c>"active, done"</c>, is invalid.
/// </summary>
/// <remarks>
/// <see cref="JsonStringEnumConverter" /> reads a comma-separated list of names for every enumeration type and combines
/// the values, even for an enumeration without <see cref="FlagsAttribute" />. This converter rejects such a string
/// with a <see cref="JsonException" /> before it reaches the wrapped converter; every other string is handled by the
/// wrapped converter unchanged. Integer values are not allowed, neither as JSON numbers nor as strings.
/// </remarks>
internal sealed class SingleValueJsonStringEnumConverter : JsonConverterFactory
{
    private readonly JsonStringEnumConverter _enumConverter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleValueJsonStringEnumConverter" /> class.
    /// </summary>
    /// <param name="namingPolicy">The naming policy that converts an enumeration member name to its JSON name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="namingPolicy" /> is <see langword="null" />.</exception>
    public SingleValueJsonStringEnumConverter(JsonNamingPolicy namingPolicy)
    {
        _enumConverter = new JsonStringEnumConverter(Ensure.NotNull(namingPolicy), allowIntegerValues: false);
    }

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        return _enumConverter.CanConvert(typeToConvert);
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var enumConverter = _enumConverter.CreateConverter(typeToConvert, options);

        return (JsonConverter)Activator.CreateInstance(
            typeof(SingleValueConverter<>).MakeGenericType(typeToConvert), enumConverter)!;
    }

    /// <summary>
    /// Converts the values of one enumeration type, delegating to the converter of
    /// <see cref="JsonStringEnumConverter" /> after rejecting comma-separated strings.
    /// </summary>
    /// <typeparam name="TEnum">The type of the enumeration.</typeparam>
    /// <param name="enumConverter">The converter that <see cref="JsonStringEnumConverter" /> created for
    /// <typeparamref name="TEnum" />.</param>
    private sealed class SingleValueConverter<TEnum>(JsonConverter<TEnum> enumConverter) : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            EnsureSingleValue(ref reader);

            return enumConverter.Read(ref reader, typeToConvert, options);
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            enumConverter.Write(writer, value, options);
        }

        public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            EnsureSingleValue(ref reader);

            return enumConverter.ReadAsPropertyName(ref reader, typeToConvert, options);
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            enumConverter.WriteAsPropertyName(writer, value, options);
        }

        private static void EnsureSingleValue(ref Utf8JsonReader reader)
        {
            // GetString unescapes the value, so an escaped comma such as , is found as well.
            if (reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName
                && reader.GetString()!.Contains(','))
            {
                throw new JsonException(
                    $"The JSON value could not be converted to {typeof(TEnum)}: it lists several values separated by "
                    + "commas, but only a single value is allowed.");
            }
        }
    }
}
