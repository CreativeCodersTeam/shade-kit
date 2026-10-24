using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

public sealed class EditorConfigLineEndingResolverTests : IDisposable
{
    private const string EditorConfigFileName = ".editorconfig";

    private readonly TempKitDirectory _directory = new TempKitDirectory();

    private readonly EditorConfigLineEndingResolver _sut = new EditorConfigLineEndingResolver();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Theory]
    [InlineData("lf", LineEnding.Lf)]
    [InlineData("crlf", LineEnding.CrLf)]
    [InlineData("LF", LineEnding.Lf)]
    [InlineData("CRLF", LineEnding.CrLf)]
    public void GetLineEnding_EndOfLineForAllFiles_ReturnsLineEnding(string endOfLine, LineEnding expected)
    {
        // Arrange
        WriteRootEditorConfig($"[*]\nend_of_line = {endOfLine}\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("readme.md")).LineEnding;

        // Assert
        lineEnding
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("cr")]
    [InlineData("native")]
    [InlineData("unset")]
    [InlineData("")]
    public void GetLineEnding_EndOfLineOtherValue_ReturnsNull(string endOfLine)
    {
        // Arrange
        WriteRootEditorConfig($"[*]\nend_of_line = {endOfLine}\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("readme.md")).LineEnding;

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Fact]
    public void GetLineEnding_NoEndOfLine_ReturnsNull()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nindent_style = space\ncharset = utf-8\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("readme.md")).LineEnding;

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Fact]
    public void GetLineEnding_GlobSectionMatchesExtension_ReturnsLineEnding()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = lf\n\n[*.md]\nend_of_line = crlf\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("readme.md")).LineEnding;

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Fact]
    public void GetLineEnding_GlobSectionDoesNotMatchExtension_ReturnsNull()
    {
        // Arrange
        WriteRootEditorConfig("[*.md]\nend_of_line = crlf\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("config.json")).LineEnding;

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Theory]
    [InlineData("readme.md")]
    [InlineData("config.json")]
    public void GetLineEnding_BraceGlobSection_ReturnsLineEndingForEveryListedExtension(string fileName)
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = lf\n\n[*.{md,json}]\nend_of_line = crlf\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath(fileName)).LineEnding;

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Fact]
    public void GetLineEnding_BraceGlobSection_IgnoresUnlistedExtension()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = lf\n\n[*.{md,json}]\nend_of_line = crlf\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("program.cs")).LineEnding;

        // Assert
        lineEnding
            .Should().Be(LineEnding.Lf);
    }

    [Fact]
    public void GetLineEnding_GlobSectionInParentDirectory_MatchesFileInSubdirectory()
    {
        // Arrange
        WriteRootEditorConfig("[*.md]\nend_of_line = crlf\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("docs", "guides", "setup.md")).LineEnding;

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Fact]
    public void GetLineEnding_NestedEditorConfig_OverridesParentForFilesBelowIt()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = lf\n");
        _directory.WriteFile("[*]\nend_of_line = crlf\n", "docs", EditorConfigFileName);

        // Act
        var nestedLineEnding = _sut.GetLineEnding(GetPath("docs", "setup.md")).LineEnding;
        var rootLineEnding = _sut.GetLineEnding(GetPath("readme.md")).LineEnding;

        // Assert
        nestedLineEnding
            .Should().Be(LineEnding.CrLf);

        rootLineEnding
            .Should().Be(LineEnding.Lf);
    }

    [Fact]
    public void GetLineEnding_NestedEditorConfigWithoutEndOfLine_InheritsParentValue()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = crlf\n");
        _directory.WriteFile("[*]\nindent_size = 2\n", "docs", EditorConfigFileName);

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("docs", "setup.md")).LineEnding;

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Fact]
    public void GetLineEnding_NestedEditorConfigWithRootTrue_IgnoresParentEditorConfig()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = crlf\n");
        _directory.WriteFile("root = true\n\n[*]\nindent_size = 2\n", "docs", EditorConfigFileName);

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("docs", "setup.md")).LineEnding;

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Fact]
    public void GetLineEnding_EditorConfigWithInvalidLines_IgnoresInvalidLines()
    {
        // Arrange
        WriteRootEditorConfig("this is not a property\n[unclosed\n=\n[*]\nend_of_line = crlf\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("readme.md")).LineEnding;

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Fact]
    public void GetLineEnding_EmptyEditorConfig_ReturnsNull()
    {
        // Arrange
        WriteRootEditorConfig("");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("readme.md")).LineEnding;

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Theory]
    [InlineData("[docs/*.md]", "docs", LineEnding.CrLf)]
    [InlineData("[docs/*.md]", "other", null)]
    [InlineData("[**.md]", "docs", LineEnding.CrLf)]
    public void GetLineEnding_PathGlobSection_MatchesRelativeToEditorConfigDirectory(string section,
        string directoryName, LineEnding? expected)
    {
        // Arrange
        WriteRootEditorConfig($"{section}\nend_of_line = crlf\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath(directoryName, "setup.md")).LineEnding;

        // Assert
        lineEnding
            .Should().Be(expected);
    }

    [Fact]
    public void GetLineEnding_NonNormalizedAbsolutePath_ResolvesNormalizedPath()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = lf\n");
        _directory.WriteFile("[*]\nend_of_line = crlf\n", "docs", EditorConfigFileName);

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("docs", "..", "readme.md")).LineEnding;

        // Assert
        lineEnding
            .Should().Be(LineEnding.Lf);
    }

    [Fact]
    public void GetLineEnding_FileAndDirectoryDoNotExist_ReturnsLineEndingOfEditorConfig()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = crlf\n");

        // Act
        var lineEnding = _sut.GetLineEnding(GetPath("missing", "directory", "new.md")).LineEnding;

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);

        File.Exists(GetPath("missing", "directory", "new.md"))
            .Should().BeFalse();
    }

    [Fact]
    public void GetLineEnding_EditorConfigChangedBetweenCalls_ReturnsCurrentValue()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nindent_size = 2\n");
        var firstLineEnding = _sut.GetLineEnding(GetPath("docs", "setup.md")).LineEnding;

        _directory.WriteFile("[*]\nend_of_line = crlf\n", "docs", EditorConfigFileName);

        // Act
        var secondLineEnding = _sut.GetLineEnding(GetPath("docs", "setup.md")).LineEnding;

        // Assert
        firstLineEnding
            .Should().BeNull();

        secondLineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Fact]
    public void GetLineEnding_EditorConfigRewrittenWithSameTimeAndSize_KeepsParsedFile()
    {
        // Arrange
        var editorConfigPath = _directory.WriteFile("root = true\n\n[*]\nend_of_line = lf\n", EditorConfigFileName);
        var lastWriteTime = File.GetLastWriteTimeUtc(editorConfigPath);
        var firstLineEnding = _sut.GetLineEnding(GetPath("readme.md")).LineEnding;

        // A file with the same time of last write, size, and mode is taken as unchanged.
        File.WriteAllText(editorConfigPath, "root = true\n\n[*]\nend_of_line = cr\n");
        File.SetLastWriteTimeUtc(editorConfigPath, lastWriteTime);

        // Act
        var secondLineEnding = _sut.GetLineEnding(GetPath("readme.md")).LineEnding;

        // Assert
        firstLineEnding
            .Should().Be(LineEnding.Lf);

        secondLineEnding
            .Should().Be(LineEnding.Lf);

        new EditorConfigLineEndingResolver().GetLineEnding(GetPath("readme.md")).LineEnding
            .Should().BeNull("another instance parses the rewritten file");
    }

    [Fact]
    public void GetLineEnding_AllEditorConfigFilesReadable_ReturnsNoWarning()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = lf\n");
        _directory.WriteFile("[*]\nindent_size = 2\n", "docs", EditorConfigFileName);

        // Act
        var result = _sut.GetLineEnding(GetPath("docs", "setup.md"));

        // Assert
        result
            .Should().Be(new EditorConfigLineEndingResult(LineEnding.Lf, null));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void GetLineEnding_EditorConfigCannotBeRead_ReturnsNoLineEndingAndWarning()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        var editorConfigPath = _directory.WriteFile("root = true\n\n[*]\nend_of_line = crlf\n", EditorConfigFileName);

        using var unreadableScope = UnixFileModeScope.MakeUnreadable(editorConfigPath);

        // Act
        var result = _sut.GetLineEnding(GetPath("readme.md"));

        // Assert
        result.LineEnding
            .Should().BeNull();

        var warning = result.Warning
            .Should().BeOfType<EditorConfigUnreadableWarning>().Subject;

        warning.FilePath
            .Should().Be(editorConfigPath);

        warning.Reason
            .Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void GetLineEnding_NestedEditorConfigCannotBeRead_IgnoresReadableParentEditorConfig()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        WriteRootEditorConfig("[*]\nend_of_line = crlf\n");
        var nestedEditorConfigPath = _directory.WriteFile("root = true\n\n[*]\nend_of_line = lf\n", "docs",
            EditorConfigFileName);

        using var unreadableScope = UnixFileModeScope.MakeUnreadable(nestedEditorConfigPath);

        // Act
        var result = _sut.GetLineEnding(GetPath("docs", "setup.md"));

        // Assert
        result.LineEnding
            .Should().BeNull();

        var warning = result.Warning
            .Should().BeOfType<EditorConfigUnreadableWarning>().Subject;

        warning.FilePath
            .Should().Be(nestedEditorConfigPath);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void GetLineEnding_UnreadableEditorConfigAboveRootEditorConfig_IsNotRead()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        var outerEditorConfigPath = _directory.WriteFile("root = true\n\n[*]\nend_of_line = lf\n",
            EditorConfigFileName);
        _directory.WriteFile("root = true\n\n[*]\nend_of_line = crlf\n", "repo", EditorConfigFileName);

        using var unreadableScope = UnixFileModeScope.MakeUnreadable(outerEditorConfigPath);

        // Act
        var result = _sut.GetLineEnding(GetPath("repo", "readme.md"));

        // Assert
        result
            .Should().Be(new EditorConfigLineEndingResult(LineEnding.CrLf, null));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void GetLineEnding_EditorConfigUnreadableThenReadable_ReturnsLineEndingOnSecondCall()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        var editorConfigPath = _directory.WriteFile("root = true\n\n[*]\nend_of_line = crlf\n", EditorConfigFileName);

        EditorConfigLineEndingResult firstResult;

        using (UnixFileModeScope.MakeUnreadable(editorConfigPath))
        {
            firstResult = _sut.GetLineEnding(GetPath("readme.md"));
        }

        // Act
        var secondResult = _sut.GetLineEnding(GetPath("readme.md"));

        // Assert
        firstResult.Warning
            .Should().BeOfType<EditorConfigUnreadableWarning>();

        secondResult
            .Should().Be(new EditorConfigLineEndingResult(LineEnding.CrLf, null));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void GetLineEnding_EditorConfigReadableThenUnreadable_ReturnsWarningOnSecondCall()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        var editorConfigPath = _directory.WriteFile("root = true\n\n[*]\nend_of_line = crlf\n", EditorConfigFileName);
        var firstResult = _sut.GetLineEnding(GetPath("readme.md"));

        using var unreadableScope = UnixFileModeScope.MakeUnreadable(editorConfigPath);

        // Act
        var secondResult = _sut.GetLineEnding(GetPath("readme.md"));

        // Assert
        firstResult
            .Should().Be(new EditorConfigLineEndingResult(LineEnding.CrLf, null));

        secondResult.LineEnding
            .Should().BeNull();

        var warning = secondResult.Warning
            .Should().BeOfType<EditorConfigUnreadableWarning>().Subject;

        warning.FilePath
            .Should().Be(editorConfigPath);
    }

    [Fact]
    public void GetLineEnding_EditorConfigIsDirectory_SkipsItWithoutWarning()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = crlf\n");
        _directory.CreateDirectory("docs", EditorConfigFileName);

        // Act
        var result = _sut.GetLineEnding(GetPath("docs", "setup.md"));

        // Assert
        result
            .Should().Be(new EditorConfigLineEndingResult(LineEnding.CrLf, null));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void GetLineEnding_UnreadableEditorConfigInOtherDirectory_DoesNotAffectFile()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        WriteRootEditorConfig("[*]\nend_of_line = crlf\n");
        var otherEditorConfigPath = _directory.WriteFile("[*]\nend_of_line = lf\n", "other", EditorConfigFileName);

        using var unreadableScope = UnixFileModeScope.MakeUnreadable(otherEditorConfigPath);

        // Act
        var result = _sut.GetLineEnding(GetPath("docs", "setup.md"));

        // Assert
        result
            .Should().Be(new EditorConfigLineEndingResult(LineEnding.CrLf, null));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void GetLineEnding_EditorConfigIsBrokenSymbolicLink_ReturnsNoLineEndingAndWarning()
    {
        // Arrange
        // A symbolic link on Windows needs a privilege that test runs usually do not have.
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Symbolic links cannot be created without a privilege.");
        WriteRootEditorConfig("[*]\nend_of_line = crlf\n");
        var linkPath = GetPath("docs", EditorConfigFileName);
        Directory.CreateDirectory(GetPath("docs"));
        File.CreateSymbolicLink(linkPath, GetPath("docs", "missing-target"));

        // Act
        var result = _sut.GetLineEnding(GetPath("docs", "setup.md"));

        // Assert
        result.LineEnding
            .Should().BeNull();

        var warning = result.Warning
            .Should().BeOfType<EditorConfigUnreadableWarning>().Subject;

        warning.FilePath
            .Should().Be(linkPath);

        warning.Reason
            .Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetLineEnding_EditorConfigLockedExclusively_ReturnsNoLineEndingAndWarning()
    {
        // Arrange
        // .NET emulates FileShare.None with an advisory lock on macOS and Linux, which the reading FileStream of the
        // parser respects, so this test makes the file unreadable on every operating system. The emulation can be
        // switched off with an environment variable; then the lock does not block reading.
        Assert.SkipWhen(IsFileLockingDisabled(), "File locking of .NET is disabled through the environment.");
        var editorConfigPath = _directory.WriteFile("root = true\n\n[*]\nend_of_line = crlf\n", EditorConfigFileName);

        EditorConfigLineEndingResult result;

        using (new FileStream(editorConfigPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Act
            result = _sut.GetLineEnding(GetPath("readme.md"));
        }

        // Assert
        result.LineEnding
            .Should().BeNull();

        var warning = result.Warning
            .Should().BeOfType<EditorConfigUnreadableWarning>().Subject;

        warning.FilePath
            .Should().Be(editorConfigPath);

        warning.IsEditorConfigFilePath
            .Should().BeTrue();

        warning.Reason
            .Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("[{1..99999999999}]")] // OverflowException: the number does not fit into an int
    [InlineData("[{0..-2147483647}]")] // OverflowException: the length of the range overflows
    [InlineData("[x{1..2147483647}]")] // OutOfMemoryException: the range exceeds the maximum array length
    [InlineData("[{1..1000000}{1..3000}]")] // ArgumentOutOfRangeException: the combined count overflows
    public void GetLineEnding_EditorConfigWithMalformedNumericRange_ReturnsNoLineEndingAndInvalidWarning(
        string sectionHeader)
    {
        // Arrange
        var editorConfigPath = _directory.WriteFile($"root = true\n\n{sectionHeader}\nend_of_line = crlf\n",
            EditorConfigFileName);

        // Act
        var result = _sut.GetLineEnding(GetPath("readme.md"));

        // Assert
        result.LineEnding
            .Should().BeNull();

        var warning = result.Warning
            .Should().BeOfType<EditorConfigInvalidWarning>().Subject;

        warning.FilePath
            .Should().Be(editorConfigPath);

        warning.IsEditorConfigFilePath
            .Should().BeTrue();

        warning.Reason
            .Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetLineEnding_MalformedSectionNotMatchingFile_ReturnsInvalidWarning()
    {
        // Arrange
        // The parser matches every section header against the path, so a malformed header fails even if it would
        // not concern the file.
        var editorConfigPath = _directory.WriteFile(
            "root = true\n\n[*]\nend_of_line = crlf\n\n[*.{1..99999999999}]\nend_of_line = lf\n", EditorConfigFileName);

        // Act
        var result = _sut.GetLineEnding(GetPath("readme.md"));

        // Assert
        result.LineEnding
            .Should().BeNull();

        result.Warning
            .Should().BeOfType<EditorConfigInvalidWarning>()
            .Which.FilePath
            .Should().Be(editorConfigPath);
    }

    [Fact]
    public void GetLineEnding_NestedEditorConfigMalformed_ReturnsWarningForNestedFile()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = crlf\n");
        var nestedEditorConfigPath = _directory.WriteFile("[{1..99999999999}]\nend_of_line = lf\n", "docs",
            EditorConfigFileName);

        // Act
        var result = _sut.GetLineEnding(GetPath("docs", "setup.md"));

        // Assert
        result.LineEnding
            .Should().BeNull();

        result.Warning
            .Should().BeOfType<EditorConfigInvalidWarning>()
            .Which.FilePath
            .Should().Be(nestedEditorConfigPath);
    }

    [Fact]
    public void GetLineEnding_ParentEditorConfigMalformed_ReturnsWarningForParentFile()
    {
        // Arrange
        var rootEditorConfigPath = _directory.WriteFile("root = true\n\n[{1..99999999999}]\nend_of_line = crlf\n",
            EditorConfigFileName);
        _directory.WriteFile("[*]\nend_of_line = lf\n", "docs", EditorConfigFileName);

        // Act
        var result = _sut.GetLineEnding(GetPath("docs", "setup.md"));

        // Assert
        result.LineEnding
            .Should().BeNull();

        result.Warning
            .Should().BeOfType<EditorConfigInvalidWarning>()
            .Which.FilePath
            .Should().Be(rootEditorConfigPath);
    }

    [Fact]
    public void GetLineEnding_MalformedEditorConfigAboveRootEditorConfig_IsNotEvaluated()
    {
        // Arrange
        _directory.WriteFile("root = true\n\n[{1..99999999999}]\nend_of_line = lf\n", EditorConfigFileName);
        _directory.WriteFile("root = true\n\n[*]\nend_of_line = crlf\n", "repo", EditorConfigFileName);

        // Act
        var result = _sut.GetLineEnding(GetPath("repo", "readme.md"));

        // Assert
        result
            .Should().Be(new EditorConfigLineEndingResult(LineEnding.CrLf, null));
    }

    [Fact]
    public void GetLineEnding_EditorConfigMalformedThenFixed_ReturnsLineEndingOnSecondCall()
    {
        // Arrange
        var editorConfigPath = _directory.WriteFile("root = true\n\n[{1..99999999999}]\nend_of_line = lf\n",
            EditorConfigFileName);
        var firstResult = _sut.GetLineEnding(GetPath("readme.md"));
        File.WriteAllText(editorConfigPath, "root = true\n\n[{1..3}.md]\nend_of_line = crlf\n");

        // Act
        var secondResult = _sut.GetLineEnding(GetPath("2.md"));

        // Assert
        firstResult.Warning
            .Should().BeOfType<EditorConfigInvalidWarning>();

        secondResult
            .Should().Be(new EditorConfigLineEndingResult(LineEnding.CrLf, null));
    }

    [Fact]
    public void GetLineEnding_EditorConfigValidThenMalformed_ReturnsInvalidWarningOnSecondCall()
    {
        // Arrange
        var editorConfigPath = _directory.WriteFile("root = true\n\n[*]\nend_of_line = crlf\n", EditorConfigFileName);
        var firstResult = _sut.GetLineEnding(GetPath("readme.md"));
        File.WriteAllText(editorConfigPath, "root = true\n\n[{1..99999999999}]\nend_of_line = crlf\n");

        // Act
        var secondResult = _sut.GetLineEnding(GetPath("readme.md"));

        // Assert
        firstResult
            .Should().Be(new EditorConfigLineEndingResult(LineEnding.CrLf, null));

        secondResult.LineEnding
            .Should().BeNull();

        secondResult.Warning
            .Should().BeOfType<EditorConfigInvalidWarning>()
            .Which.FilePath
            .Should().Be(editorConfigPath);
    }

    [Fact]
    public void GetLineEnding_MalformedEditorConfigInOtherDirectory_DoesNotAffectFile()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = crlf\n");
        _directory.WriteFile("[{1..99999999999}]\nend_of_line = lf\n", "other", EditorConfigFileName);

        // Act
        var result = _sut.GetLineEnding(GetPath("docs", "setup.md"));

        // Assert
        result
            .Should().Be(new EditorConfigLineEndingResult(LineEnding.CrLf, null));
    }

    [Fact]
    public void GetLineEnding_ParentEditorConfigUnreadableAndNestedMalformed_ReturnsUnreadableWarning()
    {
        // Arrange
        // The parser reads all .editorconfig files before it evaluates any section, so the read error comes first.
        Assert.SkipWhen(IsFileLockingDisabled(), "File locking of .NET is disabled through the environment.");
        var rootEditorConfigPath = _directory.WriteFile("root = true\n\n[*]\nend_of_line = crlf\n",
            EditorConfigFileName);
        _directory.WriteFile("[{1..99999999999}]\nend_of_line = lf\n", "docs", EditorConfigFileName);

        EditorConfigLineEndingResult result;

        using (new FileStream(rootEditorConfigPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Act
            result = _sut.GetLineEnding(GetPath("docs", "setup.md"));
        }

        // Assert
        result.LineEnding
            .Should().BeNull();

        result.Warning
            .Should().BeOfType<EditorConfigUnreadableWarning>()
            .Which.FilePath
            .Should().Be(rootEditorConfigPath);
    }

    [Theory]
    [MemberData(nameof(ReadExceptions))]
    public void GetLineEnding_ReadErrorNotTracedToEditorConfigFile_ReturnsWarningForResolvedFile(Exception exception)
    {
        // Arrange
        var filePath = GetPath("readme.md");
        var sut = new EditorConfigLineEndingResolver((_, _) => throw exception);

        // Act
        var result = sut.GetLineEnding(filePath);

        // Assert
        result.LineEnding
            .Should().BeNull();

        result.Warning
            .Should().Be(new EditorConfigUnreadableWarning(filePath, exception.Message, IsEditorConfigFilePath: false));
    }

    [Fact]
    public void GetLineEnding_ParseFunctionThrowsOtherException_PropagatesIt()
    {
        // Arrange
        var sut = new EditorConfigLineEndingResolver((_, _) => throw new InvalidOperationException("Unexpected."));

        // Act
        var act = () => sut.GetLineEnding(GetPath("readme.md"));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unexpected.");
    }

    [Theory]
    [MemberData(nameof(InvalidContentExceptions))]
    public void GetLineEnding_InvalidContentErrorNotTracedToEditorConfigFile_ReturnsInvalidWarningForResolvedFile(
        Exception exception)
    {
        // Arrange
        var filePath = GetPath("readme.md");
        var sut = new EditorConfigLineEndingResolver((_, _) => throw exception);

        // Act
        var result = sut.GetLineEnding(filePath);

        // Assert
        result.LineEnding
            .Should().BeNull();

        result.Warning
            .Should().Be(new EditorConfigInvalidWarning(filePath, exception.Message, IsEditorConfigFilePath: false));
    }

    [Fact]
    public void GetLineEnding_EditorConfigFilesReadButNoneFailsOnItsOwn_ReturnsInvalidWarningForResolvedFile()
    {
        // Arrange
        WriteRootEditorConfig("[*]\nend_of_line = crlf\n");
        var filePath = GetPath("readme.md");
        var sut = new EditorConfigLineEndingResolver((parser, path) =>
        {
            parser.Parse(path, editorConfigFiles: null);

            throw new OverflowException("The combined ranges are too large.");
        });

        // Act
        var result = sut.GetLineEnding(filePath);

        // Assert
        result.LineEnding
            .Should().BeNull();

        result.Warning
            .Should().Be(new EditorConfigInvalidWarning(filePath, "The combined ranges are too large.",
                IsEditorConfigFilePath: false));
    }

    [Fact]
    public void GetLineEnding_ParseFunctionCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        var sut = new EditorConfigLineEndingResolver((_, _) => throw new OperationCanceledException());

        // Act
        var act = () => sut.GetLineEnding(GetPath("readme.md"));

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullParseFunction_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new EditorConfigLineEndingResolver(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("parse");
    }

    // The exceptions with which reading an .editorconfig file fails.
    public static TheoryData<Exception> ReadExceptions =>
    [
        new IOException("The disk is not ready."),
        new UnauthorizedAccessException("Access is denied.")
    ];

    // The exceptions with which the EditorConfig core library fails on content it cannot evaluate.
    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
        Justification = "The test simulates the OutOfMemoryException that the EditorConfig core library throws for a " +
                        "numeric range above the maximum array length.")]
    public static TheoryData<Exception> InvalidContentExceptions =>
    [
        new OverflowException("Value was either too large or too small for an Int32."),
        new ArgumentOutOfRangeException("Non-negative number required.", innerException: null),
        new OutOfMemoryException("Array dimensions exceeded supported range.")
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("readme.md")]
    [InlineData("docs/readme.md")]
    public void GetLineEnding_InvalidFilePath_ThrowsArgumentException(string? invalidFilePath)
    {
        // Act
        var act = () => _sut.GetLineEnding(invalidFilePath!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("filePath");
    }

    // The temporary directory lies below the system temp directory, where an .editorconfig of the machine could
    // exist. root = true keeps every test independent of it.
    private void WriteRootEditorConfig(string sections)
    {
        _directory.WriteFile("root = true\n\n" + sections, EditorConfigFileName);
    }

    // DOTNET_SYSTEM_IO_DISABLEFILELOCKING=1 or true switches off the advisory locks with which .NET emulates FileShare
    // on Unix; Windows always enforces sharing modes.
    private static bool IsFileLockingDisabled()
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        var value = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_IO_DISABLEFILELOCKING");

        return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private string GetPath(params string[] relativePathParts)
    {
        return Path.Combine([_directory.RootPath, .. relativePathParts]);
    }
}
