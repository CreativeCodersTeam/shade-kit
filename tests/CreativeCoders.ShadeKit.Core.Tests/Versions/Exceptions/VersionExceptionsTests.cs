using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Versions;
using CreativeCoders.ShadeKit.Core.Versions.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Versions.Exceptions;

public class VersionExceptionsTests
{
    [Fact]
    public void EngineVersionMismatchException_Versions_ExposesThemAndNamesThemInMessage()
    {
        // Act
        var exception = new EngineVersionMismatchException("1.2.0", "1.3.0-beta.1",
            EngineVersionMismatchDirection.EngineOlder);

        // Assert
        exception.EngineVersion
            .Should().Be("1.2.0");

        exception.ExpectedVersion
            .Should().Be("1.3.0-beta.1");

        exception.Direction
            .Should().Be(EngineVersionMismatchDirection.EngineOlder);

        exception.Message
            .Should().Be("The engine version '1.2.0' does not match the expected version '1.3.0-beta.1': the major " +
                         "and minor versions differ.");
    }

    [Fact]
    public void InvalidSemanticVersionException_Value_ExposesValueAndNamesItInMessage()
    {
        // Act
        var exception = new InvalidSemanticVersionException("v1.2");

        // Assert
        exception.Value
            .Should().Be("v1.2");

        exception.Message
            .Should().Be("'v1.2' is not a valid SemVer 2.0.0 version.");
    }
}
