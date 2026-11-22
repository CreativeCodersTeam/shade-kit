using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Versions;

namespace CreativeCoders.ShadeKit.Core.Tests.Versions;

public class CliContractTests
{
    [Fact]
    public void Window_Constants_AreCurrentAndMinSupported()
    {
        // Act
        var window = CliContract.Window;

        // Assert
        CliContract.MinSupported
            .Should().Be(1);

        CliContract.Current
            .Should().Be(1);

        window.MinSupported
            .Should().Be(CliContract.MinSupported);

        window.Current
            .Should().Be(CliContract.Current);
    }
}
