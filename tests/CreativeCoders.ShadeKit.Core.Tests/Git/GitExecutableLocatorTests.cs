using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Git;

// The PATH rules are covered by PathExecutableSearchTests; these tests cover the git names and the caching of the
// shared PathExecutableLocator base class.
public class GitExecutableLocatorTests
{
    private static readonly Func<string, bool> AllExecutable = static _ => true;

    [Theory]
    [InlineData("/usr/local/bin:/usr/bin", "/usr/bin/git")]
    public void FindGitExecutable_UnixPathContainsGit_ReturnsAbsoluteGitPath(string pathVariable, string expected)
    {
        // Arrange
        var sut = CreateUnixLocator(pathVariable, expected);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Windows;C:\Program Files\Git\cmd", @"C:\Program Files\Git\cmd\git.exe")]
    public void FindGitExecutable_WindowsPathContainsGit_ReturnsAbsoluteGitExePath(string pathVariable,
        string expected)
    {
        // Arrange
        var sut = CreateWindowsLocator(pathVariable, expected);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be(expected);
    }

    [Fact]
    public void FindGitExecutable_NotFoundOnFirstCall_SearchesCurrentPathValueOnNextCall()
    {
        // Arrange
        var pathVariable = "/opt/none";
        var sut = new GitExecutableLocator(() => pathVariable, false, x => x == "/usr/bin/git", AllExecutable);

        var firstResult = sut.FindGitExecutable();
        pathVariable = "/usr/bin";

        // Act
        var secondResult = sut.FindGitExecutable();

        // Assert
        firstResult
            .Should().BeNull();

        secondResult
            .Should().Be("/usr/bin/git");
    }

    [Fact]
    public void FindGitExecutable_CalledRepeatedlyAfterMatch_SearchesPathOnlyOnce()
    {
        // Arrange
        var pathVariableReads = 0;
        var fileChecks = 0;
        var sut = new GitExecutableLocator(() =>
        {
            pathVariableReads++;

            return "/opt/none:/usr/bin";
        }, false, x =>
        {
            fileChecks++;

            return x == "/usr/bin/git";
        }, AllExecutable);

        // Act
        var results = Enumerable.Range(0, 3).Select(_ => sut.FindGitExecutable()).ToList();

        // Assert
        results
            .Should().Equal("/usr/bin/git", "/usr/bin/git", "/usr/bin/git");

        pathVariableReads
            .Should().Be(1);

        fileChecks
            .Should().Be(2);
    }

    [Fact]
    public void FindGitExecutable_PathChangesAfterMatch_ReturnsFirstMatch()
    {
        // Arrange
        var pathVariable = "/usr/bin";
        var sut = new GitExecutableLocator(() => pathVariable, false,
            x => x is "/usr/bin/git" or "/usr/local/bin/git", AllExecutable);

        var firstResult = sut.FindGitExecutable();
        pathVariable = "/usr/local/bin";

        // Act
        var secondResult = sut.FindGitExecutable();

        // Assert
        firstResult
            .Should().Be("/usr/bin/git");

        secondResult
            .Should().Be("/usr/bin/git");
    }

    [Fact]
    public void FindGitExecutable_DefaultConstructor_ReturnsNullOrFullyQualifiedExistingExecutableFile()
    {
        // Arrange
        var sut = new GitExecutableLocator();

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Match(x => x == null || (Path.IsPathFullyQualified(x) && File.Exists(x)
                                               && PathExecutableSearch.HasUnixExecuteBit(x)));
    }

    private static GitExecutableLocator CreateUnixLocator(string? pathVariable, params string[] existingFiles)
    {
        return new GitExecutableLocator(() => pathVariable, false, existingFiles.Contains, AllExecutable);
    }

    private static GitExecutableLocator CreateWindowsLocator(string? pathVariable, params string[] existingFiles)
    {
        return new GitExecutableLocator(() => pathVariable, true, existingFiles.Contains, AllExecutable);
    }
}
