using AwesomeAssertions;
using CreativeCoders.Cli.Hosting;

namespace CreativeCoders.ShadeKit.Cli.Tests;

public class ShadeKitCliExitCodesTests
{
    [Theory]
    [InlineData(CliExitCodes.CommandNotFound)]
    [InlineData(CliExitCodes.CommandCreationFailed)]
    [InlineData(CliExitCodes.CommandResultUnknown)]
    [InlineData(CliExitCodes.CommandOptionsInvalid)]
    [InlineData(CliExitCodes.PreProcessorFailed)]
    [InlineData(CliExitCodes.PostProcessorFailed)]
    [InlineData(-1)]
    public void ToProcessExitCode_NegativeHostExitCode_ReturnsHostError(int exitCode)
    {
        // Act
        var processExitCode = ShadeKitCliExitCodes.ToProcessExitCode(exitCode);

        // Assert
        processExitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);
    }

    [Theory]
    [InlineData(CliExitCodes.Success)]
    [InlineData(ShadeKitCliExitCodes.KitRootNotFound)]
    [InlineData(ShadeKitCliExitCodes.FeaturesDirectoryNotFound)]
    [InlineData(ShadeKitCliExitCodes.HostError)]
    public void ToProcessExitCode_NonNegativeExitCode_ReturnsExitCodeUnchanged(int exitCode)
    {
        // Act
        var processExitCode = ShadeKitCliExitCodes.ToProcessExitCode(exitCode);

        // Assert
        processExitCode
            .Should().Be(exitCode);
    }
}
