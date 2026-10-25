using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Trackers;

namespace CreativeCoders.ShadeKit.Core.Tests.Trackers;

// The PATH rules themselves are covered by the tests of the GitExecutableLocator; these tests cover the gh names,
// the caching, and the seams.
public class GhExecutableLocatorTests
{
    private static readonly Func<string, bool> AllExecutable = static _ => true;

    [Fact]
    public void Constructor_NullGetPathVariable_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GhExecutableLocator(null!, false, _ => true, AllExecutable);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("getPathVariable");
    }

    [Fact]
    public void Constructor_NullFileExists_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GhExecutableLocator(() => "/usr/bin", false, null!, AllExecutable);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fileExists");
    }

    [Fact]
    public void Constructor_NullIsExecutable_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GhExecutableLocator(() => "/usr/bin", false, _ => true, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("isExecutable");
    }

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
    public void FindGhExecutable_UnixRules_LooksForGhWithoutExtension()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new GhExecutableLocator(() => "/usr/bin", false, x =>
        {
            checkedPaths.Add(x);

            return false;
        }, AllExecutable);

        // Act
        var result = sut.FindGhExecutable();

        // Assert
        result
            .Should().BeNull();

        checkedPaths
            .Should().Equal("/usr/bin/gh");
    }

    [Fact]
    public void FindGhExecutable_WindowsRules_LooksForGhExe()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new GhExecutableLocator(() => @"C:\Tools", true, x =>
        {
            checkedPaths.Add(x);

            return false;
        }, AllExecutable);

        // Act
        var result = sut.FindGhExecutable();

        // Assert
        result
            .Should().BeNull();

        checkedPaths
            .Should().Equal(@"C:\Tools\gh.exe");
    }

    [Fact]
    public void FindGhExecutable_RelativeEntries_SkipsThem()
    {
        // Arrange
        var sut = new GhExecutableLocator(() => ".:bin:/usr/bin", false, _ => true, AllExecutable);

        // Act
        var result = sut.FindGhExecutable();

        // Assert
        result
            .Should().Be("/usr/bin/gh");
    }

    [Fact]
    public void FindGhExecutable_UnixGhNotExecutable_ReturnsNull()
    {
        // Arrange
        var sut = new GhExecutableLocator(() => "/usr/bin", false, _ => true, _ => false);

        // Act
        var result = sut.FindGhExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindGhExecutable_CalledRepeatedlyAfterMatch_SearchesPathOnlyOnce()
    {
        // Arrange
        var pathVariableReads = 0;
        var pathVariable = "/usr/bin";
        var sut = new GhExecutableLocator(() =>
        {
            pathVariableReads++;

            return pathVariable;
        }, false, x => x is "/usr/bin/gh" or "/usr/local/bin/gh", AllExecutable);

        var firstResult = sut.FindGhExecutable();
        pathVariable = "/usr/local/bin";

        // Act
        var results = Enumerable.Range(0, 2).Select(_ => sut.FindGhExecutable()).ToList();

        // Assert
        firstResult
            .Should().Be("/usr/bin/gh");

        results
            .Should().Equal("/usr/bin/gh", "/usr/bin/gh");

        pathVariableReads
            .Should().Be(1);
    }

    [Fact]
    public void FindGhExecutable_NotFoundOnFirstCall_SearchesCurrentPathValueOnNextCall()
    {
        // Arrange
        var pathVariable = "/opt/none";
        var sut = new GhExecutableLocator(() => pathVariable, false, x => x == "/usr/bin/gh", AllExecutable);

        var firstResult = sut.FindGhExecutable();
        pathVariable = "/usr/bin";

        // Act
        var secondResult = sut.FindGhExecutable();

        // Assert
        firstResult
            .Should().BeNull();

        secondResult
            .Should().Be("/usr/bin/gh");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FindGhExecutable_PathNotSet_ReturnsNull(string? pathVariable)
    {
        // Arrange
        var sut = new GhExecutableLocator(() => pathVariable, false, _ => true, AllExecutable);

        // Act
        var result = sut.FindGhExecutable();

        // Assert
        result
            .Should().BeNull();
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
