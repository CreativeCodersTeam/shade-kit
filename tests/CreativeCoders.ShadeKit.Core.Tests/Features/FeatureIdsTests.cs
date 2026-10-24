using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public class FeatureIdsTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("0")]
    [InlineData("login")]
    [InlineData("gh-142-login-sso")]
    [InlineData("20261013-1530-login-sso")]
    [InlineData("a-1-b-2")]
    public void IsValid_ValidId_ReturnsTrue(string featureId)
    {
        // Act
        var isValid = FeatureIds.IsValid(featureId);

        // Assert
        isValid
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../gh-142")]
    [InlineData("gh/142")]
    [InlineData("gh\\142")]
    [InlineData("Gh-142")]
    [InlineData("GH-142")]
    [InlineData("-gh-142")]
    [InlineData("gh-142-")]
    [InlineData("gh--142")]
    [InlineData("-")]
    [InlineData("gh 142")]
    [InlineData("gh_142")]
    [InlineData("gh.142")]
    [InlineData("gh-142\n")]
    [InlineData("\ngh-142")]
    [InlineData("über")]
    public void IsValid_InvalidId_ReturnsFalse(string? featureId)
    {
        // Act
        var isValid = FeatureIds.IsValid(featureId);

        // Assert
        isValid
            .Should().BeFalse();
    }

    [Fact]
    public void EnsureValid_ValidId_DoesNotThrow()
    {
        // Act
        var act = () => FeatureIds.EnsureValid("gh-142-login-sso");

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("gh/142")]
    [InlineData("GH-142")]
    public void EnsureValid_InvalidId_ThrowsInvalidFeatureIdExceptionNamingId(string featureId)
    {
        // Act
        var act = () => FeatureIds.EnsureValid(featureId);

        // Assert
        act.Should().Throw<InvalidFeatureIdException>()
            .Which.FeatureId.Should().Be(featureId);
    }

    [Fact]
    public void EnsureValid_NullId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => FeatureIds.EnsureValid(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("featureId");
    }
}
