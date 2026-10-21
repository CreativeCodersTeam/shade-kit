using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Git;

public class GitExecutableLocatorTests
{
    // Every existing file counts as executable, so the tests of the PATH rules do not depend on file modes.
    private static readonly Func<string, bool> AllExecutable = static _ => true;

    [Fact]
    public void Constructor_NullGetPathVariable_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitExecutableLocator(null!, false, _ => true, AllExecutable);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("getPathVariable");
    }

    [Fact]
    public void Constructor_NullFileExists_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitExecutableLocator(() => "/usr/bin", false, null!, AllExecutable);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fileExists");
    }

    [Fact]
    public void Constructor_NullIsExecutable_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitExecutableLocator(() => "/usr/bin", false, _ => true, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("isExecutable");
    }

    [Theory]
    [InlineData("/usr/local/bin:/usr/bin", "/usr/bin/git")]
    [InlineData("/usr/bin/", "/usr/bin/git")]
    [InlineData("/opt/a;b:/usr/bin", "/usr/bin/git")]
    [InlineData("/", "/git")]
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
    [InlineData(@"C:\Program Files\Git\cmd\", @"C:\Program Files\Git\cmd\git.exe")]
    [InlineData(@"C:/Program Files/Git/cmd/", @"C:/Program Files/Git/cmd/git.exe")]
    [InlineData(@"\\server\share\git", @"\\server\share\git\git.exe")]
    [InlineData(@"\\?\C:\Git", @"\\?\C:\Git\git.exe")]
    [InlineData(@"C:\", @"C:\git.exe")]
    [InlineData(@"c:\git\cmd", @"c:\git\cmd\git.exe")]
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
    public void FindGitExecutable_WindowsPathWithDrive_DoesNotSplitAtColon()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new GitExecutableLocator(() => @"C:\Git\cmd", true, x =>
        {
            checkedPaths.Add(x);

            return false;
        }, AllExecutable);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().BeNull();

        checkedPaths
            .Should().Equal(@"C:\Git\cmd\git.exe");
    }

    [Fact]
    public void FindGitExecutable_UnixRelativeAndEmptyEntries_SkipsThem()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new GitExecutableLocator(() => ".:bin::./tools:~/bin:/usr/bin", false, x =>
        {
            checkedPaths.Add(x);

            return true;
        }, AllExecutable);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be("/usr/bin/git");

        checkedPaths
            .Should().Equal("/usr/bin/git");
    }

    [Fact]
    public void FindGitExecutable_WindowsRelativeAndEmptyEntries_SkipsThem()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new GitExecutableLocator(() => @".;bin;;C:Git;C:;\Git;C;1:\Git;.\tools;C:\Git\cmd", true, x =>
        {
            checkedPaths.Add(x);

            return true;
        }, AllExecutable);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be(@"C:\Git\cmd\git.exe");

        checkedPaths
            .Should().Equal(@"C:\Git\cmd\git.exe");
    }

    [Fact]
    public void FindGitExecutable_UnixSeveralEntriesContainGit_ReturnsFirstMatch()
    {
        // Arrange
        var sut = CreateUnixLocator("/opt/none:/usr/local/bin:/usr/bin", "/usr/local/bin/git", "/usr/bin/git");

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be("/usr/local/bin/git");
    }

    [Fact]
    public void FindGitExecutable_MatchFound_DoesNotCheckLaterEntries()
    {
        // Arrange
        var checkedPaths = new List<string>();
        var sut = new GitExecutableLocator(() => "/opt/none:/usr/local/bin:/usr/bin", false, x =>
        {
            checkedPaths.Add(x);

            return x == "/usr/local/bin/git";
        }, AllExecutable);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be("/usr/local/bin/git");

        checkedPaths
            .Should().Equal("/opt/none/git", "/usr/local/bin/git");
    }

    [Fact]
    public void FindGitExecutable_WindowsSeveralEntriesContainGit_ReturnsFirstMatch()
    {
        // Arrange
        var sut = CreateWindowsLocator(@"C:\None;D:\Git\cmd;C:\Git\cmd", @"D:\Git\cmd\git.exe",
            @"C:\Git\cmd\git.exe");

        // Act
        var result = sut.FindGitExecutable();

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
    public void FindGitExecutable_WindowsQuotedEntry_UsesEntryWithoutQuotes(string pathVariable, string expected)
    {
        // Arrange
        var sut = CreateWindowsLocator(pathVariable, expected);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(@"""""")]
    [InlineData(@""".""")]
    [InlineData(@"""bin""")]
    public void FindGitExecutable_WindowsQuotedRelativeOrEmptyEntry_ReturnsNull(string pathVariable)
    {
        // Arrange
        var sut = new GitExecutableLocator(() => pathVariable, true, _ => true, AllExecutable);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindGitExecutable_UnixQuotedEntry_TreatsQuotesAsPartOfRelativeEntry()
    {
        // Arrange
        var sut = new GitExecutableLocator(() => "\"/usr/bin\"", false, _ => true, AllExecutable);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindGitExecutable_WindowsOnlyGitWithoutExtension_ReturnsNull()
    {
        // Arrange
        var sut = CreateWindowsLocator(@"C:\Git\cmd", @"C:\Git\cmd\git", @"C:\Git\cmd\git.cmd");

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindGitExecutable_UnixOnlyGitExe_ReturnsNull()
    {
        // Arrange
        var sut = CreateUnixLocator("/usr/bin", "/usr/bin/git.exe");

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(":")]
    [InlineData("/usr/local/bin:/usr/bin")]
    public void FindGitExecutable_UnixGitNotFound_ReturnsNull(string? pathVariable)
    {
        // Arrange
        var sut = CreateUnixLocator(pathVariable);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(";")]
    [InlineData(@"C:\Windows;C:\Windows\System32")]
    public void FindGitExecutable_WindowsGitNotFound_ReturnsNull(string? pathVariable)
    {
        // Arrange
        var sut = CreateWindowsLocator(pathVariable);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindGitExecutable_PathVariableReadOnEveryCall_UsesCurrentValue()
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
    public void FindGitExecutable_GitIsDirectoryOnFileSystem_SkipsItAndReturnsNextFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var executableName = OperatingSystem.IsWindows() ? "git.exe" : "git";

        var directoryEntry = tempDirectory.CreateDirectory("with-directory");
        tempDirectory.CreateDirectory("with-directory", executableName);

        var fileEntry = tempDirectory.CreateDirectory("with-file");
        tempDirectory.CreateFile("with-file", executableName);

        var pathVariable = string.Join(Path.PathSeparator, directoryEntry, fileEntry);
        var sut = new GitExecutableLocator(() => pathVariable, OperatingSystem.IsWindows(), File.Exists,
            AllExecutable);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be(Path.Combine(fileEntry, executableName));
    }

    [Fact]
    public void FindGitExecutable_OnlyDirectoryNamedGitOnFileSystem_ReturnsNull()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var executableName = OperatingSystem.IsWindows() ? "git.exe" : "git";

        var directoryEntry = tempDirectory.CreateDirectory("with-directory");
        tempDirectory.CreateDirectory("with-directory", executableName);

        var sut = new GitExecutableLocator(() => directoryEntry, OperatingSystem.IsWindows(), File.Exists,
            AllExecutable);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().BeNull();
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
                                               && GitExecutableLocator.HasUnixExecuteBit(x)));
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
        var result = GitExecutableLocator.HasUnixExecuteBit(linkPath);

        // Assert
        result
            .Should().Be(targetExecutable);
    }

    [Fact]
    public void FindGitExecutable_UnixDanglingLinkFirst_SkipsItAndReturnsExecutableGit()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed for execute bits.");
        using var tempDirectory = new TempKitDirectory();

        var danglingEntry = tempDirectory.CreateDirectory("dangling");
        File.CreateSymbolicLink(Path.Combine(danglingEntry, "git"), Path.Combine(tempDirectory.RootPath, "missing"));

        var executableEntry = tempDirectory.CreateDirectory("executable");
        SetUnixFileMode(tempDirectory.WriteFile(string.Empty, "executable", "git"),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var sut = new GitExecutableLocator(() => $"{danglingEntry}:{executableEntry}", false, File.Exists,
            GitExecutableLocator.HasUnixExecuteBit);

        // Act
        var result = sut.FindGitExecutable();

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
        var result = GitExecutableLocator.HasUnixExecuteBit(@"C:\missing\git.exe");

        // Assert
        result
            .Should().BeTrue();
    }

    [Fact]
    public void FindGitExecutable_UnixFirstGitNotExecutable_SkipsItAndReturnsExecutableSecondGit()
    {
        // Arrange
        var checkedExecutables = new List<string>();
        var sut = new GitExecutableLocator(() => "/home/jane/bin:/usr/bin", false,
            x => x is "/home/jane/bin/git" or "/usr/bin/git", x =>
            {
                checkedExecutables.Add(x);

                return x == "/usr/bin/git";
            });

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be("/usr/bin/git");

        checkedExecutables
            .Should().Equal("/home/jane/bin/git", "/usr/bin/git");
    }

    [Fact]
    public void FindGitExecutable_UnixNoGitExecutable_ReturnsNull()
    {
        // Arrange
        var sut = new GitExecutableLocator(() => "/home/jane/bin:/usr/bin", false, _ => true, _ => false);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindGitExecutable_UnixMissingFile_DoesNotCheckExecuteBit()
    {
        // Arrange
        var checkedExecutables = new List<string>();
        var sut = new GitExecutableLocator(() => "/opt/none:/usr/bin", false, x => x == "/usr/bin/git", x =>
        {
            checkedExecutables.Add(x);

            return true;
        });

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be("/usr/bin/git");

        checkedExecutables
            .Should().Equal("/usr/bin/git");
    }

    [Fact]
    public void FindGitExecutable_WindowsRules_DoesNotCheckExecuteBit()
    {
        // Arrange
        var sut = new GitExecutableLocator(() => @"C:\Git\cmd", true, _ => true,
            _ => throw new InvalidOperationException("The execute check must not be called with the Windows rules."));

        // Act
        var result = sut.FindGitExecutable();

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
        var result = GitExecutableLocator.HasUnixExecuteBit(filePath);

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
        var result = GitExecutableLocator.HasUnixExecuteBit(Path.Combine(tempDirectory.RootPath, "git"));

        // Assert
        result
            .Should().BeFalse();
    }

    [Fact]
    public void FindGitExecutable_UnixNonExecutableGitOnFileSystem_SkipsItAndReturnsExecutableGit()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed for execute bits.");
        using var tempDirectory = new TempKitDirectory();

        var plainEntry = tempDirectory.CreateDirectory("plain");
        SetUnixFileMode(tempDirectory.WriteFile(string.Empty, "plain", "git"), UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var executableEntry = tempDirectory.CreateDirectory("executable");
        SetUnixFileMode(tempDirectory.WriteFile(string.Empty, "executable", "git"),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var sut = new GitExecutableLocator(() => $"{plainEntry}:{executableEntry}", false, File.Exists,
            GitExecutableLocator.HasUnixExecuteBit);

        // Act
        var result = sut.FindGitExecutable();

        // Assert
        result
            .Should().Be(Path.Combine(executableEntry, "git"));
    }

    private static void SetUnixFileMode(string filePath, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(filePath, mode);
        }
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
