using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Versions;
using CreativeCoders.ShadeKit.Core.Versions.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Versions;

public class CliContractCheckerTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Check_RequestedWithinWindow_Succeeds(int requestedContract)
    {
        // Arrange
        var checker = new CliContractChecker(new CliContractWindow(2, 4));

        // Act
        var act = () => checker.Check(requestedContract);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Check_RequestedAboveCurrent_ThrowsEngineContractOutdatedExceptionWithContracts()
    {
        // Arrange
        var checker = new CliContractChecker(new CliContractWindow(2, 4));

        // Act
        var act = () => checker.Check(5);

        // Assert
        var exception = act.Should().Throw<EngineContractOutdatedException>().Which;

        exception.RequestedContract
            .Should().Be(5);

        exception.MinSupportedContract
            .Should().Be(2);

        exception.CurrentContract
            .Should().Be(4);
    }

    [Fact]
    public void Check_RequestedBelowMinSupported_ThrowsSkillsContractOutdatedExceptionWithContracts()
    {
        // Arrange
        var checker = new CliContractChecker(new CliContractWindow(2, 4));

        // Act
        var act = () => checker.Check(1);

        // Assert
        var exception = act.Should().Throw<SkillsContractOutdatedException>().Which;

        exception.RequestedContract
            .Should().Be(1);

        exception.MinSupportedContract
            .Should().Be(2);

        exception.CurrentContract
            .Should().Be(4);
    }

    [Fact]
    public void Check_RequestedZeroWithDefaultWindow_ThrowsSkillsContractOutdatedException()
    {
        // Arrange
        var checker = new CliContractChecker(CliContract.Window);

        // Act
        var act = () => checker.Check(0);

        // Assert
        var exception = act.Should().Throw<SkillsContractOutdatedException>().Which;

        exception.RequestedContract
            .Should().Be(0);

        exception.MinSupportedContract
            .Should().Be(CliContract.MinSupported);

        exception.CurrentContract
            .Should().Be(CliContract.Current);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Check_NegativeRequested_ThrowsArgumentOutOfRangeException(int requestedContract)
    {
        // Arrange
        var checker = new CliContractChecker(new CliContractWindow(2, 4));

        // Act
        var act = () => checker.Check(requestedContract);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("requestedContract");
    }

    [Fact]
    public void Ctor_NullWindow_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CliContractChecker(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("window");
    }

    [Fact]
    public void Window_Constructed_ReturnsWindowOfConstructor()
    {
        // Arrange
        var window = new CliContractWindow(2, 4);
        var checker = new CliContractChecker(window);

        // Act
        var actualWindow = checker.Window;

        // Assert
        actualWindow
            .Should().BeSameAs(window);
    }
}
