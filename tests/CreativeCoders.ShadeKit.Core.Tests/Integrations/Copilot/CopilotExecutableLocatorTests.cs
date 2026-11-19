using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Copilot;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Copilot;

// The PATH rules are covered by PathExecutableSearchTests and the caching of the shared base class by
// GitExecutableLocatorTests; these tests cover the copilot names.
public class CopilotExecutableLocatorTests
{
    private static readonly Func<string, bool> AllExecutable = static _ => true;

    [Theory]
    [InlineData("/opt/none:/usr/local/bin:/usr/bin", "/usr/local/bin/copilot")]
    [InlineData("/home/me/.local/bin/", "/home/me/.local/bin/copilot")]
    public void FindCopilotExecutable_UnixPathContainsCopilot_ReturnsAbsoluteCopilotPath(string pathVariable,
        string expected)
    {
        // Arrange
        var sut = new CopilotExecutableLocator(() => pathVariable, false, x => x == expected, AllExecutable);

        // Act
        var result = sut.FindCopilotExecutable();

        // Assert
        result
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("/opt/bin/copilot.cmd")]
    [InlineData("/opt/bin/copilot.exe")]
    public void FindCopilotExecutable_UnixPathContainsOnlyWindowsNames_ReturnsNull(string existingFile)
    {
        // Arrange
        var sut = new CopilotExecutableLocator(() => "/opt/bin", false, x => x == existingFile, AllExecutable);

        // Act
        var result = sut.FindCopilotExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindCopilotExecutable_WindowsDirectoryContainsExeAndCmd_ReturnsExe()
    {
        // Arrange
        string[] existingFiles = [@"C:\npm\copilot.cmd", @"C:\npm\copilot.exe"];
        var sut = new CopilotExecutableLocator(() => @"C:\npm", true, existingFiles.Contains, AllExecutable);

        // Act
        var result = sut.FindCopilotExecutable();

        // Assert
        result
            .Should().Be(@"C:\npm\copilot.exe");
    }

    [Fact]
    public void FindCopilotExecutable_WindowsDirectoryContainsOnlyNpmShims_ReturnsCmd()
    {
        // Arrange
        string[] existingFiles = [@"C:\npm\copilot.cmd", @"C:\npm\copilot.ps1", @"C:\npm\copilot"];
        var sut = new CopilotExecutableLocator(() => @"C:\npm", true, existingFiles.Contains, AllExecutable);

        // Act
        var result = sut.FindCopilotExecutable();

        // Assert
        result
            .Should().Be(@"C:\npm\copilot.cmd");
    }

    [Fact]
    public void FindCopilotExecutable_WindowsCmdInEarlierDirectoryThanExe_ReturnsCmdOfEarlierDirectory()
    {
        // Arrange
        string[] existingFiles = [@"C:\npm\copilot.cmd", @"C:\Tools\copilot.exe"];
        var sut = new CopilotExecutableLocator(() => @"C:\npm;C:\Tools", true, existingFiles.Contains,
            AllExecutable);

        // Act
        var result = sut.FindCopilotExecutable();

        // Assert
        result
            .Should().Be(@"C:\npm\copilot.cmd", "the PATH order decides before the name order");
    }

    [Fact]
    public void FindCopilotExecutable_WindowsPathContainsOnlyPowerShellShim_ReturnsNull()
    {
        // Arrange
        string[] existingFiles = [@"C:\npm\copilot.ps1", @"C:\npm\copilot"];
        var sut = new CopilotExecutableLocator(() => @"C:\npm", true, existingFiles.Contains, AllExecutable);

        // Act
        var result = sut.FindCopilotExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindCopilotExecutable_UnixPathWithoutCopilot_ReturnsNull()
    {
        // Arrange
        var sut = new CopilotExecutableLocator(() => "/usr/bin:/bin", false, static _ => false, AllExecutable);

        // Act
        var result = sut.FindCopilotExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindCopilotExecutable_DefaultConstructor_ReturnsNullOrFullyQualifiedExistingExecutableFile()
    {
        // Arrange
        var sut = new CopilotExecutableLocator();

        // Act
        var result = sut.FindCopilotExecutable();

        // Assert
        result
            .Should().Match(x => x == null || (Path.IsPathFullyQualified(x) && File.Exists(x)
                                               && (OperatingSystem.IsWindows()
                                                   || PathExecutableSearch.HasUnixExecuteBit(x))));
    }
}
