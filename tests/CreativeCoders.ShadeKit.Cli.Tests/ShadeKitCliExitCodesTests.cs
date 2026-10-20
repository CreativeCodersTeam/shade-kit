using System.Reflection;
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
    [InlineData(ShadeKitCliExitCodes.NotAGitRepository)]
    [InlineData(ShadeKitCliExitCodes.KitAlreadyInitialized)]
    [InlineData(ShadeKitCliExitCodes.PathNotAFile)]
    [InlineData(ShadeKitCliExitCodes.KitRootNotADirectory)]
    [InlineData(ShadeKitCliExitCodes.FormatTooNew)]
    [InlineData(ShadeKitCliExitCodes.UpgradeNeeded)]
    [InlineData(ShadeKitCliExitCodes.InvalidFile)]
    [InlineData(ShadeKitCliExitCodes.InvalidManagedSection)]
    [InlineData(ShadeKitCliExitCodes.UnsupportedFileEncoding)]
    [InlineData(ShadeKitCliExitCodes.HostError)]
    public void ToProcessExitCode_NonNegativeExitCode_ReturnsExitCodeUnchanged(int exitCode)
    {
        // Act
        var processExitCode = ShadeKitCliExitCodes.ToProcessExitCode(exitCode);

        // Assert
        processExitCode
            .Should().Be(exitCode);
    }

    [Theory]
    [Trait("Requirement", "FP01-AC8")]
    [InlineData(ShadeKitCliExitCodes.KitRootNotFound, 10)]
    [InlineData(ShadeKitCliExitCodes.NotAGitRepository, 12)]
    [InlineData(ShadeKitCliExitCodes.KitAlreadyInitialized, 13)]
    [InlineData(ShadeKitCliExitCodes.PathNotAFile, 14)]
    [InlineData(ShadeKitCliExitCodes.KitRootNotADirectory, 15)]
    [InlineData(ShadeKitCliExitCodes.FormatTooNew, 20)]
    [InlineData(ShadeKitCliExitCodes.UpgradeNeeded, 21)]
    [InlineData(ShadeKitCliExitCodes.InvalidFile, 22)]
    [InlineData(ShadeKitCliExitCodes.InvalidManagedSection, 23)]
    [InlineData(ShadeKitCliExitCodes.UnsupportedFileEncoding, 24)]
    [InlineData(ShadeKitCliExitCodes.HostError, 255)]
    public void ExitCodes_Contract_HaveStableValues(int exitCode, int expectedExitCode)
    {
        // Assert
        exitCode
            .Should().Be(expectedExitCode);
    }

    [Fact]
    public void ExitCodes_AllConstants_AreUniqueAndInDocumentedRanges()
    {
        // Act
        var exitCodes = GetConstants<int>(typeof(ShadeKitCliExitCodes)).Values.ToArray();

        // Assert
        exitCodes
            .Should().OnlyHaveUniqueItems()
            .And.AllSatisfy(code => (code is >= 10 and <= 49 or 255).Should().BeTrue($"{code} is outside the ranges"));
    }

    [Fact]
    public void ExitCodes_EveryConstant_HasErrorCodeWithSameName()
    {
        // Act
        var exitCodeNames = GetConstants<int>(typeof(ShadeKitCliExitCodes)).Keys;
        var errorCodeNames = GetConstants<string>(typeof(ShadeKitCliErrorCodes)).Keys;

        // Assert
        exitCodeNames
            .Should().BeEquivalentTo(errorCodeNames);
    }

    [Theory]
    [InlineData(int.MinValue, ShadeKitCliExitCodes.HostError)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void ToProcessExitCode_ExtremeValues_MapsOnlyNegativeValues(int exitCode, int expectedExitCode)
    {
        // Act
        var processExitCode = ShadeKitCliExitCodes.ToProcessExitCode(exitCode);

        // Assert
        processExitCode
            .Should().Be(expectedExitCode);
    }

    internal static Dictionary<string, T> GetConstants<T>(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.IsLiteral && x.FieldType == typeof(T))
            .ToDictionary(x => x.Name, x => (T)x.GetRawConstantValue()!);
    }
}
