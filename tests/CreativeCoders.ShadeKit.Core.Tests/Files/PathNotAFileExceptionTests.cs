using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

public class PathNotAFileExceptionTests
{
    [Fact]
    public void Constructor_Path_MessageNamesPath()
    {
        // Act
        var exception = new PathNotAFileException("/repo/CLAUDE.md");

        // Assert
        exception.Message
            .Should().Be("The path '/repo/CLAUDE.md' is a directory, but a file is expected.");

        exception.Path
            .Should().Be("/repo/CLAUDE.md");
    }
}
