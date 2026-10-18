using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Init;

namespace CreativeCoders.ShadeKit.Core.Tests.Init;

public class UnsupportedFileEncodingExceptionTests
{
    [Fact]
    public void Constructor_FilePathAndEncodingName_MessageNamesFileAndEncoding()
    {
        // Act
        var exception = new UnsupportedFileEncodingException("/repo/.gitignore", "UTF-16 LE");

        // Assert
        exception.Message
            .Should().Be("The file '/repo/.gitignore' is encoded as UTF-16 LE; only UTF-8 files can be changed.");

        exception.FilePath
            .Should().Be("/repo/.gitignore");

        exception.EncodingName
            .Should().Be("UTF-16 LE");
    }
}
