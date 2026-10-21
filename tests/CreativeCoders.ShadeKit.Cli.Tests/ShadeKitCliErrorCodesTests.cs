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

    [GeneratedRegex("^[a-z]+(-[a-z]+)*$")]
    private static partial Regex KebabCaseRegex();
}
