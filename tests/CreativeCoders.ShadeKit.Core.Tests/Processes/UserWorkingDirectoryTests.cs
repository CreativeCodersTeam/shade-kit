using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Processes;

public class UserWorkingDirectoryTests
{
    private const string HomeDirectory = "/home/me";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Get_HomeDirectoryNotKnown_ReturnsTemporaryDirectory(string? homeDirectory)
    {
        // Act
        var workingDirectory = UserWorkingDirectory.Get(homeDirectory, static _ => true);

        // Assert
        workingDirectory
            .Should().Be(Path.GetTempPath());
    }

    [Fact]
    public void Get_HomeDirectoryDoesNotExist_ReturnsTemporaryDirectory()
    {
        // Act
        var workingDirectory = UserWorkingDirectory.Get(HomeDirectory, static _ => false);

        // Assert
        workingDirectory
            .Should().Be(Path.GetTempPath());
    }

    [Fact]
    public void Get_HomeDirectoryExists_ReturnsHomeDirectory()
    {
        // Act
        var workingDirectory = UserWorkingDirectory.Get(HomeDirectory, static x => x == HomeDirectory);

        // Assert
        workingDirectory
            .Should().Be(HomeDirectory);
    }

    [Fact]
    public void Get_CurrentProcess_ReturnsExistingHomeOrTemporaryDirectory()
    {
        // Act
        var workingDirectory = UserWorkingDirectory.Get();

        // Assert
        workingDirectory
            .Should().BeOneOf(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Path.GetTempPath());

        Directory.Exists(workingDirectory)
            .Should().BeTrue();
    }
}
