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
    [InlineData(ShadeKitCliExitCodes.InvalidArguments)]
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
    [InlineData(ShadeKitCliExitCodes.GitUnavailable)]
    [InlineData(ShadeKitCliExitCodes.GitIdentityMissing)]
    [InlineData(ShadeKitCliExitCodes.NoCurrentFeature)]
    [InlineData(ShadeKitCliExitCodes.FeatureAlreadyExists)]
    [InlineData(ShadeKitCliExitCodes.FeatureNotFound)]
    [InlineData(ShadeKitCliExitCodes.InvalidFeatureId)]
    [InlineData(ShadeKitCliExitCodes.InvalidFeatureSource)]
    [InlineData(ShadeKitCliExitCodes.InvalidFeatureTitle)]
    [InlineData(ShadeKitCliExitCodes.FeatureIoFailed)]
    [InlineData(ShadeKitCliExitCodes.UnknownProfile)]
    [InlineData(ShadeKitCliExitCodes.PhaseNotInProfile)]
    [InlineData(ShadeKitCliExitCodes.TransitionNotAllowed)]
    [InlineData(ShadeKitCliExitCodes.NoPendingTransition)]
    [InlineData(ShadeKitCliExitCodes.FeatureDone)]
    [InlineData(ShadeKitCliExitCodes.CompleteNotAllowed)]
    [InlineData(ShadeKitCliExitCodes.PhaseStateInvalid)]
    [InlineData(ShadeKitCliExitCodes.Canceled)]
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
    [InlineData(ShadeKitCliExitCodes.InvalidArguments, 2)]
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
    [InlineData(ShadeKitCliExitCodes.GitUnavailable, 16)]
    [InlineData(ShadeKitCliExitCodes.GitIdentityMissing, 36)]
    [InlineData(ShadeKitCliExitCodes.NoCurrentFeature, 37)]
    [InlineData(ShadeKitCliExitCodes.FeatureAlreadyExists, 30)]
    [InlineData(ShadeKitCliExitCodes.FeatureNotFound, 31)]
    [InlineData(ShadeKitCliExitCodes.InvalidFeatureId, 32)]
    [InlineData(ShadeKitCliExitCodes.InvalidFeatureSource, 33)]
    [InlineData(ShadeKitCliExitCodes.InvalidFeatureTitle, 34)]
    [InlineData(ShadeKitCliExitCodes.FeatureIoFailed, 35)]
    [InlineData(ShadeKitCliExitCodes.UnknownProfile, 40)]
    [InlineData(ShadeKitCliExitCodes.PhaseNotInProfile, 41)]
    [InlineData(ShadeKitCliExitCodes.TransitionNotAllowed, 42)]
    [InlineData(ShadeKitCliExitCodes.NoPendingTransition, 43)]
    [InlineData(ShadeKitCliExitCodes.FeatureDone, 44)]
    [InlineData(ShadeKitCliExitCodes.CompleteNotAllowed, 45)]
    [InlineData(ShadeKitCliExitCodes.PhaseStateInvalid, 46)]
    [InlineData(ShadeKitCliExitCodes.Canceled, 130)]
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
            .And.AllSatisfy(code =>
                (code is 2 or >= 10 and <= 49 or 130 or 255).Should().BeTrue($"{code} is outside the ranges"));
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
