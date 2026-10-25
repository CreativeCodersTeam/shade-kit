using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Trackers;

namespace CreativeCoders.ShadeKit.Core.Tests.Trackers;

// The PATH rules are covered by PathExecutableSearchTests and the caching of the shared base class by
// GitExecutableLocatorTests; these tests cover the gh names.
public class GhExecutableLocatorTests
{
    private static readonly Func<string, bool> AllExecutable = static _ => true;

    [Theory]
    [InlineData("/opt/none:/usr/local/bin:/usr/bin", "/usr/local/bin/gh")]
    [InlineData("/opt/homebrew/bin/", "/opt/homebrew/bin/gh")]
    public void FindGhExecutable_UnixPathContainsGh_ReturnsAbsoluteGhPath(string pathVariable, string expected)
    {
        // Arrange
        var sut = new GhExecutableLocator(() => pathVariable, false, x => x == expected, AllExecutable);

        // Act
        var result = sut.FindGhExecutable();

        // Assert
        result
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Windows;C:\Program Files\GitHub CLI", @"C:\Program Files\GitHub CLI\gh.exe")]
    [InlineData(@"""C:\Program Files\GitHub CLI\""", @"C:\Program Files\GitHub CLI\gh.exe")]
    public void FindGhExecutable_WindowsPathContainsGhExe_ReturnsAbsoluteGhExePath(string pathVariable,
        string expected)
    {
        // Arrange
        var sut = new GhExecutableLocator(() => pathVariable, true, x => x == expected, AllExecutable);

        // Act
        var result = sut.FindGhExecutable();

        // Assert
        result
            .Should().Be(expected);
    }

    [Fact]
    public void FindGhExecutable_DefaultConstructor_ReturnsNullOrFullyQualifiedExistingExecutableFile()
    {
        // Arrange
        var sut = new GhExecutableLocator();

        // Act
        var result = sut.FindGhExecutable();

        // Assert
        result
            .Should().Match(x => x == null || (Path.IsPathFullyQualified(x) && File.Exists(x)
                                               && PathExecutableSearch.HasUnixExecuteBit(x)));
    }
}
