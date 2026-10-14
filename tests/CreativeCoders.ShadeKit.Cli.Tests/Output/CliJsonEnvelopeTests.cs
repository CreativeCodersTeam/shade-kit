using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

public class CliJsonEnvelopeTests
{
    [Fact]
    public void Success_DataWithoutWarnings_CreatesOkEnvelopeWithEmptyWarnings()
    {
        // Arrange
        var data = new { Value = 1 };

        // Act
        var envelope = CliJsonEnvelope.Success(data);

        // Assert
        envelope.Ok
            .Should().BeTrue();

        envelope.Data
            .Should().BeSameAs(data);

        envelope.Warnings
            .Should().BeEmpty();

        envelope.Error
            .Should().BeNull();
    }

    [Fact]
    public void Success_Warnings_KeepsWarningsInOrder()
    {
        // Arrange
        var first = new CliJsonMessage("upgrade-needed", "first");
        var second = new CliJsonMessage("upgrade-needed", "second");

        // Act
        var envelope = CliJsonEnvelope.Success(new object(), [first, second]);

        // Assert
        envelope.Warnings
            .Should().Equal(first, second);
    }

    [Fact]
    public void Failure_Error_CreatesFailedEnvelopeWithoutData()
    {
        // Arrange
        var error = new CliJsonMessage("kit-root-not-found", "message");

        // Act
        var envelope = CliJsonEnvelope.Failure(error);

        // Assert
        envelope.Ok
            .Should().BeFalse();

        envelope.Data
            .Should().BeNull();

        envelope.Warnings
            .Should().BeEmpty();

        envelope.Error
            .Should().Be(error);
    }

    [Fact]
    public void Failure_ErrorWithWarnings_KeepsWarnings()
    {
        // Arrange
        var warning = new CliJsonMessage("upgrade-needed", "warning");

        // Act
        var envelope = CliJsonEnvelope.Failure(new CliJsonMessage("invalid-file", "error"), [warning]);

        // Assert
        envelope.Warnings
            .Should().Equal(warning);
    }

    [Fact]
    public void Success_NullData_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliJsonEnvelope.Success(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("data");
    }

    [Fact]
    public void Failure_NullError_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CliJsonEnvelope.Failure(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("error");
    }

    [Fact]
    public void Success_WarningsListChangedAfterCreation_KeepsOriginalWarnings()
    {
        // Arrange
        var warning = new CliJsonMessage("upgrade-needed", "first");
        var warnings = new List<CliJsonMessage> { warning };

        // Act
        var envelope = CliJsonEnvelope.Success(new object(), warnings);
        warnings.Add(new CliJsonMessage("upgrade-needed", "second"));

        // Assert
        envelope.Warnings
            .Should().Equal(warning);
    }

    [Fact]
    public void Success_LazyWarnings_EnumeratesThemOnce()
    {
        // Arrange
        var enumerations = 0;

        IEnumerable<CliJsonMessage> CreateWarnings()
        {
            enumerations++;
            yield return new CliJsonMessage("upgrade-needed", "warning");
        }

        // Act
        var envelope = CliJsonEnvelope.Success(new object(), CreateWarnings());

        // Assert
        envelope.Warnings
            .Should().HaveCount(1);

        enumerations
            .Should().Be(1);
    }

    [Fact]
    public void Success_WarningsContainNull_ThrowsArgumentException()
    {
        // Act
        var act = () => CliJsonEnvelope.Success(new object(), [null!]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("warnings");
    }

    [Fact]
    public void Failure_WarningsContainNull_ThrowsArgumentException()
    {
        // Act
        var act = () => CliJsonEnvelope.Failure(new CliJsonMessage("invalid-file", "error"), [null!]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("warnings");
    }
}
