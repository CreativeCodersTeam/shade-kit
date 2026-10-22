using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace CreativeCoders.ShadeKit.Cli.Tests;

public partial class ShadeKitCliErrorCodesTests
{
    [Theory]
    [Trait("Requirement", "FP01-AC8")]
    [InlineData(ShadeKitCliErrorCodes.InvalidArguments, "invalid-arguments")]
    [InlineData(ShadeKitCliErrorCodes.KitRootNotFound, "kit-root-not-found")]
    [InlineData(ShadeKitCliErrorCodes.NotAGitRepository, "not-a-git-repository")]
    [InlineData(ShadeKitCliErrorCodes.KitAlreadyInitialized, "already-initialized")]
    [InlineData(ShadeKitCliErrorCodes.PathNotAFile, "path-not-a-file")]
    [InlineData(ShadeKitCliErrorCodes.KitRootNotADirectory, "kit-root-not-a-directory")]
    [InlineData(ShadeKitCliErrorCodes.FormatTooNew, "format-too-new")]
    [InlineData(ShadeKitCliErrorCodes.UpgradeNeeded, "upgrade-needed")]
    [InlineData(ShadeKitCliErrorCodes.InvalidFile, "invalid-file")]
    [InlineData(ShadeKitCliErrorCodes.InvalidManagedSection, "invalid-managed-section")]
    [InlineData(ShadeKitCliErrorCodes.UnsupportedFileEncoding, "unsupported-encoding")]
    [InlineData(ShadeKitCliErrorCodes.GitUnavailable, "git-unavailable")]
    [InlineData(ShadeKitCliErrorCodes.GitIdentityMissing, "git-identity-missing")]
    [InlineData(ShadeKitCliErrorCodes.FeatureAlreadyExists, "feature-already-exists")]
    [InlineData(ShadeKitCliErrorCodes.FeatureNotFound, "feature-not-found")]
    [InlineData(ShadeKitCliErrorCodes.InvalidFeatureId, "invalid-feature-id")]
    [InlineData(ShadeKitCliErrorCodes.InvalidFeatureSource, "invalid-source")]
    [InlineData(ShadeKitCliErrorCodes.InvalidFeatureTitle, "invalid-title")]
    [InlineData(ShadeKitCliErrorCodes.FeatureIoFailed, "feature-io-failed")]
    [InlineData(ShadeKitCliErrorCodes.UnknownProfile, "unknown-profile")]
    [InlineData(ShadeKitCliErrorCodes.Canceled, "canceled")]
    [InlineData(ShadeKitCliErrorCodes.HostError, "host-error")]
    public void ErrorCodes_Contract_HaveStableValues(string errorCode, string expectedErrorCode)
    {
        // Assert
        errorCode
            .Should().Be(expectedErrorCode);
    }

    [Fact]
    public void ErrorCodes_AllConstants_AreKebabCaseAndUnique()
    {
        // Act
        var errorCodes = ShadeKitCliExitCodesTests.GetConstants<string>(typeof(ShadeKitCliErrorCodes)).Values;

        // Assert
        errorCodes
            .Should().NotBeEmpty()
            .And.OnlyHaveUniqueItems()
            .And.AllSatisfy(code => KebabCaseRegex().IsMatch(code).Should().BeTrue(code));
    }

    [Fact]
    public void ForExitCode_EveryExitCodeConstant_ReturnsErrorCodeWithSameName()
    {
        // Arrange
        var exitCodes = ShadeKitCliExitCodesTests.GetConstants<int>(typeof(ShadeKitCliExitCodes));
        var errorCodes = ShadeKitCliExitCodesTests.GetConstants<string>(typeof(ShadeKitCliErrorCodes));

        // Act
        var mappedErrorCodes = exitCodes.ToDictionary(x => x.Key, x => ShadeKitCliErrorCodes.ForExitCode(x.Value));

        // Assert
        mappedErrorCodes
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(errorCodes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(49)]
    [InlineData(256)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ForExitCode_UndefinedExitCode_ThrowsArgumentOutOfRangeException(int exitCode)
    {
        // Act
        var act = () => ShadeKitCliErrorCodes.ForExitCode(exitCode);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("exitCode");
    }

    [GeneratedRegex("^[a-z]+(-[a-z]+)*$")]
    private static partial Regex KebabCaseRegex();
}
