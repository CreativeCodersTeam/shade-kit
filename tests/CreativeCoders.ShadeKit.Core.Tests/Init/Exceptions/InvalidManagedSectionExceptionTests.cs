using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Init.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Init.Exceptions;

public class InvalidManagedSectionExceptionTests
{
    [Fact]
    public void Constructor_FilePathAndReason_MessageNamesFileAndReason()
    {
        // Act
        var exception = new InvalidManagedSectionException("/repo/CLAUDE.md", "The begin marker has no end marker.");

        // Assert
        exception.Message
            .Should().Be("The managed SHADE kit section in '/repo/CLAUDE.md' is invalid: " +
                         "The begin marker has no end marker.");

        exception.FilePath
            .Should().Be("/repo/CLAUDE.md");

        exception.Reason
            .Should().Be("The begin marker has no end marker.");
    }
}
