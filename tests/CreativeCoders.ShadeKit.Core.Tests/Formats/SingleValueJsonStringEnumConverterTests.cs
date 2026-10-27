using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Tests.Formats;

public class SingleValueJsonStringEnumConverterTests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        Converters = { new SingleValueJsonStringEnumConverter(JsonEnumNames.NamingPolicy) }
    };

    [Fact]
    public void Ctor_NullNamingPolicy_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SingleValueJsonStringEnumConverter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("namingPolicy");
    }

    [Theory]
    [InlineData(typeof(SampleMode), true)]
    [InlineData(typeof(DayOfWeek), true)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(SampleMode?), false)]
    public void CanConvert_Type_ReturnsTrueOnlyForEnumerations(Type type, bool expected)
    {
        // Arrange
        var converter = new SingleValueJsonStringEnumConverter(JsonEnumNames.NamingPolicy);

        // Act
        var canConvert = converter.CanConvert(type);

        // Assert
        canConvert
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("\"longer-value\"", SampleMode.LongerValue)]
    [InlineData("\"LongerValue\"", SampleMode.LongerValue)]
    [InlineData("\"longervalue\"", SampleMode.LongerValue)]
    [InlineData("\"LONGERVALUE\"", SampleMode.LongerValue)]
    [InlineData("\"default\"", SampleMode.Default)]
    [InlineData("\"DEFAULT\"", SampleMode.Default)]
    public void Deserialize_SingleName_ReadsValue(string json, SampleMode expected)
    {
        // Act
        var value = JsonSerializer.Deserialize<SampleMode>(json, Options);

        // Assert
        value
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("\"default, longer-value\"")]
    [InlineData("\"default,longer-value\"")]
    [InlineData("\"longer-value, longer-value\"")]
    [InlineData("\"Default, LongerValue\"")]
    [InlineData("\"default,\"")]
    [InlineData("\",default\"")]
    [InlineData("\",\"")]
    [InlineData("\"default\\u002C longer-value\"")]
    public void Deserialize_CommaSeparatedNames_ThrowsJsonException(string json)
    {
        // Act
        var act = () => JsonSerializer.Deserialize<SampleMode>(json, Options);

        // Assert
        act.Should().Throw<JsonException>()
            .WithMessage("*separated by commas*");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("\"0\"")]
    [InlineData("\"1\"")]
    [InlineData("\"Longer-Value\"")]
    [InlineData("\"unknown\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("true")]
    public void Deserialize_IntegerOrUnknownValue_ThrowsJsonException(string json)
    {
        // Act
        var act = () => JsonSerializer.Deserialize<SampleMode>(json, Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_NullableEnumWithNull_ReadsNull()
    {
        // Act
        var value = JsonSerializer.Deserialize<SampleMode?>("null", Options);

        // Assert
        value
            .Should().BeNull();
    }

    [Fact]
    public void Deserialize_NullableEnumWithSingleName_ReadsValue()
    {
        // Act
        var value = JsonSerializer.Deserialize<SampleMode?>("\"longer-value\"", Options);

        // Assert
        value
            .Should().Be(SampleMode.LongerValue);
    }

    [Fact]
    public void Deserialize_NullableEnumWithCommaSeparatedNames_ThrowsJsonException()
    {
        // Act
        var act = () => JsonSerializer.Deserialize<SampleMode?>("\"default, longer-value\"", Options);

        // Assert
        act.Should().Throw<JsonException>()
            .WithMessage("*separated by commas*");
    }

    [Fact]
    public void Deserialize_DictionaryWithSingleNameKey_ReadsKey()
    {
        // Act
        var value = JsonSerializer.Deserialize<Dictionary<SampleMode, int>>("""{ "longer-value": 1 }""", Options);

        // Assert
        value
            .Should().Equal(new Dictionary<SampleMode, int> { [SampleMode.LongerValue] = 1 });
    }

    [Theory]
    [InlineData("longer-value")]
    [InlineData("LongerValue")]
    [InlineData("LONGERVALUE")]
    public void Deserialize_DictionaryWithSingleNameOrMemberNameKey_ReadsKey(string key)
    {
        // Act
        var value = JsonSerializer.Deserialize<Dictionary<SampleMode, int>>($$"""{ "{{key}}": 1 }""", Options);

        // Assert
        value
            .Should().Equal(new Dictionary<SampleMode, int> { [SampleMode.LongerValue] = 1 });
    }

    [Theory]
    [InlineData("default, longer-value")]
    [InlineData("default,longer-value")]
    [InlineData("default\\u002C longer-value")]
    public void Deserialize_DictionaryWithCommaSeparatedKey_ThrowsJsonException(string key)
    {
        // Act
        var act = () => JsonSerializer.Deserialize<Dictionary<SampleMode, int>>($$"""{ "{{key}}": 1 }""", Options);

        // Assert
        act.Should().Throw<JsonException>()
            .WithMessage("*separated by commas*");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("unknown")]
    [InlineData("Longer-Value")]
    public void Deserialize_DictionaryWithIntegerOrUnknownKey_ThrowsJsonException(string key)
    {
        // Act
        var act = () => JsonSerializer.Deserialize<Dictionary<SampleMode, int>>($$"""{ "{{key}}": 1 }""", Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("\" default\"")]
    [InlineData("\"default \"")]
    [InlineData("\" longer-value \"")]
    [InlineData("\"\\tLongerValue\"")]
    public void Deserialize_NameWithSurroundingWhitespace_BehavesLikeJsonStringEnumConverter(string json)
    {
        // Arrange
        var plainOptions = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter(JsonEnumNames.NamingPolicy, allowIntegerValues: false) }
        };
        var expected = ReadOrNull(json, plainOptions);

        // Act
        var value = ReadOrNull(json, Options);

        // Assert
        value
            .Should().Be(expected);
    }

    [Fact]
    public void SerializeAndDeserialize_TwoEnumTypesWithOneOptionsInstance_ConvertsBoth()
    {
        // Arrange
        var original = (Mode: SampleMode.LongerValue, Day: DayOfWeek.Wednesday);

        // Act
        var modeJson = JsonSerializer.Serialize(original.Mode, Options);
        var dayJson = JsonSerializer.Serialize(original.Day, Options);
        var mode = JsonSerializer.Deserialize<SampleMode>(modeJson, Options);
        var day = JsonSerializer.Deserialize<DayOfWeek>(dayJson, Options);

        // Assert
        modeJson
            .Should().Be("\"longer-value\"");

        dayJson
            .Should().Be("\"wednesday\"");

        (mode, day)
            .Should().Be(original);
    }

    [Theory]
    [InlineData(SampleMode.Default, "\"default\"")]
    [InlineData(SampleMode.LongerValue, "\"longer-value\"")]
    public void Serialize_Value_WritesKebabCaseName(SampleMode value, string expected)
    {
        // Act
        var json = JsonSerializer.Serialize(value, Options);

        // Assert
        json
            .Should().Be(expected);
    }

    [Fact]
    public void Serialize_UndefinedValue_ThrowsJsonException()
    {
        // Act
        var act = () => JsonSerializer.Serialize((SampleMode)99, Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Serialize_DictionaryWithEnumKey_WritesKebabCaseKey()
    {
        // Arrange
        var dictionary = new Dictionary<SampleMode, int> { [SampleMode.LongerValue] = 1 };

        // Act
        var json = JsonSerializer.Serialize(dictionary, Options);

        // Assert
        json
            .Should().Be("""{"longer-value":1}""");
    }

    private static SampleMode? ReadOrNull(string json, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<SampleMode>(json, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
