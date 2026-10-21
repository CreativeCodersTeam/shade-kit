using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace CreativeCoders.ShadeKit.Cli.Tests;

public partial class ShadeKitCliWarningCodesTests
{
    [Theory]
    [InlineData(ShadeKitCliWarningCodes.CurrentFeatureMismatch, "current-feature-mismatch")]
    [InlineData(ShadeKitCliWarningCodes.CurrentFeatureMissing, "current-feature-missing")]
    [InlineData(ShadeKitCliWarningCodes.FeatureFileMissing, "feature-file-missing")]
    [InlineData(ShadeKitCliWarningCodes.FeatureFolderInvalid, "feature-folder-invalid")]
    [InlineData(ShadeKitCliWarningCodes.FeatureIdMismatch, "feature-id-mismatch")]
    [InlineData(ShadeKitCliWarningCodes.CurrentFeatureInvalid, "current-feature-invalid")]
    public void WarningCodes_Contract_HaveStableValues(string warningCode, string expectedWarningCode)
    {
        // Assert
        warningCode
            .Should().Be(expectedWarningCode);
    }

    [Fact]
    public void WarningCodes_AllConstants_AreKebabCaseAndUnique()
    {
        // Act
        var warningCodes = ShadeKitCliExitCodesTests.GetConstants<string>(typeof(ShadeKitCliWarningCodes)).Values;

        // Assert
        warningCodes
            .Should().NotBeEmpty()
            .And.OnlyHaveUniqueItems()
            .And.AllSatisfy(code => KebabCaseRegex().IsMatch(code).Should().BeTrue(code));
    }

    [Fact]
    public void WarningCodes_AllConstants_DifferFromErrorCodes()
    {
        // Act
        var warningCodes = ShadeKitCliExitCodesTests.GetConstants<string>(typeof(ShadeKitCliWarningCodes));
        var errorCodes = ShadeKitCliExitCodesTests.GetConstants<string>(typeof(ShadeKitCliErrorCodes));

        // Assert
        warningCodes.Values
            .Should().NotIntersectWith(errorCodes.Values);

        warningCodes.Keys
            .Should().NotIntersectWith(errorCodes.Keys);
    }

    [GeneratedRegex("^[a-z]+(-[a-z]+)*$")]
    private static partial Regex KebabCaseRegex();
}
