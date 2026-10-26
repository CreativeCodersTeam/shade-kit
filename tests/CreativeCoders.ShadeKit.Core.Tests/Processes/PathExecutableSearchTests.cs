using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Processes;

// The one home of the tests of the PATH rules; the locators of git and gh only pass their executable names. The tests
// use the git names.
public class PathExecutableSearchTests
{
    private const string WindowsName = "git.exe";

    private const string UnixName = "git";

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

    [Theory]
    [InlineData("/usr/local/bin:/usr/bin", "/usr/bin/git")]
    [InlineData("/usr/bin/", "/usr/bin/git")]
    [InlineData("/opt/a;b:/usr/bin", "/usr/bin/git")]
    [InlineData("/", "/git")]
    public void Find_UnixPathContainsGit_ReturnsAbsoluteGitPath(string pathVariable, string expected)
    {
        // Arrange
        var sut = CreateUnixSearch(pathVariable, expected);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\Windows;C:\Program Files\Git\cmd", @"C:\Program Files\Git\cmd\git.exe")]
    [InlineData(@"C:\Program Files\Git\cmd\", @"C:\Program Files\Git\cmd\git.exe")]
    [InlineData(@"C:/Program Files/Git/cmd/", @"C:/Program Files/Git/cmd/git.exe")]
    [InlineData(@"\\server\share\git", @"\\server\share\git\git.exe")]
    [InlineData(@"\\?\C:\Git", @"\\?\C:\Git\git.exe")]
    [InlineData(@"C:\", @"C:\git.exe")]
    [InlineData(@"c:\git\cmd", @"c:\git\cmd\git.exe")]
    public void Find_WindowsPathContainsGit_ReturnsAbsoluteGitExePath(string pathVariable,
        string expected)
    {
        // Arrange
        var sut = CreateWindowsSearch(pathVariable, expected);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(expected);
    }

    [Fact]
    public void Find_WindowsPathWithDrive_DoesNotSplitAtColon()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => @"C:\Git\cmd", true, x =>
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
            .Should().Equal(@"C:\Git\cmd\git.exe");
    }

    [Fact]
    public void Find_UnixRelativeAndEmptyEntries_SkipsThem()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => ".:bin::./tools:~/bin:/usr/bin", false, x =>
        {
            checkedPaths.Add(x);

            return true;
        }, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be("/usr/bin/git");

        checkedPaths
            .Should().Equal("/usr/bin/git");
    }

    [Fact]
    public void Find_WindowsRelativeAndEmptyEntries_SkipsThem()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => @".;bin;;C:Git;C:;\Git;C;1:\Git;.\tools;C:\Git\cmd", true, x =>
        {
            checkedPaths.Add(x);

            return true;
        }, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"C:\Git\cmd\git.exe");

        checkedPaths
            .Should().Equal(@"C:\Git\cmd\git.exe");
    }

    [Fact]
    public void Find_UnixSeveralEntriesContainGit_ReturnsFirstMatch()
    {
        // Arrange
        var sut = CreateUnixSearch("/opt/none:/usr/local/bin:/usr/bin", "/usr/local/bin/git", "/usr/bin/git");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be("/usr/local/bin/git");
    }

    [Fact]
    public void Find_MatchFound_DoesNotCheckLaterEntries()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => "/opt/none:/usr/local/bin:/usr/bin", false, x =>
        {
            checkedPaths.Add(x);

            return x == "/usr/local/bin/git";
        }, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be("/usr/local/bin/git");

        checkedPaths
            .Should().Equal("/opt/none/git", "/usr/local/bin/git");
    }

    [Fact]
    public void Find_WindowsSeveralEntriesContainGit_ReturnsFirstMatch()
    {
        // Arrange
        var sut = CreateWindowsSearch(@"C:\None;D:\Git\cmd;C:\Git\cmd", @"D:\Git\cmd\git.exe",
            @"C:\Git\cmd\git.exe");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"D:\Git\cmd\git.exe");
    }

    [Theory]
    [InlineData(@"""C:\Program Files\Git\cmd""", @"C:\Program Files\Git\cmd\git.exe")]
    [InlineData(@"C:\None;""C:\Program Files\Git\cmd"";D:\Git", @"C:\Program Files\Git\cmd\git.exe")]
    [InlineData(@"""C:\Tools;Git\cmd"";D:\Git", @"C:\Tools;Git\cmd\git.exe")]
    [InlineData(@"C:\Program"" ""Files\Git\cmd", @"C:\Program Files\Git\cmd\git.exe")]
    [InlineData(@"D:\None;C:\Git""", @"C:\Git\git.exe")]
    [InlineData(@"""C:\Git;D:\Git", @"C:\Git;D:\Git\git.exe")]
    public void Find_WindowsQuotedEntry_UsesEntryWithoutQuotes(string pathVariable, string expected)
    {
        // Arrange
        var sut = CreateWindowsSearch(pathVariable, expected);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(@"""""")]
    [InlineData(@""".""")]
    [InlineData(@"""bin""")]
    public void Find_WindowsQuotedRelativeOrEmptyEntry_ReturnsNull(string pathVariable)
    {
        // Arrange
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => pathVariable, true, _ => true, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void Find_UnixQuotedEntry_TreatsQuotesAsPartOfRelativeEntry()
    {
        // Arrange
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => "\"/usr/bin\"", false, _ => true, AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void Find_WindowsOnlyGitWithoutExtension_ReturnsNull()
    {
        // Arrange
        var sut = CreateWindowsSearch(@"C:\Git\cmd", @"C:\Git\cmd\git", @"C:\Git\cmd\git.cmd");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void Find_UnixOnlyGitExe_ReturnsNull()
    {
        // Arrange
        var sut = CreateUnixSearch("/usr/bin", "/usr/bin/git.exe");

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(":")]
    [InlineData("/usr/local/bin:/usr/bin")]
    public void Find_UnixGitNotFound_ReturnsNull(string? pathVariable)
    {
        // Arrange
        var sut = CreateUnixSearch(pathVariable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(";")]
    [InlineData(@"C:\Windows;C:\Windows\System32")]
    public void Find_WindowsGitNotFound_ReturnsNull(string? pathVariable)
    {
        // Arrange
        var sut = CreateWindowsSearch(pathVariable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void Find_GitIsDirectoryOnFileSystem_SkipsItAndReturnsNextFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var executableName = OperatingSystem.IsWindows() ? "git.exe" : "git";

        var directoryEntry = tempDirectory.CreateDirectory("with-directory");
        tempDirectory.CreateDirectory("with-directory", executableName);

        var fileEntry = tempDirectory.CreateDirectory("with-file");
        tempDirectory.CreateFile("with-file", executableName);

        var pathVariable = string.Join(Path.PathSeparator, directoryEntry, fileEntry);
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => pathVariable, OperatingSystem.IsWindows(), File.Exists,
            AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(Path.Combine(fileEntry, executableName));
    }

    [Fact]
    public void Find_OnlyDirectoryNamedGitOnFileSystem_ReturnsNull()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var executableName = OperatingSystem.IsWindows() ? "git.exe" : "git";

        var directoryEntry = tempDirectory.CreateDirectory("with-directory");
        tempDirectory.CreateDirectory("with-directory", executableName);

        var sut = new PathExecutableSearch(WindowsName, UnixName, () => directoryEntry, OperatingSystem.IsWindows(), File.Exists,
            AllExecutable);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HasUnixExecuteBit_SymbolicLink_JudgesModeOfTarget(bool targetExecutable)
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed for execute bits.");
        using var tempDirectory = new TempKitDirectory();
        var targetPath = tempDirectory.WriteFile(string.Empty, "real-git");
        SetUnixFileMode(targetPath, targetExecutable
            ? UnixFileMode.UserRead | UnixFileMode.UserExecute
            : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var linkPath = Path.Combine(tempDirectory.RootPath, "git");
        File.CreateSymbolicLink(linkPath, targetPath);

        // Act
        var result = PathExecutableSearch.HasUnixExecuteBit(linkPath);

        // Assert
        result
            .Should().Be(targetExecutable);
    }

    [Fact]
    public void Find_UnixDanglingLinkFirst_SkipsItAndReturnsExecutableGit()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed for execute bits.");
        using var tempDirectory = new TempKitDirectory();

        var danglingEntry = tempDirectory.CreateDirectory("dangling");
        File.CreateSymbolicLink(Path.Combine(danglingEntry, "git"), Path.Combine(tempDirectory.RootPath, "missing"));

        var executableEntry = tempDirectory.CreateDirectory("executable");
        SetUnixFileMode(tempDirectory.WriteFile(string.Empty, "executable", "git"),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var sut = new PathExecutableSearch(WindowsName, UnixName, () => $"{danglingEntry}:{executableEntry}", false, File.Exists,
            PathExecutableSearch.HasUnixExecuteBit);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(Path.Combine(executableEntry, "git"));
    }

    [Fact]
    public void HasUnixExecuteBit_OnWindows_ReturnsTrue()
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows has no execute bits.");

        // Act
        var result = PathExecutableSearch.HasUnixExecuteBit(@"C:\missing\git.exe");

        // Assert
        result
            .Should().BeTrue();
    }

    [Fact]
    public void Find_UnixFirstGitNotExecutable_SkipsItAndReturnsExecutableSecondGit()
    {
        // Arrange
        var checkedExecutables = new List<string>();
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => "/home/jane/bin:/usr/bin", false,
            x => x is "/home/jane/bin/git" or "/usr/bin/git", x =>
            {
                checkedExecutables.Add(x);

                return x == "/usr/bin/git";
            });

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be("/usr/bin/git");

        checkedExecutables
            .Should().Equal("/home/jane/bin/git", "/usr/bin/git");
    }

    [Fact]
    public void Find_UnixNoGitExecutable_ReturnsNull()
    {
        // Arrange
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => "/home/jane/bin:/usr/bin", false, _ => true, _ => false);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void Find_UnixMissingFile_DoesNotCheckExecuteBit()
    {
        // Arrange
        var checkedExecutables = new List<string>();
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => "/opt/none:/usr/bin", false, x => x == "/usr/bin/git", x =>
        {
            checkedExecutables.Add(x);

            return true;
        });

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be("/usr/bin/git");

        checkedExecutables
            .Should().Equal("/usr/bin/git");
    }

    [Fact]
    public void Find_WindowsRules_DoesNotCheckExecuteBit()
    {
        // Arrange
        var sut = new PathExecutableSearch(WindowsName, UnixName, () => @"C:\Git\cmd", true, _ => true,
            _ => throw new InvalidOperationException("The execute check must not be called with the Windows rules."));

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(@"C:\Git\cmd\git.exe");
    }

    [Theory]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite, false)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserExecute, true)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.GroupExecute, true)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.OtherExecute, true)]
    public void HasUnixExecuteBit_FileWithMode_ReturnsWhetherAnyExecuteBitIsSet(UnixFileMode mode, bool expected)
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed for execute bits.");
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile(string.Empty, "git");
        SetUnixFileMode(filePath, mode);

        // Act
        var result = PathExecutableSearch.HasUnixExecuteBit(filePath);

        // Assert
        result
            .Should().Be(expected);
    }

    [Fact]
    public void HasUnixExecuteBit_MissingFile_ReturnsFalse()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no execute bits, so every file counts.");
        using var tempDirectory = new TempKitDirectory();

        // Act
        var result = PathExecutableSearch.HasUnixExecuteBit(Path.Combine(tempDirectory.RootPath, "git"));

        // Assert
        result
            .Should().BeFalse();
    }

    [Fact]
    public void Find_UnixNonExecutableGitOnFileSystem_SkipsItAndReturnsExecutableGit()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed for execute bits.");
        using var tempDirectory = new TempKitDirectory();

        var plainEntry = tempDirectory.CreateDirectory("plain");
        SetUnixFileMode(tempDirectory.WriteFile(string.Empty, "plain", "git"), UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var executableEntry = tempDirectory.CreateDirectory("executable");
        SetUnixFileMode(tempDirectory.WriteFile(string.Empty, "executable", "git"),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var sut = new PathExecutableSearch(WindowsName, UnixName, () => $"{plainEntry}:{executableEntry}", false, File.Exists,
            PathExecutableSearch.HasUnixExecuteBit);

        // Act
        var result = sut.Find();

        // Assert
        result
            .Should().Be(Path.Combine(executableEntry, "git"));
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
            x => x is "/usr/bin/git" or "/usr/local/bin/git", AllExecutable);

        var firstResult = sut.Find();
        pathVariable = "/usr/local/bin";

        // Act
        var secondResult = sut.Find();

        // Assert
        firstResult
            .Should().Be("/usr/bin/git");

        secondResult
            .Should().Be("/usr/local/bin/git");
    }

    private static void SetUnixFileMode(string filePath, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(filePath, mode);
        }
    }

    private static PathExecutableSearch CreateUnixSearch(string? pathVariable, params string[] existingFiles)
    {
        return new PathExecutableSearch(WindowsName, UnixName, () => pathVariable, false, existingFiles.Contains,
            AllExecutable);
    }

    private static PathExecutableSearch CreateWindowsSearch(string? pathVariable, params string[] existingFiles)
    {
        return new PathExecutableSearch(WindowsName, UnixName, () => pathVariable, true, existingFiles.Contains,
            AllExecutable);
    }
}
