using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Init;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

public class CliJsonWriterTests
{
    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public void Write_SuccessEnvelope_WritesAllFourEnvelopeFieldsInOrder()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);

        // Act
        writer.Write(CliJsonEnvelope.Success(new { Value = 1 }));

        // Assert
        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;

        root.EnumerateObject().Select(x => x.Name)
            .Should().Equal("ok", "data", "warnings", "error");

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        root.GetProperty("data").GetProperty("value").GetInt32()
            .Should().Be(1);

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        root.GetProperty("error").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public void Write_FailureEnvelope_WritesNullDataAndErrorObject()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);

        // Act
        writer.Write(CliJsonEnvelope.Failure(new CliJsonMessage("kit-root-not-found", "No kit root.")));

        // Assert
        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;

        root.EnumerateObject().Select(x => x.Name)
            .Should().Equal("ok", "data", "warnings", "error");

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("data").ValueKind
            .Should().Be(JsonValueKind.Null);

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("kit-root-not-found");

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be("No kit root.");
    }

    [Fact]
    public void Write_EnvelopeWithWarnings_WritesCodeAndMessageOfEachWarning()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);

        // Act
        writer.Write(CliJsonEnvelope.Success(new object(), [new CliJsonMessage("upgrade-needed", "Run upgrade.")]));

        // Assert
        using var document = JsonDocument.Parse(output.ToString());
        var warning = document.RootElement.GetProperty("warnings")[0];

        warning.EnumerateObject().Select(x => (x.Name, x.Value.GetString()))
            .Should().Equal(("code", "upgrade-needed"), ("message", "Run upgrade."));
    }

    [Fact]
    public void Write_DataWithDictionary_KeepsDictionaryKeysUnchanged()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);
        var data = new { Formats = new Dictionary<string, int> { ["current-feature"] = 1, ["Config"] = 2 } };

        // Act
        writer.Write(CliJsonEnvelope.Success(data));

        // Assert
        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("data").GetProperty("formats").EnumerateObject().Select(x => x.Name)
            .Should().Equal("current-feature", "Config");
    }

    [Fact]
    public void Write_Envelope_WritesIndentedLfTextEndingWithLineBreakAndUnescapedText()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);

        // Act
        writer.Write(CliJsonEnvelope.Success(new { Text = "Grüße <b>" }));

        // Assert
        output.ToString()
            .Should().Be(
                "{\n" +
                "  \"ok\": true,\n" +
                "  \"data\": {\n" +
                "    \"text\": \"Grüße <b>\"\n" +
                "  },\n" +
                "  \"warnings\": [],\n" +
                "  \"error\": null\n" +
                "}\n");
    }

    [Fact]
    public void Write_Envelope_FlushesOutput()
    {
        // Arrange
        var output = A.Fake<TextWriter>(options => options.CallsBaseMethods());
        var writer = new CliJsonWriter(output);

        // Act
        writer.Write(CliJsonEnvelope.Success(new object()));

        // Assert
        A.CallTo(() => output.Flush())
            .MustHaveHappened();
    }

    [Fact]
    public void Write_NullEnvelope_ThrowsArgumentNullException()
    {
        // Arrange
        var writer = new CliJsonWriter(TextWriter.Null);

        // Act
        var act = () => writer.Write(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("envelope");
    }

    [Fact]
    public void Ctor_NullOutput_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CliJsonWriter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("output");
    }

    [Fact]
    public void Write_DataThatCannotBeSerialized_ThrowsAndWritesNothing()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);
        var cyclic = new CyclicData();
        cyclic.Self = cyclic;

        // Act
        var act = () => writer.Write(CliJsonEnvelope.Success(cyclic));

        // Assert
        act.Should().Throw<JsonException>();

        output.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public void Write_TwoEnvelopes_WritesTwoCompleteDocuments()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);

        // Act
        writer.Write(CliJsonEnvelope.Success(new { Value = 1 }));
        writer.Write(CliJsonEnvelope.Success(new { Value = 2 }));

        // Assert
        var text = output.ToString();
        var split = text.IndexOf("}\n{", StringComparison.Ordinal) + 2;

        JsonDocument.Parse(text[..split]).RootElement.GetProperty("data").GetProperty("value").GetInt32()
            .Should().Be(1);

        JsonDocument.Parse(text[split..]).RootElement.GetProperty("data").GetProperty("value").GetInt32()
            .Should().Be(2);
    }

    [Fact]
    public void Write_FailureEnvelope_WritesErrorCodeBeforeMessage()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);

        // Act
        writer.Write(CliJsonEnvelope.Failure(new CliJsonMessage("invalid-file", "Broken.")));

        // Assert
        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").EnumerateObject().Select(x => x.Name)
            .Should().Equal("code", "message");
    }

    [Fact]
    public void Write_DataWithNullProperty_WritesNullInsteadOfOmittingIt()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);

        // Act
        writer.Write(CliJsonEnvelope.Success(new { Value = (string?)null }));

        // Assert
        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("data").GetProperty("value").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Write_Envelope_SetsHasWritten()
    {
        // Arrange
        var writer = new CliJsonWriter(TextWriter.Null);

        // Act
        writer.Write(CliJsonEnvelope.Success(new object()));

        // Assert
        writer.HasWritten
            .Should().BeTrue();
    }

    [Fact]
    public void Write_DataThatCannotBeSerialized_LeavesHasWrittenFalse()
    {
        // Arrange
        var writer = new CliJsonWriter(TextWriter.Null);
        var cyclic = new CyclicData();
        cyclic.Self = cyclic;

        // Act
        var act = () => writer.Write(CliJsonEnvelope.Success(cyclic));

        // Assert
        act.Should().Throw<JsonException>();

        writer.HasWritten
            .Should().BeFalse();
    }

    [Fact]
    public void WriteIfNothingWritten_NothingWrittenYet_WritesEnvelope()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);

        // Act
        writer.WriteIfNothingWritten(
            CliJsonEnvelope.Failure(new CliJsonMessage(ShadeKitCliErrorCodes.HostError, "failed")));

        // Assert
        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.HostError);
    }

    [Fact]
    public void WriteIfNothingWritten_EnvelopeAlreadyWritten_WritesNoSecondDocument()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);
        writer.Write(CliJsonEnvelope.Success(new { Value = 1 }));
        var expected = output.ToString();

        // Act
        writer.WriteIfNothingWritten(
            CliJsonEnvelope.Failure(new CliJsonMessage(ShadeKitCliErrorCodes.HostError, "failed")));

        // Assert
        output.ToString()
            .Should().Be(expected);
    }

    public sealed class CyclicData
    {
        public CyclicData? Self { get; set; }
    }

    [Fact]
    public void Write_DataWithEnumValue_WritesLowerCaseKebabCaseName()
    {
        // Arrange
        using var output = new StringWriter();
        var writer = new CliJsonWriter(output);

        // Act
        writer.Write(CliJsonEnvelope.Success(new { Policy = OutdatedFormatOnReadPolicy.MigrateAndWarn }));

        // Assert
        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("data").GetProperty("policy").GetString()
            .Should().Be("migrate-and-warn");
    }

    [Theory]
    [InlineData(InitializedFileAction.Created, "created")]
    [InlineData(InitializedFileAction.Appended, "appended")]
    [InlineData(InitializedFileAction.Updated, "updated")]
    public void GetEnumName_FileAction_ReturnsNameThatJsonOutputUses(InitializedFileAction action, string expectedName)
    {
        // Arrange
        using var output = new StringWriter();
        new CliJsonWriter(output).Write(CliJsonEnvelope.Success(new { Action = action }));
        using var document = JsonDocument.Parse(output.ToString());

        // Act
        var name = CliJsonWriter.GetEnumName(action);

        // Assert
        name
            .Should().Be(expectedName)
            .And.Be(document.RootElement.GetProperty("data").GetProperty("action").GetString());
    }
}
