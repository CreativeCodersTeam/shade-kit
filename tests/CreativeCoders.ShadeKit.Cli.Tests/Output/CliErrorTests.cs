using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

public class CliErrorTests
{
    [Theory]
    [InlineData(ShadeKitCliExitCodes.InvalidArguments, ShadeKitCliErrorCodes.InvalidArguments)]
    [InlineData(ShadeKitCliExitCodes.FeatureNotFound, ShadeKitCliErrorCodes.FeatureNotFound)]
    [InlineData(ShadeKitCliExitCodes.Canceled, ShadeKitCliErrorCodes.Canceled)]
    [InlineData(ShadeKitCliExitCodes.HostError, ShadeKitCliErrorCodes.HostError)]
    public void Create_DefinedExitCode_UsesErrorCodeOfExitCode(int exitCode, string expectedCode)
    {
        // Act
        var error = CliError.Create(exitCode, "The message.");

        // Assert
        error
            .Should().Be(new CliError(exitCode, expectedCode, "The message."));
    }

    [Fact]
    public void Create_UndefinedExitCode_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => CliError.Create(1, "The message.");

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("exitCode");
    }
}
