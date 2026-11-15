using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations.Claude;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations.Claude;

// The PATH rules are covered by PathExecutableSearchTests and the caching of the shared base class by
// GitExecutableLocatorTests; these tests cover the claude names.
public class ClaudeExecutableLocatorTests
{
    private static readonly Func<string, bool> AllExecutable = static _ => true;

    [Theory]
    [InlineData("/opt/none:/usr/local/bin:/usr/bin", "/usr/local/bin/claude")]
    [InlineData("/opt/homebrew/bin/", "/opt/homebrew/bin/claude")]
    public void FindClaudeExecutable_UnixPathContainsClaude_ReturnsAbsoluteClaudePath(string pathVariable,
        string expected)
    {
        // Arrange
        var sut = new ClaudeExecutableLocator(() => pathVariable, false, x => x == expected, AllExecutable);

        // Act
        var result = sut.FindClaudeExecutable();

        // Assert
        result
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Windows;C:\Users\me\.local\bin", @"C:\Users\me\.local\bin\claude.exe")]
    [InlineData(@"""C:\Users\me\.local\bin\""", @"C:\Users\me\.local\bin\claude.exe")]
    public void FindClaudeExecutable_WindowsPathContainsClaudeExe_ReturnsAbsoluteClaudeExePath(string pathVariable,
        string expected)
    {
        // Arrange
        var sut = new ClaudeExecutableLocator(() => pathVariable, true, x => x == expected, AllExecutable);

        // Act
        var result = sut.FindClaudeExecutable();

        // Assert
        result
            .Should().Be(expected);
    }

    [Fact]
    public void FindClaudeExecutable_WindowsPathContainsOnlyNpmShims_ReturnsNull()
    {
        // Arrange
        string[] existingFiles = [@"C:\npm\claude.cmd", @"C:\npm\claude.ps1", @"C:\npm\claude"];
        var sut = new ClaudeExecutableLocator(() => @"C:\npm", true, existingFiles.Contains, AllExecutable);

        // Act
        var result = sut.FindClaudeExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindClaudeExecutable_UnixPathContainsOnlyClaudeExe_ReturnsNull()
    {
        // Arrange
        var sut = new ClaudeExecutableLocator(() => "/opt/bin", false, static x => x == "/opt/bin/claude.exe",
            AllExecutable);

        // Act
        var result = sut.FindClaudeExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindClaudeExecutable_UnixPathWithoutClaude_ReturnsNull()
    {
        // Arrange
        var sut = new ClaudeExecutableLocator(() => "/usr/bin:/bin", false, static _ => false, AllExecutable);

        // Act
        var result = sut.FindClaudeExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindClaudeExecutable_DefaultConstructor_ReturnsNullOrFullyQualifiedExistingExecutableFile()
    {
        // Arrange
        var sut = new ClaudeExecutableLocator();

        // Act
        var result = sut.FindClaudeExecutable();

        // Assert
        result
            .Should().Match(x => x == null || (Path.IsPathFullyQualified(x) && File.Exists(x)
                                               && PathExecutableSearch.HasUnixExecuteBit(x)));
    }
}
