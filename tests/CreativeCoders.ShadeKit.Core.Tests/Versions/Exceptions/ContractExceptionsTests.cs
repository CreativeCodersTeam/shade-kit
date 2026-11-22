using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Versions.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Versions.Exceptions;

public class ContractExceptionsTests
{
    [Fact]
    public void EngineContractOutdatedException_Contracts_ExposesThemAndNamesThemInMessage()
    {
        // Act
        var exception = new EngineContractOutdatedException(5, 2, 4);

        // Assert
        exception.RequestedContract
            .Should().Be(5);

        exception.MinSupportedContract
            .Should().Be(2);

        exception.CurrentContract
            .Should().Be(4);

        exception.Message
            .Should().Be("The kit skills need the CLI contract 5, but this engine supports only the CLI contracts 2 " +
                         "to 4.");
    }

    [Fact]
    public void SkillsContractOutdatedException_Contracts_ExposesThemAndNamesThemInMessage()
    {
        // Act
        var exception = new SkillsContractOutdatedException(1, 2, 4);

        // Assert
        exception.RequestedContract
            .Should().Be(1);

        exception.MinSupportedContract
            .Should().Be(2);

        exception.CurrentContract
            .Should().Be(4);

        exception.Message
            .Should().Be("The kit skills are written for the CLI contract 1, but this engine supports only the CLI " +
                         "contracts 2 to 4.");
    }
}
