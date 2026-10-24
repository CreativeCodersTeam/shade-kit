using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

public class CliCommandAbortTests
{
    private static readonly CliError Error = new CliError(ShadeKitCliExitCodes.InvalidFile,
        ShadeKitCliErrorCodes.InvalidFile, "'/kit/[nested]/config.json' is invalid: Broken.");

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

    [Fact]
    public void Run_OperationSucceeds_ReturnsResultAndWritesNoJson()
    {
        // Arrange
        var jsonWriter = A.Fake<ICliJsonWriter>();

        // Act
        var result = CliCommandAbort.Run(() => 42, CreateMapper(), true, jsonWriter);

        // Assert
        result
            .Should().Be(42);

        A.CallTo(jsonWriter)
            .MustNotHaveHappened();
    }

    [Fact]
    public void Run_OperationThrowsMappedException_ThrowsAbortExceptionWithMappedExitCode()
    {
        // Arrange
        var cause = new KitRootPathNotFoundException("/repo/src", "/repo");
        using var jsonOutput = new StringWriter();

        // Act
        var act = () => CliCommandAbort.Run<int>(() => throw cause, CreateMapper(), true,
            new CliJsonWriter(jsonOutput));

        // Assert
        var exception = act.Should().Throw<CliCommandAbortException>().Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.InnerException
            .Should().BeSameAs(cause);

        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.KitRootNotFound);
    }

    [Fact]
    public void Run_OperationThrowsUnmappedException_LetsItEscapeAndWritesNoJson()
    {
        // Arrange
        var cause = new InvalidOperationException("unexpected");
        var jsonWriter = A.Fake<ICliJsonWriter>();

        // Act
        var act = () => CliCommandAbort.Run<int>(() => throw cause, CreateMapper(), true, jsonWriter);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .Which.Should().BeSameAs(cause);

        A.CallTo(jsonWriter)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task RunAsync_OperationSucceeds_ReturnsResult()
    {
        // Act
        var result = await CliCommandAbort.RunAsync(() => Task.FromResult("done"), CreateMapper(), false,
            A.Fake<ICliJsonWriter>());

        // Assert
        result
            .Should().Be("done");
    }

    [Fact]
    public async Task RunAsync_OperationFaultsWithMappedException_ThrowsPrintedAbortExceptionInPlainMode()
    {
        // Arrange
        var cause = new NotAGitRepositoryException("/tmp/[x]");
        var jsonWriter = A.Fake<ICliJsonWriter>();

        // Act
        var act = () => CliCommandAbort.RunAsync(() => Task.FromException<int>(cause), CreateMapper(), false,
            jsonWriter);

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.NotAGitRepository);

        exception.PrintMessage
            .Should().BeTrue();

        exception.Message
            .Should().Contain("'/tmp/[[x]]'");

        A.CallTo(jsonWriter)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task RunAsync_OperationThrowsUnmappedException_LetsItEscape()
    {
        // Act
        var act = () => CliCommandAbort.RunAsync<int>(() => throw new InvalidOperationException("unexpected"),
            CreateMapper(), true, A.Fake<ICliJsonWriter>());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Run_NullOperation_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliCommandAbort.Run<int>(null!, CreateMapper(), true, A.Fake<ICliJsonWriter>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("operation");
    }

    [Fact]
    public async Task RunAsync_NullErrorMapper_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliCommandAbort.RunAsync(() => Task.FromResult(1), null!, true, A.Fake<ICliJsonWriter>());

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("errorMapper");
    }

    private static CliErrorMapper CreateMapper()
    {
        return new CliErrorMapper(A.Fake<IEngineVersionProvider>());
    }
}
