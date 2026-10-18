using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Init;

namespace CreativeCoders.ShadeKit.Core.Tests.Init;

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
