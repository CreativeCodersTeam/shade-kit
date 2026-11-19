using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations;

public class AgentJsonFieldReaderTests
{
    private const string ToolName = "agent";

    private const string Subject = "entry 2";

    [Fact]
    public void ExitCodeText_ExitCode_NamesToolAndExitCode()
    {
        // Arrange
        var sut = CreateReader(new ExternalProcessResult(42, string.Empty, string.Empty));

        // Act
        var text = sut.ExitCodeText;

        // Assert
        text.Should().Be("agent exited with the exit code 42");
    }

    [Fact]
    public void ExitCodeText_NegativeExitCode_UsesInvariantMinusSign()
    {
        // Arrange
        var sut = CreateReader(new ExternalProcessResult(-1, string.Empty, string.Empty));

        // Act
        var text = sut.ExitCodeText;

        // Assert
        text.Should().Be("agent exited with the exit code -1");
    }

    [Theory]
    [InlineData(null, "agent output")]
    [InlineData(Subject, "entry 2 of the agent output")]
    [InlineData("", " of the agent output")]
    public void Describe_Subject_ReturnsDescription(string? subject, string expected)
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var description = sut.Describe(subject);

        // Assert
        description.Should().Be(expected);
    }

    [Fact]
    public void Fail_ReasonAndInnerException_PassesBothAndNoToolMessage()
    {
        // Arrange
        var sut = CreateReader();
        var innerException = new InvalidOperationException("inner");

        // Act
        var exception = sut.Fail("Something failed.", innerException);

        // Assert
        exception.Message.Should().Be("Something failed.");
        exception.ToolMessage.Should().BeNull();
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void Fail_ReasonOnly_HasNoInnerException()
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var exception = sut.Fail("Something failed.");

        // Assert
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void CreateExitCodeException_StandardErrorHasLines_NamesLastNonEmptyLineOfStandardError()
    {
        // Arrange
        var sut = CreateReader(new ExternalProcessResult(1, "output line\n",
            "warning: first\n  Error: plugin not found  \n\n   \n"));

        // Act
        var exception = sut.CreateExitCodeException();

        // Assert
        exception.Message.Should().Be("agent exited with the exit code 1: Error: plugin not found.");
        exception.ToolMessage.Should().Be("Error: plugin not found");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void CreateExitCodeException_StandardErrorOnlyWhitespace_NamesLastNonEmptyLineOfStandardOutput()
    {
        // Arrange
        var sut = CreateReader(new ExternalProcessResult(2, "first\nlast line.\n", " \n\t\n"));

        // Act
        var exception = sut.CreateExitCodeException();

        // Assert
        exception.Message.Should().Be("agent exited with the exit code 2: last line.");
        exception.ToolMessage.Should().Be("last line.");
    }

    [Fact]
    public void CreateExitCodeException_NoOutput_EndsExitCodeTextWithFullStop()
    {
        // Arrange
        var sut = CreateReader(new ExternalProcessResult(3, "  \n", string.Empty));

        // Act
        var exception = sut.CreateExitCodeException();

        // Assert
        exception.Message.Should().Be("agent exited with the exit code 3.");
        exception.ToolMessage.Should().BeNull();
    }

    [Theory]
    [InlineData("progress 10%\rprogress 100%\rfatal: offline")]
    [InlineData("first\r\nfatal: offline\r\n")]
    [InlineData("first\nfatal: offline\r")]
    public void CreateExitCodeException_CrLfOrLoneCr_SplitsLinesAtEveryLineEnd(string standardError)
    {
        // Arrange
        var sut = CreateReader(new ExternalProcessResult(1, string.Empty, standardError));

        // Act
        var exception = sut.CreateExitCodeException();

        // Assert
        exception.ToolMessage.Should().Be("fatal: offline");
        exception.Message.Should().Be("agent exited with the exit code 1: fatal: offline.");
    }

    [Fact]
    public void EnsureObject_Object_DoesNotThrow()
    {
        // Arrange
        var sut = CreateReader();
        var element = Parse("""{ "a": 1 }""");

        // Act
        var act = () => sut.EnsureObject(element, Subject);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("[]", Subject, "The entry 2 of the agent output is not a JSON object.")]
    [InlineData("\"text\"", null, "The agent output is not a JSON object.")]
    [InlineData("null", Subject, "The entry 2 of the agent output is not a JSON object.")]
    public void EnsureObject_NotObject_ThrowsWithDescribedSubject(string json, string? subject, string expected)
    {
        // Arrange
        var sut = CreateReader();
        var element = Parse(json);

        // Act
        var act = () => sut.EnsureObject(element, subject);

        // Assert
        act.Should().Throw<TestAgentException>().WithMessage(expected);
    }

    [Theory]
    [InlineData("""{ "name": "shade-kit" }""", "shade-kit")]
    [InlineData("""{ "name": " padded " }""", " padded ")]
    public void ReadRequiredString_NonEmptyString_ReturnsValueUnchanged(string json, string expected)
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var value = sut.ReadRequiredString(Parse(json), "name", Subject);

        // Assert
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "name": null }""")]
    [InlineData("""{ "name": "" }""")]
    [InlineData("""{ "name": "  " }""")]
    public void ReadRequiredString_MissingNullOrBlank_ThrowsNoNonEmptyString(string json)
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var act = () => sut.ReadRequiredString(Parse(json), "name", Subject);

        // Assert
        act.Should().Throw<TestAgentException>()
            .WithMessage("The entry 2 of the agent output has no non-empty 'name' string.");
    }

    [Fact]
    public void ReadRequiredString_NullSubject_DescribesOutputObject()
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var act = () => sut.ReadRequiredString(Parse("{}"), "name", null);

        // Assert
        act.Should().Throw<TestAgentException>().WithMessage("The agent output has no non-empty 'name' string.");
    }

    [Fact]
    public void ReadRequiredString_NotString_ThrowsNotAString()
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var act = () => sut.ReadRequiredString(Parse("""{ "name": 1 }"""), "name", Subject);

        // Assert
        act.Should().Throw<TestAgentException>()
            .WithMessage("The field 'name' of the entry 2 of the agent output is not a string.");
    }

    [Theory]
    [InlineData("{}", null)]
    [InlineData("""{ "ref": null }""", null)]
    [InlineData("""{ "ref": "" }""", "")]
    [InlineData("""{ "ref": "main" }""", "main")]
    public void ReadOptionalString_MissingNullOrString_ReturnsValue(string json, string? expected)
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var value = sut.ReadOptionalString(Parse(json), "ref", Subject);

        // Assert
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("""{ "ref": 1 }""")]
    [InlineData("""{ "ref": true }""")]
    [InlineData("""{ "ref": {} }""")]
    [InlineData("""{ "ref": [] }""")]
    public void ReadOptionalString_NotString_ThrowsNotAString(string json)
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var act = () => sut.ReadOptionalString(Parse(json), "ref", Subject);

        // Assert
        act.Should().Throw<TestAgentException>()
            .WithMessage("The field 'ref' of the entry 2 of the agent output is not a string.");
    }

    [Fact]
    public void ReadOptionalString_NotStringWithNullSubject_DescribesOutputObject()
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var act = () => sut.ReadOptionalString(Parse("""{ "ref": 1 }"""), "ref", null);

        // Assert
        act.Should().Throw<TestAgentException>()
            .WithMessage("The field 'ref' of the agent output is not a string.");
    }

    [Theory]
    [InlineData("""{ "enabled": true }""", true)]
    [InlineData("""{ "enabled": false }""", false)]
    public void ReadRequiredBoolean_Boolean_ReturnsValue(string json, bool expected)
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var value = sut.ReadRequiredBoolean(Parse(json), "enabled", Subject);

        // Assert
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "enabled": null }""")]
    [InlineData("""{ "enabled": "true" }""")]
    [InlineData("""{ "enabled": 1 }""")]
    public void ReadRequiredBoolean_MissingNullOrNotBoolean_ThrowsNoBoolean(string json)
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var act = () => sut.ReadRequiredBoolean(Parse(json), "enabled", Subject);

        // Assert
        act.Should().Throw<TestAgentException>()
            .WithMessage("The entry 2 of the agent output has no 'enabled' Boolean.");
    }

    [Fact]
    public void ReadRequiredBoolean_NullSubject_DescribesOutputObject()
    {
        // Arrange
        var sut = CreateReader();

        // Act
        var act = () => sut.ReadRequiredBoolean(Parse("{}"), "enabled", null);

        // Assert
        act.Should().Throw<TestAgentException>().WithMessage("The agent output has no 'enabled' Boolean.");
    }

    private static AgentJsonFieldReader<TestAgentException> CreateReader(ExternalProcessResult? result = null)
    {
        return new AgentJsonFieldReader<TestAgentException>(ToolName,
            result ?? new ExternalProcessResult(0, string.Empty, string.Empty),
            (reason, toolMessage, innerException) => new TestAgentException(reason, toolMessage, innerException));
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    public sealed class TestAgentException(string reason, string? toolMessage, Exception? innerException)
        : Exception(reason, innerException)
    {
        public string? ToolMessage { get; } = toolMessage;
    }
}
