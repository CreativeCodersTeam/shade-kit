using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace CreativeCoders.ShadeKit.Cli.Tests;

public partial class ShadeKitCliErrorCodesTests
{
    [Theory]
    [Trait("Requirement", "FP01-AC8")]
    [InlineData(ShadeKitCliErrorCodes.KitRootNotFound, "kit-root-not-found")]
    [InlineData(ShadeKitCliErrorCodes.FeaturesDirectoryNotFound, "features-dir-not-found")]
    [InlineData(ShadeKitCliErrorCodes.NotAGitRepository, "not-a-git-repository")]
    [InlineData(ShadeKitCliErrorCodes.FormatTooNew, "format-too-new")]
    [InlineData(ShadeKitCliErrorCodes.UpgradeNeeded, "upgrade-needed")]
    [InlineData(ShadeKitCliErrorCodes.InvalidFile, "invalid-file")]
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
