using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Codex;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Codex;

// The PATH rules are covered by PathExecutableSearchTests and the caching of the shared base class by
// GitExecutableLocatorTests; these tests cover the codex names.
public class CodexExecutableLocatorTests
{
    private static readonly Func<string, bool> AllExecutable = static _ => true;

    [Theory]
    [InlineData("/opt/none:/usr/local/bin:/usr/bin", "/usr/local/bin/codex")]
    [InlineData("/home/me/.local/bin/", "/home/me/.local/bin/codex")]
    public void FindCodexExecutable_UnixPathContainsCodex_ReturnsAbsoluteCodexPath(string pathVariable,
        string expected)
    {
        // Arrange
        var sut = new CodexExecutableLocator(() => pathVariable, false, x => x == expected, AllExecutable);

        // Act
        var result = sut.FindCodexExecutable();

        // Assert
        result
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("/opt/bin/codex.cmd")]
    [InlineData("/opt/bin/codex.exe")]
    public void FindCodexExecutable_UnixPathContainsOnlyWindowsNames_ReturnsNull(string existingFile)
    {
        // Arrange
        var sut = new CodexExecutableLocator(() => "/opt/bin", false, x => x == existingFile, AllExecutable);

        // Act
        var result = sut.FindCodexExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindCodexExecutable_WindowsDirectoryContainsExeAndCmd_ReturnsExe()
    {
        // Arrange
        string[] existingFiles = [@"C:\npm\codex.cmd", @"C:\npm\codex.exe"];
        var sut = new CodexExecutableLocator(() => @"C:\npm", true, existingFiles.Contains, AllExecutable);

        // Act
        var result = sut.FindCodexExecutable();

        // Assert
        result
            .Should().Be(@"C:\npm\codex.exe");
    }

    [Fact]
    public void FindCodexExecutable_WindowsDirectoryContainsOnlyNpmShims_ReturnsCmd()
    {
        // Arrange
        string[] existingFiles = [@"C:\npm\codex.cmd", @"C:\npm\codex.ps1", @"C:\npm\codex"];
        var sut = new CodexExecutableLocator(() => @"C:\npm", true, existingFiles.Contains, AllExecutable);

        // Act
        var result = sut.FindCodexExecutable();

        // Assert
        result
            .Should().Be(@"C:\npm\codex.cmd");
    }

    [Fact]
    public void FindCodexExecutable_WindowsCmdInEarlierDirectoryThanExe_ReturnsCmdOfEarlierDirectory()
    {
        // Arrange
        string[] existingFiles = [@"C:\npm\codex.cmd", @"C:\Tools\codex.exe"];
        var sut = new CodexExecutableLocator(() => @"C:\npm;C:\Tools", true, existingFiles.Contains,
            AllExecutable);

        // Act
        var result = sut.FindCodexExecutable();

        // Assert
        result
            .Should().Be(@"C:\npm\codex.cmd", "the PATH order decides before the name order");
    }

    [Fact]
    public void FindCodexExecutable_WindowsPathContainsOnlyPowerShellShim_ReturnsNull()
    {
        // Arrange
        string[] existingFiles = [@"C:\npm\codex.ps1", @"C:\npm\codex"];
        var sut = new CodexExecutableLocator(() => @"C:\npm", true, existingFiles.Contains, AllExecutable);

        // Act
        var result = sut.FindCodexExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindCodexExecutable_UnixPathWithoutCodex_ReturnsNull()
    {
        // Arrange
        var sut = new CodexExecutableLocator(() => "/usr/bin:/bin", false, static _ => false, AllExecutable);

        // Act
        var result = sut.FindCodexExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindCodexExecutable_DefaultConstructor_ReturnsNullOrFullyQualifiedExistingExecutableFile()
    {
        // Arrange
        var sut = new CodexExecutableLocator();

        // Act
        var result = sut.FindCodexExecutable();

        // Assert
        result
            .Should().Match(x => x == null || (Path.IsPathFullyQualified(x) && File.Exists(x)
                                               && (OperatingSystem.IsWindows()
                                                   || PathExecutableSearch.HasUnixExecuteBit(x))));
    }
}
