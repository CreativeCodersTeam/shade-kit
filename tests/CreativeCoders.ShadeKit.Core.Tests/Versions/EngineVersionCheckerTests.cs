using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Versions;
using CreativeCoders.ShadeKit.Core.Versions.Exceptions;
using CreativeCoders.ShadeKit.Core.Versions.Warnings;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Versions;

public class EngineVersionCheckerTests
{
    [Theory]
    [Trait("Requirement", "FP06-1-AC3")]
    [InlineData("1.2.0", "1.2.0")]
    [InlineData("1.2.0", "1.2.7")]
    [InlineData("1.2.7", "1.2.0")]
    [InlineData("0.1.3", "0.1.0")]
    public void Check_ReleaseEngineWithSameMajorAndMinor_ReturnsResultWithoutWarnings(string engineVersion,
        string expectedVersion)
    {
        // Arrange
        var checker = CreateChecker(engineVersion);

        // Act
        var result = checker.Check(expectedVersion);

        // Assert
        result.EngineVersion
            .Should().Be(engineVersion);

        result.ExpectedVersion
            .Should().Be(expectedVersion);

        result.Warnings
            .Should().BeEmpty();
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC3")]
    [InlineData("1.2.0-beta.1", "1.2.0")]
    [InlineData("1.2.3-rc.1", "1.2.0")]
    [InlineData("1.2.0-alpha", "1.2.5-beta.2")]
    public void Check_PrereleaseEngineWithSameMajorAndMinor_ReturnsResultWithoutWarnings(string engineVersion,
        string expectedVersion)
    {
        // Arrange
        var checker = CreateChecker(engineVersion);

        // Act
        var result = checker.Check(expectedVersion);

        // Assert
        result.Warnings
            .Should().BeEmpty();
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC3")]
    [InlineData("1.2.0-beta.1")]
    [InlineData("1.2.0+build.5")]
    [InlineData("1.2.9-rc.1+sha.abc")]
    public void Check_ExpectedVersionWithPrereleaseOrBuildMetadata_IgnoresThemAndKeepsValueAsPassed(
        string expectedVersion)
    {
        // Arrange
        var checker = CreateChecker("1.2.0");

        // Act
        var result = checker.Check(expectedVersion);

        // Assert
        result.ExpectedVersion
            .Should().Be(expectedVersion);

        result.Warnings
            .Should().BeEmpty();
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC1")]
    [Trait("Requirement", "FP06-1-AC2")]
    [InlineData("1.2.0", "1.3.0")]
    [InlineData("1.2.9", "2.0.0")]
    [InlineData("1.9.0", "2.1.0")]
    [InlineData("1.2.0", "1.3.0-beta.1")]
    [InlineData("0.9.0", "1.0.0+build.1")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("0.0.0", "0.2147483647.0")]
    [InlineData("1.2.0+sha.abc", "1.3.0")]
    public void Check_ReleaseEngineWithLowerMajorOrMinor_ThrowsMismatchWithEngineOlder(string engineVersion,
        string expectedVersion)
    {
        // Arrange
        var checker = CreateChecker(engineVersion);

        // Act
        var act = () => checker.Check(expectedVersion);

        // Assert
        var exception = act.Should().Throw<EngineVersionMismatchException>().Which;

        exception.EngineVersion
            .Should().Be(engineVersion);

        exception.ExpectedVersion
            .Should().Be(expectedVersion);

        exception.Direction
            .Should().Be(EngineVersionMismatchDirection.EngineOlder);
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC1")]
    [Trait("Requirement", "FP06-1-AC2")]
    [InlineData("1.3.0", "1.2.0")]
    [InlineData("2.0.0", "1.9.9")]
    [InlineData("2.1.0", "1.9.0")]
    [InlineData("1.3.0", "1.2.9-rc.1")]
    [InlineData("1.10.0", "1.9.0")]
    [InlineData("2147483647.0.0", "0.0.0")]
    [InlineData("1.3.0+sha.abc", "1.2.0")]
    public void Check_ReleaseEngineWithHigherMajorOrMinor_ThrowsMismatchWithEngineNewer(string engineVersion,
        string expectedVersion)
    {
        // Arrange
        var checker = CreateChecker(engineVersion);

        // Act
        var act = () => checker.Check(expectedVersion);

        // Assert
        var exception = act.Should().Throw<EngineVersionMismatchException>().Which;

        exception.EngineVersion
            .Should().Be(engineVersion);

        exception.ExpectedVersion
            .Should().Be(expectedVersion);

        exception.Direction
            .Should().Be(EngineVersionMismatchDirection.EngineNewer);
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC4")]
    [InlineData("1.2.0-beta.1", "1.3.0", EngineVersionMismatchDirection.EngineOlder)]
    [InlineData("1.9.0-rc.2", "2.0.0", EngineVersionMismatchDirection.EngineOlder)]
    [InlineData("1.3.0-beta.1", "1.2.0", EngineVersionMismatchDirection.EngineNewer)]
    [InlineData("2.0.0-alpha", "1.9.0+build.3", EngineVersionMismatchDirection.EngineNewer)]
    public void Check_PrereleaseEngineWithDifferentMajorOrMinor_ReturnsResultWithPrereleaseWarning(
        string engineVersion, string expectedVersion, EngineVersionMismatchDirection expectedDirection)
    {
        // Arrange
        var checker = CreateChecker(engineVersion);

        // Act
        var result = checker.Check(expectedVersion);

        // Assert
        result.EngineVersion
            .Should().Be(engineVersion);

        result.ExpectedVersion
            .Should().Be(expectedVersion);

        result.Warnings
            .Should().Equal(new EnginePrereleaseVersionMismatchWarning(engineVersion, expectedVersion,
                expectedDirection));
    }

    [Theory]
    [Trait("Requirement", "FP06-1-AC5")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("v1.2.0")]
    [InlineData("1.2")]
    [InlineData("1.2.0.0")]
    [InlineData(" 1.2.0")]
    [InlineData("01.2.0")]
    [InlineData("latest")]
    public void Check_InvalidExpectedVersion_ThrowsInvalidSemanticVersionExceptionWithValue(string expectedVersion)
    {
        // Arrange
        var checker = CreateChecker("1.2.0");

        // Act
        var act = () => checker.Check(expectedVersion);

        // Assert
        act.Should().Throw<InvalidSemanticVersionException>()
            .Which.Value.Should().Be(expectedVersion);
    }

    [Fact]
    public void Check_InvalidExpectedVersionAndInvalidEngineVersion_ThrowsInvalidSemanticVersionException()
    {
        // Arrange
        var checker = CreateChecker("1.0.0.0");

        // Act
        var act = () => checker.Check("1.2");

        // Assert
        act.Should().Throw<InvalidSemanticVersionException>()
            .Which.Value.Should().Be("1.2");
    }

    [Theory]
    [InlineData("1.0.0.0")]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.0-")]
    [InlineData("v1.2.0")]
    public void Check_InvalidEngineVersion_ThrowsInvalidOperationExceptionNamingEngineVersion(string engineVersion)
    {
        // Arrange
        var checker = CreateChecker(engineVersion);

        // Act
        var act = () => checker.Check("1.2.0");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"The engine version '{engineVersion}' is not a valid SemVer 2.0.0 version*");
    }

    [Fact]
    public void Check_NullExpectedVersionAndInvalidEngineVersion_ThrowsArgumentNullException()
    {
        // Arrange
        var checker = CreateChecker("1.0.0.0");

        // Act
        var act = () => checker.Check(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("expectedVersion");
    }

    [Fact]
    public void Check_NullExpectedVersion_ThrowsArgumentNullException()
    {
        // Arrange
        var checker = CreateChecker("1.2.0");

        // Act
        var act = () => checker.Check(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("expectedVersion");
    }

    [Fact]
    public void Ctor_NullEngineVersionProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new EngineVersionChecker(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("engineVersionProvider");
    }

    private static EngineVersionChecker CreateChecker(string engineVersion)
    {
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).Returns(engineVersion);

        return new EngineVersionChecker(engineVersionProvider);
    }
}
