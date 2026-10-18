using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

public class CliCommandAbortTests
{
    private static readonly CliError Error = new(ShadeKitCliExitCodes.InvalidFile, ShadeKitCliErrorCodes.InvalidFile,
        "'/kit/[nested]/config.json' is invalid: Broken.");

    [Fact]
    public void Create_PlainMode_ReturnsPrintedAbortExceptionWithEscapedMessageAndWritesNoJson()
    {
        // Arrange
        var cause = new InvalidOperationException("cause");
        var jsonWriter = A.Fake<ICliJsonWriter>();

        // Act
        var exception = CliCommandAbort.Create(Error, cause, false, jsonWriter);

        // Assert
        exception.Message
            .Should().Be("'/kit/[[nested]]/config.json' is invalid: Broken.");

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidFile);

        exception.PrintMessage
            .Should().BeTrue();

        exception.InnerException
            .Should().BeSameAs(cause);

        A.CallTo(jsonWriter)
            .MustNotHaveHappened();
    }

    [Fact]
    public void Create_JsonMode_WritesFailureEnvelopeAndReturnsSilentAbortExceptionWithRawMessage()
    {
        // Arrange
        var cause = new InvalidOperationException("cause");
        using var jsonOutput = new StringWriter();

        // Act
        var exception = CliCommandAbort.Create(Error, cause, true, new CliJsonWriter(jsonOutput));

        // Assert
        exception.Message
            .Should().Be(Error.Message);

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidFile);

        exception.PrintMessage
            .Should().BeFalse();

        exception.InnerException
            .Should().BeSameAs(cause);

        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidFile);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be(Error.Message);
    }

    [Fact]
    public void Create_NullError_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliCommandAbort.Create(null!, new InvalidOperationException(), true, A.Fake<ICliJsonWriter>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("error");
    }

    [Fact]
    public void Create_NullException_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliCommandAbort.Create(Error, null!, true, A.Fake<ICliJsonWriter>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("exception");
    }

    [Fact]
    public void Create_NullJsonWriter_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliCommandAbort.Create(Error, new InvalidOperationException(), false, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jsonWriter");
    }
}
