using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Tests.Formats;

public class KitFileFormatExceptionTests
{
    [Fact]
    public void FormatTooNewException_Created_MessageNamesFileAndVersions()
    {
        // Act
        var exception = new FormatTooNewException("config.json", 3, 1, "2.0.0");

        // Assert
        exception.Message
            .Should().Contain("config.json")
            .And.Contain("3")
            .And.Contain("1");
    }

    [Fact]
    public void UpgradeNeededException_Created_MessageNamesFileVersionsAndUpgradeCommand()
    {
        // Act
        var exception = new UpgradeNeededException("config.json", 1, 2);

        // Assert
        exception.Message
            .Should().Contain("config.json")
            .And.Contain("version 1")
            .And.Contain("version is 2")
            .And.Contain("shade-kit upgrade");
    }

    [Fact]
    public void InvalidKitFileException_Created_MessageNamesFileAndReason()
    {
        // Arrange
        var innerException = new InvalidOperationException("inner");

        // Act
        var exception = new InvalidKitFileException("config.json", "The 'schemaVersion' field is missing.",
            innerException);

        // Assert
        exception.Message
            .Should().Be("The file 'config.json' is invalid: The 'schemaVersion' field is missing.");

        exception.FilePath
            .Should().Be("config.json");

        exception.InnerException
            .Should().BeSameAs(innerException);
    }
}
