using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Processes;

// The tests of the ordered Windows executable names (an .exe, then a .cmd shim). The PATH rules themselves are tested
// in PathExecutableSearchTests.
public class PathExecutableSearchWindowsNamesTests
{
    private const string UnixName = "copilot";

    private static readonly string[] WindowsNames = ["copilot.exe", "copilot.cmd"];

    private static readonly Func<string, bool> AllExecutable = static _ => true;

    [Fact]
    public void Constructor_NullWindowsExecutableNames_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PathExecutableSearch((IReadOnlyList<string>)null!, UnixName, () => "/usr/bin", false,
            _ => true, AllExecutable);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("windowsExecutableNames");
    }

    public static TheoryData<string?[]> InvalidWindowsNames { get; } = new(
        [],
        ["copilot.exe", ""],
        ["   ", "copilot.cmd"],
        ["copilot.exe", null]);

    private static readonly string[] ReversedWindowsNames = ["copilot.cmd", "copilot.exe"];

    private static readonly string[] NpmExeAndCmd = [@"C:\npm\copilot.exe", @"C:\npm\copilot.cmd"];

    [Theory]
    [MemberData(nameof(InvalidWindowsNames))]
    public void Constructor_InvalidWindowsExecutableNames_ThrowsArgumentException(string?[] names)
    {
        // Act
        var act = () => new PathExecutableSearch(names!, UnixName, () => "/usr/bin", false, _ => true,
            AllExecutable);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("windowsExecutableNames");
    }

    [Fact]
    public void Constructor_NamesListAndInvalidUnixName_ThrowsArgumentException()
    {
        // Act
        var act = () => new PathExecutableSearch(WindowsNames, " ", () => "/usr/bin", false, _ => true,
            AllExecutable);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("unixExecutableName");
    }

    [Fact]
    public void Find_WindowsDirectoryContainsExeAndCmd_ReturnsExe()
    {
        // Arrange
        var sut = CreateWindowsSearch(@"C:\npm", @"C:\npm\copilot.exe", @"C:\npm\copilot.cmd");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"C:\npm\copilot.exe");
    }

    [Fact]
    public void Find_WindowsDirectoryContainsOnlyCmd_ReturnsCmd()
    {
        // Arrange
        var sut = CreateWindowsSearch(@"C:\Windows;C:\Users\jane\AppData\Roaming\npm",
            @"C:\Users\jane\AppData\Roaming\npm\copilot.cmd");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"C:\Users\jane\AppData\Roaming\npm\copilot.cmd");
    }

    [Fact]
    public void Find_CmdInEarlierDirectoryAndExeInLaterDirectory_ReturnsCmdOfEarlierDirectory()
    {
        // Arrange
        var sut = CreateWindowsSearch(@"C:\npm;C:\tools", @"C:\npm\copilot.cmd", @"C:\tools\copilot.exe");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"C:\npm\copilot.cmd");
    }

    [Fact]
    public void Find_SeveralWindowsDirectories_ChecksEveryNameOfADirectoryBeforeTheNextDirectory()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new PathExecutableSearch(WindowsNames, UnixName, () => @"C:\a;relative;C:\b", true, x =>
        {
            checkedPaths.Add(x);

            return false;
        }, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();

        checkedPaths
            .Should().Equal(@"C:\a\copilot.exe", @"C:\a\copilot.cmd", @"C:\b\copilot.exe", @"C:\b\copilot.cmd");
    }

    [Fact]
    public void Find_NamesInOtherOrder_PrefersTheEarlierName()
    {
        // Arrange
        var sut = new PathExecutableSearch(ReversedWindowsNames, UnixName, () => @"C:\npm", true,
            NpmExeAndCmd.Contains, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"C:\npm\copilot.cmd");
    }

    [Fact]
    public void Find_NamesListChangedAfterConstruction_UsesTheNamesOfTheConstruction()
    {
        // Arrange
        var names = new List<string> { "copilot.exe", "copilot.cmd" };
        var sut = new PathExecutableSearch(names, UnixName, () => @"C:\npm", true, NpmExeAndCmd.Contains,
            AllExecutable);

        names.Reverse();

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"C:\npm\copilot.exe");
    }

    [Fact]
    public void Find_UnixRulesWithWindowsNames_LooksOnlyForTheUnixName()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new PathExecutableSearch(WindowsNames, UnixName, () => "/opt/npm:/usr/local/bin", false, x =>
        {
            checkedPaths.Add(x);

            return x == "/usr/local/bin/copilot";
        }, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be("/usr/local/bin/copilot");

        checkedPaths
            .Should().Equal("/opt/npm/copilot", "/usr/local/bin/copilot");
    }

    [Fact]
    public void Find_SingleWindowsName_DoesNotFindCmdShim()
    {
        // Arrange
        var sut = new PathExecutableSearch("git.exe", "git", () => @"C:\Git\cmd", true,
            x => x == @"C:\Git\cmd\git.cmd", AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    private static PathExecutableSearch CreateWindowsSearch(string pathVariable, params string[] existingFiles)
    {
        return new PathExecutableSearch(WindowsNames, UnixName, () => pathVariable, true, existingFiles.Contains,
            AllExecutable);
    }
}
