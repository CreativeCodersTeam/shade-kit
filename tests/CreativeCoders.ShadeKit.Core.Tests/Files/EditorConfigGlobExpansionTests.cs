using System.Diagnostics;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

public class EditorConfigGlobExpansionTests
{
    [Theory]
    [InlineData("/repo/**/*.cs")]
    [InlineData("/repo/**/*.{cs,md}")]
    [InlineData("/repo/**/{1..10000}")]
    [InlineData("/repo/**/{1..100}{1..100}")]
    [InlineData("/repo/**/{-5000..4999}")]
    [InlineData("/repo/**/{10000..1}")]
    [InlineData("/repo/**/\\{1..100000}")]
    [InlineData("/repo/**/{1..100000")]
    [InlineData("#/repo/**/{1..100000}")]
    [InlineData("")]
    public void IsTooLarge_GlobUpToLimit_ReturnsFalse(string glob)
    {
        // Act
        var isTooLarge = EditorConfigGlobExpansion.IsTooLarge(glob);

        // Assert
        isTooLarge
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("/repo/**/{1..10001}")]
    [InlineData("/repo/**/{1..20000}{1..20000}")]
    [InlineData("/repo/**/{1..100000000}")]
    [InlineData("/repo/**/x{1..2147483647}")]
    [InlineData("/repo/**/{1..99999999999}")]
    [InlineData("/repo/**/{-2147483648..2147483647}")]
    [InlineData("!/repo/**/{1..20000}")]
    [InlineData("  /repo/**/{1..20000}  ")]
    [InlineData("/repo/**/{a,{1..20000}}")]
    [InlineData("/repo/**/{1..101}{1..100}")]
    public void IsTooLarge_GlobAboveLimit_ReturnsTrue(string glob)
    {
        // Act
        var isTooLarge = EditorConfigGlobExpansion.IsTooLarge(glob);

        // Assert
        isTooLarge
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(13, false)]
    [InlineData(14, true)]
    [InlineData(40, true)]
    public void IsTooLarge_RepeatedAlternatives_CountsEveryCombination(int groupCount, bool expected)
    {
        // Arrange: 2^13 = 8192 patterns, 2^14 = 16384 patterns.
        var glob = "/repo/**/" + string.Concat(Enumerable.Repeat("{a,b}", groupCount));

        // Act
        var isTooLarge = EditorConfigGlobExpansion.IsTooLarge(glob);

        // Assert
        isTooLarge
            .Should().Be(expected);
    }

    [Fact]
    public void IsTooLarge_HugeGlob_ReturnsWithoutExpandingIt()
    {
        // Arrange
        var glob = "/repo/**/" + string.Concat(Enumerable.Repeat("{1..2000000000}", 50));
        var stopwatch = Stopwatch.StartNew();

        // Act
        var isTooLarge = EditorConfigGlobExpansion.IsTooLarge(glob);

        // Assert
        isTooLarge
            .Should().BeTrue();

        stopwatch.Elapsed
            .Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void IsTooLarge_NullGlob_ThrowsArgumentNullException()
    {
        // Act
        var act = () => EditorConfigGlobExpansion.IsTooLarge(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("glob");
    }
}
