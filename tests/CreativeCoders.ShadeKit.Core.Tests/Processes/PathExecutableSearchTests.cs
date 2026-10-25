using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Processes;

// The search is generic: these tests use the gh names to prove that the executable names are parameters and not
// fixed to git. The PATH rules themselves are covered in detail by GitExecutableLocatorTests.
public class PathExecutableSearchTests
{
    private const string WindowsName = "gh.exe";

    private const string UnixName = "gh";

    // Every existing file counts as executable, so the tests of the PATH rules do not depend on file modes.
    private static readonly Func<string, bool> AllExecutable = static _ => true;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidWindowsExecutableName_ThrowsArgumentException(string? executableName)
    {
        // Act
        var act = () => new PathExecutableSearch(executableName!, UnixName, () => "/usr/bin", false,
            _ => true, AllExecutable);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("windowsExecutableName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidUnixExecutableName_ThrowsArgumentException(string? executableName)
    {
        // Act
        var act = () => new PathExecutableSearch(WindowsName, executableName!, () => "/usr/bin", false,
            _ => true, AllExecutable);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("unixExecutableName");
    }

    [Fact]
    public void Constructor_NullGetPathVariable_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PathExecutableSearch(WindowsName, UnixName, null!, false, _ => true, AllExecutable);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("getPathVariable");
    }

    [Fact]
    public void Constructor_NullFileExists_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PathExecutableSearch(WindowsName, UnixName, () => "/usr/bin", false, null!,
            AllExecutable);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fileExists");
    }

    [Fact]
    public void Constructor_NullIsExecutable_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PathExecutableSearch(WindowsName, UnixName, () => "/usr/bin", false, _ => true, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("isExecutable");
    }

    [Fact]
    public void Find_UnixRulesWithOtherExecutableName_ReturnsPathWithUnixName()
    {
        // Arrange
        var sut = CreateSearch("/usr/local/bin:/opt/homebrew/bin", false, "/opt/homebrew/bin/gh");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be("/opt/homebrew/bin/gh");
    }

    [Fact]
    public void Find_WindowsRulesWithOtherExecutableName_ReturnsPathWithWindowsName()
    {
        // Arrange
        var sut = CreateSearch(@"C:\Windows;C:\Program Files\GitHub CLI", true,
            @"C:\Program Files\GitHub CLI\gh.exe");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"C:\Program Files\GitHub CLI\gh.exe");
    }

    [Fact]
    public void Find_UnixRulesOnlyWindowsNameExists_ReturnsNull()
    {
        // Arrange
        var sut = CreateSearch("/usr/bin", false, "/usr/bin/gh.exe", "/usr/bin/git");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void Find_WindowsRulesOnlyUnixNameExists_ReturnsNull()
    {
        // Arrange
        var sut = CreateSearch(@"C:\Tools", true, @"C:\Tools\gh", @"C:\Tools\gh.cmd");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Theory]
    [InlineData(@"""C:\Program Files\GitHub CLI""", @"C:\Program Files\GitHub CLI\gh.exe")]
    [InlineData(@"C:\None;""C:\Tools;CLI"";D:\gh", @"C:\Tools;CLI\gh.exe")]
    [InlineData(@"C:\Program"" ""Files\GitHub CLI", @"C:\Program Files\GitHub CLI\gh.exe")]
    public void Find_WindowsQuotedEntry_UsesEntryWithoutQuotes(string pathVariable, string expected)
    {
        // Arrange
        var sut = CreateSearch(pathVariable, true, expected);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(expected);
    }

    [Fact]
    public void Find_UnixRelativeEntries_SkipsThemWithoutCheckingFiles()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => ".:bin:./tools:~/bin:/usr/bin", false, x =>
        {
            checkedPaths.Add(x);

            return true;
        }, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be("/usr/bin/gh");

        checkedPaths
            .Should().Equal("/usr/bin/gh");
    }

    [Fact]
    public void Find_WindowsRelativeEntries_SkipsThemWithoutCheckingFiles()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => @".;bin;C:Tools;\Tools;.\gh;C:\Tools", true,
            x =>
            {
                checkedPaths.Add(x);

                return true;
            }, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"C:\Tools\gh.exe");

        checkedPaths
            .Should().Equal(@"C:\Tools\gh.exe");
    }

    [Fact]
    public void Find_OnlyRelativeEntries_ReturnsNull()
    {
        // Arrange
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => ".:bin:tools", false, _ => true,
            AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void Find_UnixFirstFileWithoutExecuteBit_SkipsItAndReturnsExecutableFile()
    {
        // Arrange
        var checkedExecutables = new List<string>();
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => "/home/jane/bin:/usr/bin", false,
            x => x is "/home/jane/bin/gh" or "/usr/bin/gh", x =>
            {
                checkedExecutables.Add(x);

                return x == "/usr/bin/gh";
            });

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be("/usr/bin/gh");

        checkedExecutables
            .Should().Equal("/home/jane/bin/gh", "/usr/bin/gh");
    }

    [Fact]
    public void Find_UnixNoFileWithExecuteBit_ReturnsNull()
    {
        // Arrange
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => "/home/jane/bin:/usr/bin", false,
            _ => true, _ => false);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void Find_WindowsRules_DoesNotCheckExecuteBit()
    {
        // Arrange
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => @"C:\Tools", true, _ => true,
            _ => throw new InvalidOperationException("The execute check must not be called with the Windows rules."));

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"C:\Tools\gh.exe");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(":", false)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(";", true)]
    public void Find_EmptyOrMissingPath_ReturnsNullWithoutCheckingFiles(string? pathVariable, bool isWindows)
    {
        // Arrange
        var fileChecks = 0;
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => pathVariable, isWindows, _ =>
        {
            fileChecks++;

            return true;
        }, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();

        fileChecks
            .Should().Be(0);
    }

    [Fact]
    public void Find_CalledTwice_ReadsCurrentPathValueOnEveryCall()
    {
        // Arrange
        var pathVariable = "/usr/bin";
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => pathVariable, false,
            x => x is "/usr/bin/gh" or "/usr/local/bin/gh", AllExecutable);

        var firstResult = sut.Find();
        pathVariable = "/usr/local/bin";

        // Act
        var secondResult = sut.Find();

        // Assert
        firstResult
            .Should().Be("/usr/bin/gh");

        secondResult
            .Should().Be("/usr/local/bin/gh");
    }

    private static PathExecutableSearch CreateSearch(string? pathVariable, bool isWindows,
        params string[] existingFiles)
    {
        return new PathExecutableSearch(WindowsName, UnixName, () => pathVariable, isWindows, existingFiles.Contains,
            AllExecutable);
    }
}
