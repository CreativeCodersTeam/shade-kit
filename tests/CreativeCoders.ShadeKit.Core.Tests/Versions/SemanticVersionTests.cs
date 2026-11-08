using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Versions;

namespace CreativeCoders.ShadeKit.Core.Tests.Versions;

public class SemanticVersionTests
{
    [Theory]
    [InlineData("0.0.0", 0, 0, 0, null)]
    [InlineData("1.2.3", 1, 2, 3, null)]
    [InlineData("10.20.30", 10, 20, 30, null)]
    [InlineData("1.2.3-beta.1", 1, 2, 3, "beta.1")]
    [InlineData("1.0.0-alpha-a.b-c", 1, 0, 0, "alpha-a.b-c")]
    [InlineData("1.2.3-0", 1, 2, 3, "0")]
    [InlineData("1.2.3-0a.01a", 1, 2, 3, "0a.01a")]
    [InlineData("1.2.3--", 1, 2, 3, "-")]
    [InlineData("1.2.3+build.5", 1, 2, 3, null)]
    [InlineData("1.2.3+001.0-x", 1, 2, 3, null)]
    [InlineData("1.2.3-rc.1+sha.abc", 1, 2, 3, "rc.1")]
    [InlineData("0.7.1-feature-06.4", 0, 7, 1, "feature-06.4")]
    [InlineData("1.2.3-RC.1", 1, 2, 3, "RC.1")]
    [InlineData("1.2.3-2147483648", 1, 2, 3, "2147483648")]
    [InlineData("1.2.3-rc.99999999999999999999", 1, 2, 3, "rc.99999999999999999999")]
    [InlineData("2147483647.2147483647.2147483647", int.MaxValue, int.MaxValue, int.MaxValue, null)]
    public void TryParse_ValidVersion_ReturnsTrueWithParts(
        string value,
        int major,
        int minor,
        int patch,
        string? prerelease)
    {
        // Act
        var parsed = SemanticVersion.TryParse(value, out var version);

        // Assert
        parsed
            .Should().BeTrue();

        version
            .Should().NotBeNull();

        version.Major
            .Should().Be(major);

        version.Minor
            .Should().Be(minor);

        version.Patch
            .Should().Be(patch);

        version.Prerelease
            .Should().Be(prerelease);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 1.2.3")]
    [InlineData("1.2.3 ")]
    [InlineData("1.2.3\n")]
    [InlineData("\n1.2.3")]
    [InlineData("1.2.3\t")]
    [InlineData("1.2.3\r")]
    [InlineData("1.2.3\r\n")]
    [InlineData("1.2.3 ")]
    [InlineData("1. 2.3")]
    [InlineData("1.2.3-beta\n")]
    [InlineData("1.2.3+b\n")]
    [InlineData("1.2.3+a b")]
    [InlineData("v1.2.3")]
    [InlineData("V1.2.3")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2-beta")]
    [InlineData("1.2+build")]
    [InlineData("1.2.3.4-beta")]
    [InlineData("1..3")]
    [InlineData(".2.3")]
    [InlineData("1.2.")]
    [InlineData("..")]
    [InlineData("1.2.3.")]
    [InlineData("1.-2.3")]
    [InlineData("1.2.-3")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData("-1.2.3")]
    [InlineData("+1.2.3")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3-beta.01")]
    [InlineData("1.2.3-a..b")]
    [InlineData("1.2.3-a.")]
    [InlineData("1.2.3-.a")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3+a..b")]
    [InlineData("1.2.3+.a")]
    [InlineData("1.2.3+a.")]
    [InlineData("1.2.3-a+")]
    [InlineData("1.2.3+a+b")]
    [InlineData("1.2.3-a b")]
    [InlineData("1.2.3-a_b")]
    [InlineData("1.2.3-ä")]
    [InlineData("1.2.3+ä")]
    [InlineData("١.2.3")]
    [InlineData("1.2.3-١")]
    [InlineData("1.2.3-beta.١")]
    [InlineData("1.2.3-１")]
    [InlineData("1.2.3+١")]
    [InlineData("1.2.3+sha.１")]
    [InlineData("2147483648.0.0")]
    [InlineData("0.2147483648.0")]
    [InlineData("0.0.2147483648")]
    [InlineData("99999999999999999999.0.0")]
    [InlineData("a.b.c")]
    public void TryParse_InvalidVersion_ReturnsFalseAndNull(string value)
    {
        // Act
        var parsed = SemanticVersion.TryParse(value, out var version);

        // Assert
        parsed
            .Should().BeFalse();

        version
            .Should().BeNull();
    }

    [Fact]
    public void TryParse_Null_ReturnsFalseAndNull()
    {
        // Act
        var parsed = SemanticVersion.TryParse(null, out var version);

        // Assert
        parsed
            .Should().BeFalse();

        version
            .Should().BeNull();
    }

    [Fact]
    public void Parse_ValidVersion_ReturnsVersion()
    {
        // Act
        var version = SemanticVersion.Parse("1.2.3-beta.1+sha.abc");

        // Assert
        version.Major
            .Should().Be(1);

        version.Minor
            .Should().Be(2);

        version.Patch
            .Should().Be(3);

        version.Prerelease
            .Should().Be("beta.1");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1.2.3\n")]
    [InlineData("v1.2.3")]
    [InlineData("1.2")]
    [InlineData("1.2.3-01")]
    [InlineData("2147483648.0.0")]
    [InlineData("0.0.2147483648")]
    public void Parse_InvalidVersion_ThrowsFormatExceptionNamingValue(string value)
    {
        // Act
        var act = () => SemanticVersion.Parse(value);

        // Assert
        act
            .Should().Throw<FormatException>()
            .WithMessage($"'{value}' is not a valid SemVer 2.0.0 version.");
    }

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        // Act
        var act = () => SemanticVersion.Parse(null!);

        // Assert
        act
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("value");
    }

    [Theory]
    [InlineData("0.0.0", "0.0.0")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-beta.1", "1.2.3-beta.1")]
    [InlineData("1.2.3+build.5", "1.2.3")]
    [InlineData("1.2.3-rc.1+sha.abc", "1.2.3-rc.1")]
    [InlineData("1.0.0-alpha-a.b-c", "1.0.0-alpha-a.b-c")]
    [InlineData("1.2.3-RC.1", "1.2.3-RC.1")]
    [InlineData("2147483647.2147483647.2147483647", "2147483647.2147483647.2147483647")]
    public void ToString_ParsedVersion_ReturnsVersionWithoutBuildMetadata(string value, string expected)
    {
        // Arrange
        var version = SemanticVersion.Parse(value);

        // Act
        var text = version.ToString();

        // Assert
        text
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("1.2.3-beta.1")]
    [InlineData("1.2.3-0")]
    [InlineData("1.2.3-rc.1+sha.abc")]
    public void IsPrerelease_VersionWithPrereleaseLabel_ReturnsTrue(string value)
    {
        // Arrange
        var version = SemanticVersion.Parse(value);

        // Act
        var isPrerelease = version.IsPrerelease;

        // Assert
        isPrerelease
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("1.2.3+build.5")]
    public void IsPrerelease_VersionWithoutPrereleaseLabel_ReturnsFalse(string value)
    {
        // Arrange
        var version = SemanticVersion.Parse(value);

        // Act
        var isPrerelease = version.IsPrerelease;

        // Assert
        isPrerelease
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-beta.1", "1.2.3-beta.1")]
    [InlineData("1.2.3+build.5", "1.2.3")]
    [InlineData("1.2.3-beta.1+a", "1.2.3-beta.1+b")]
    public void Equals_SameVersionParts_AreEqual(string left, string right)
    {
        // Arrange
        var leftVersion = SemanticVersion.Parse(left);
        var rightVersion = SemanticVersion.Parse(right);

        // Act
        var areEqual = leftVersion == rightVersion;

        // Assert
        areEqual
            .Should().BeTrue();

        leftVersion.GetHashCode()
            .Should().Be(rightVersion.GetHashCode());
    }

    [Theory]
    [InlineData("1.2.3", "2.2.3")]
    [InlineData("1.2.3", "1.3.3")]
    [InlineData("1.2.3", "1.2.4")]
    [InlineData("1.2.3", "1.2.3-beta.1")]
    [InlineData("1.2.3-beta.1", "1.2.3-beta.2")]
    [InlineData("1.2.3-RC.1", "1.2.3-rc.1")]
    public void Equals_DifferentVersionParts_AreNotEqual(string left, string right)
    {
        // Arrange
        var leftVersion = SemanticVersion.Parse(left);
        var rightVersion = SemanticVersion.Parse(right);

        // Act
        var areNotEqual = leftVersion != rightVersion;

        // Assert
        areNotEqual
            .Should().BeTrue();
    }

    [Fact]
    public void Equals_NullOrOtherType_ReturnsFalse()
    {
        // Arrange
        var version = SemanticVersion.Parse("1.2.3");

        // Act
        var equalsString = version.Equals((object)"1.2.3");
        var equalsNull = version.Equals(null);

        // Assert
        equalsNull
            .Should().BeFalse();

        equalsString
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("1.2.3+build.5")]
    [InlineData("1.2.3-rc.1+sha.abc")]
    [InlineData("1.0.0-alpha-a.b-c")]
    public void Parse_ToStringOfParsedVersion_ReturnsEqualVersion(string value)
    {
        // Arrange
        var version = SemanticVersion.Parse(value);

        // Act
        var reparsed = SemanticVersion.Parse(version.ToString());

        // Assert
        reparsed
            .Should().Be(version);
    }
}
