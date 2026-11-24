using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Versions;

namespace CreativeCoders.ShadeKit.Core.Tests.Versions;

public class CliContractWindowTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    public void Ctor_MinSupportedBelowOne_ThrowsArgumentOutOfRangeException(int minSupported, int current)
    {
        // Act
        var act = () => new CliContractWindow(minSupported, current);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("minSupported");
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(5, 4)]
    public void Ctor_CurrentBelowMinSupported_ThrowsArgumentOutOfRangeException(int minSupported, int current)
    {
        // Act
        var act = () => new CliContractWindow(minSupported, current);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("current");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 4)]
    public void Ctor_ValidValues_ExposesThem(int minSupported, int current)
    {
        // Act
        var window = new CliContractWindow(minSupported, current);

        // Assert
        window.MinSupported
            .Should().Be(minSupported);

        window.Current
            .Should().Be(current);
    }
}
