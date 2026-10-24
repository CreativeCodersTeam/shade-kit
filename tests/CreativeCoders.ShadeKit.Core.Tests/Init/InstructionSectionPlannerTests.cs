using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Init;

public class InstructionSectionPlannerTests
{
    private const string FileName = "CLAUDE.md";

    private const string Begin = "<!-- shade-kit:begin -->";

    private const string End = "<!-- shade-kit:end -->";

    private const string ExpectedSection = ManagedInstructionSection.Text;

    public static TheoryData<string, string> InvalidMarkerFiles { get; } = new()
    {
        { $"# Title\n{Begin}\nold\n", "The begin marker in line 2 has no end marker." },
        { $"# Title\n{End}\n", "The end marker in line 2 has no begin marker." },
        { $"{End}\ntext\n{Begin}\n", "The end marker in line 1 comes before the begin marker in line 3." },
        { $"{Begin}\n{Begin}\n{End}\n", "The file contains more than one begin marker (lines 1, 2)." },
        { $"{Begin}\n{End}\n{End}\n", "The file contains more than one end marker (lines 2, 3)." },
        { $"{Begin}\n{End}\n{Begin}\n{End}\n", "The file contains more than one begin marker (lines 1, 3)." },
        { $"  {Begin}\r\n\t{Begin} again\r\n{End}\r\n", "The file contains more than one begin marker (lines 1, 2)." },
        { $"{End}\n{End}\n", "The file contains more than one end marker (lines 1, 2)." },
        { $"{Begin}\n{Begin}\n", "The file contains more than one begin marker (lines 1, 2)." },
        { $"    ```\n{Begin}\n{End}\n    ```\n{Begin}\n{End}\n", "The file contains more than one begin marker (lines 2, 5)." },
        { $"```\n{Begin}\n```\n{End}\n", "The end marker in line 4 has no begin marker." },
        { $"{Begin}\n```\n{End}\n```\n", "The begin marker in line 1 has no end marker." },
        { $"<!-- shade-kit:begin\n{End}\n", "The end marker in line 2 has no begin marker." },
        { $"```\n{Begin}\n```js\n{End}\n```\n{End}\n", "The end marker in line 6 has no begin marker." },
        { $"```\n{Begin}\n    ```\n{End}\n```\n{End}\n", "The end marker in line 6 has no begin marker." },
        { $"\t```\n{Begin}\n{End}\n\t```\n{Begin}\n{End}\n", "The file contains more than one begin marker (lines 2, 5)." },
        { $"# Docs\n```\n{Begin}\nnever closed\n{End}\n", "The code block opened in line 2 is never closed, so the section cannot be placed after it." },
        { "# Docs\n\n~~~~\ncode\n~~~\n", "The code block opened in line 3 is never closed, so the section cannot be placed after it." },
        { $"{Begin}\n```\nopen\n{End}\n", "The code block opened in line 2 is never closed, so the section cannot be placed after it." }
    };

    // Files whose marker lines are no markers: examples in a fenced code block, or lines that only look like one.
    public static TheoryData<string> FilesWithoutMarkers { get; } = new()
    {
        $"# Docs\n```\n{Begin}\nexample\n{End}\n```\n",
        $"~~~ markdown\n{Begin}\n{End}\n~~~\n",
        $"```\n{Begin}\n{End}\n```\ntext\n```markdown\n{Begin}\n{End}\n```\n",
        $"   ```\n{Begin}\n{End}\n   ```\n",
        $"````\n```\n{Begin}\n{End}\n```\n````\n",
        $"~~~\n{Begin}\n```\n{End}\n~~~\n",
        $"```\r\n{Begin}\r\n{End}\r\n```\r\n",
        $"```\n{Begin}\n{End}\n````\n",
        $"~~~\n{Begin}\n{End}\n~~~~~\n",
        $"```\n{Begin}\n{End}\n```  \n",
        $"~~~ a`b\n{Begin}\n{End}\n~~~\n",
        "<!-- shade-kit:beginning -->\n",
        "<!-- shade-kit:begin_x -->\n"
    };

    // Files with one section outside any fenced code block, and the expected file after the update.
    public static TheoryData<string, string> FilesWithSection { get; } = new()
    {
        { $"```\n{Begin}\nexample\n{End}\n```\n{Begin}\nold\n{End}\n", $"```\n{Begin}\nexample\n{End}\n```\n{ExpectedSection}\n" },
        { $"<!-- shade-kit:beginning -->\n{Begin}\nold\n{End}\n", $"<!-- shade-kit:beginning -->\n{ExpectedSection}\n" },
        { $"<!-- shade-kit:begin-->\nold\n{End}\n", $"{ExpectedSection}\n" },
        { $"<!-- shade-kit:begin\tlegacy -->\nold\n{End}\n", $"{ExpectedSection}\n" },
        { $"    ```\n{Begin}\nold\n{End}\n", $"    ```\n{ExpectedSection}\n" },
        { $"```a`b\n{Begin}\nold\n{End}\n", $"```a`b\n{ExpectedSection}\n" },
        { $"``\n{Begin}\nold\n{End}\n", $"``\n{ExpectedSection}\n" },
        { $"```\ncode\n```\n{Begin}\nold\n{End}\n", $"```\ncode\n```\n{ExpectedSection}\n" },
        { $"{Begin}\nold\n```\n{End}\n```\n{End}\nrest\n", $"{ExpectedSection}\nrest\n" }
    };

    public static TheoryData<byte[], string> UnsupportedEncodingFiles { get; } = new()
    {
        { [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("# Project\r\n")], "UTF-16 LE" },
        { [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes("# Project\n")], "UTF-16 BE" },
        { [0xFF, 0xFE, 0x00, 0x00, .. Encoding.UTF32.GetBytes("# Project\n")], "UTF-32 LE" },
        { [0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, (byte)'#'], "UTF-32 BE" }
    };

    [Fact]
    public async Task PlanAsync_FileWrittenFromPlan_SecondCallReturnsNullAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = Path.Combine(tempDirectory.RootPath, FileName);
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);
        var afterFirstCall = tempDirectory.ReadFileBytes(FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().BeNull();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(afterFirstCall);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlanAsync_SectionAtStartOfFile_ReplacesSectionAndKeepsBom(bool withBom)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        byte[] prefix = withBom ? TestText.Utf8Bom : [];
        var filePath = tempDirectory.WriteFileBytes([.. prefix, .. Encoding.UTF8.GetBytes($"{Begin}\nold\n{End}\nrest\n")],
            FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Updated);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([.. prefix, .. Encoding.UTF8.GetBytes($"{ExpectedSection}\nrest\n")]);
    }

    [Theory]
    [InlineData(true, "\n")]
    [InlineData(false, "")]
    public async Task PlanAsync_IdenticalSectionWithBomOrWithoutTerminator_ReturnsNullAndLeavesFileUnchanged(
        bool withBom, string terminator)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        byte[] prefix = withBom ? TestText.Utf8Bom : [];
        byte[] original = [.. prefix, .. Encoding.UTF8.GetBytes($"intro\n{ExpectedSection}{terminator}")];
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().BeNull();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Theory]
    [InlineData(LineEnding.Lf)]
    [InlineData(LineEnding.CrLf)]
    public async Task PlanAsync_CurrentSectionWithOtherOrMixedLineEndings_ReturnsNullAndLeavesFileUnchanged(
        LineEnding lineEnding)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile($"intro\r\n{ExpectedSection}\n", FileName);
        var original = tempDirectory.ReadFileBytes(FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken, lineEnding);

        // Assert
        action
            .Should().BeNull();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Theory]
    [InlineData(LineEnding.Lf, "\n")]
    [InlineData(LineEnding.CrLf, "\r\n")]
    public async Task PlanAsync_OutdatedSectionInMixedFile_WritesWholeFileWithTargetLineEnding(LineEnding lineEnding,
        string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile($"intro\r\n{Begin}\nold\r\n{End}\noutro", FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken, lineEnding);

        // Assert
        action
            .Should().Be(InitializedFileAction.Updated);

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"intro{newLine}{ExpectedSection.Replace("\n", newLine, StringComparison.Ordinal)}" +
                         $"{newLine}outro");
    }

    [Theory]
    [InlineData(LineEnding.Lf, "\n")]
    [InlineData(LineEnding.CrLf, "\r\n")]
    public async Task PlanAsync_FileMissingWithLineEnding_CreatesSectionWithTargetLineEnding(LineEnding lineEnding,
        string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = Path.Combine(tempDirectory.RootPath, FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken, lineEnding);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be(ExpectedSection.Replace("\n", newLine, StringComparison.Ordinal) + newLine);
    }

    [Fact]
    public async Task PlanAsync_InvalidUtf8File_ThrowsUnsupportedFileEncodingExceptionAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        byte[] original = [(byte)'#', (byte)' ', 0xC3, (byte)'\n'];
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionPlanner().PlanAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UnsupportedFileEncodingException>())
            .Which.FilePath.Should().Be(filePath);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlanAsync_EmptyFile_AppendsOnlyTheSection(bool withBom)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        byte[] prefix = withBom ? TestText.Utf8Bom : [];
        var filePath = tempDirectory.WriteFileBytes(prefix, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([.. prefix, .. Encoding.UTF8.GetBytes(ExpectedSection + "\n")]);
    }

    [Fact]
    public async Task PlanAsync_CanceledTokenWithExistingFile_ThrowsAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = "# Project\n"u8.ToArray();
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionPlanner().PlanAsync(filePath, new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public async Task PlanAsync_FileMissing_CreatesFileWithOnlyTheSection()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = Path.Combine(tempDirectory.RootPath, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Created);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(Encoding.UTF8.GetBytes(ExpectedSection + "\n"));
    }

    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public async Task PlanAsync_FileWithoutMarkers_AppendsSectionAndKeepsExistingContentUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = Encoding.UTF8.GetBytes("# Project\n\nRules für Ümlaute.\n");
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([.. original, .. Encoding.UTF8.GetBytes("\n" + ExpectedSection + "\n")]);
    }

    [Fact]
    public async Task PlanAsync_FileWithoutTrailingNewline_AddsNewlineBeforeBlankLine()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("# Project", FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be("# Project\n\n" + ExpectedSection + "\n");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task PlanAsync_FileEndsWithBlankLine_AppendsSectionWithoutAnotherBlankLine(string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var text = $"# Project{newLine}{newLine}";
        var filePath = tempDirectory.WriteFile(text, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken, ToLineEnding(newLine));

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileText(FileName)
            .Should().Be(text + ExpectedSection.Replace("\n", newLine, StringComparison.Ordinal) + newLine);
    }

    [Theory]
    [MemberData(nameof(UnsupportedEncodingFiles))]
    public async Task PlanAsync_Utf16OrUtf32File_ThrowsUnsupportedFileEncodingExceptionAndLeavesFileUnchanged(
        byte[] original, string expectedEncodingName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionPlanner().PlanAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UnsupportedFileEncodingException>()).Which;

        exception.FilePath
            .Should().Be(filePath);

        exception.EncodingName
            .Should().Be(expectedEncodingName);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Fact]
    public async Task PlanAsync_PathIsDirectory_ThrowsPathNotAFileException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var directoryPath = tempDirectory.CreateDirectory(FileName);

        // Act
        var act = () => new InstructionSectionPlanner().PlanAsync(directoryPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PathNotAFileException>())
            .Which.Path.Should().Be(directoryPath);

        Directory.Exists(directoryPath)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(LineEnding.CrLf, "\r\n")]
    [InlineData(LineEnding.Lf, "\n")]
    public async Task PlanAsync_CrLfFileWithBomWithoutMarkers_AppendsWithTargetLineEndingAndKeepsBom(
        LineEnding lineEnding, string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes([.. TestText.Utf8Bom, .. "# Project\r\n"u8], FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken, lineEnding);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([
                .. TestText.Utf8Bom,
                .. Encoding.UTF8.GetBytes(
                    $"# Project{newLine}{newLine}" + ExpectedSection.Replace("\n", newLine, StringComparison.Ordinal) +
                    newLine)
            ]);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public async Task PlanAsync_SectionWithOtherContent_ReplacesSectionAndKeepsContentOutsideUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        const string before = "# Project\n\nIntro.\n";
        const string after = "\n## Other\n\nMore.\n";
        var filePath = tempDirectory.WriteFile($"{before}{Begin}\nold content\n{End}{after}", FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Updated);

        var text = tempDirectory.ReadFileText(FileName);

        text
            .Should().Be(before + ExpectedSection + after);

        TestText.CountLinesStartingWith(text, ManagedInstructionSection.BeginMarkerPrefix)
            .Should().Be(1);

        TestText.CountLinesStartingWith(text, ManagedInstructionSection.EndMarker)
            .Should().Be(1);
    }

    [Fact]
    public async Task PlanAsync_IndentedMarkers_ReplacesWholeMarkerLines()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile($"intro\n  {Begin}  \nold\n\t{End}\t\noutro\n", FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Updated);

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"intro\n{ExpectedSection}\noutro\n");
    }

    [Fact]
    public async Task PlanAsync_SectionIsLastLineWithoutTerminator_ReplacesSectionWithoutAddingTerminator()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile($"intro\n{Begin}\nold\n{End}", FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be($"intro\n{ExpectedSection}");
    }

    [Theory]
    [InlineData(LineEnding.CrLf, "\r\n")]
    [InlineData(LineEnding.Lf, "\n")]
    public async Task PlanAsync_CrLfFileWithBomAndOtherSection_ReplacesWithTargetLineEndingAndKeepsBom(
        LineEnding lineEnding, string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(
            [.. TestText.Utf8Bom, .. Encoding.UTF8.GetBytes($"intro\r\n{Begin}\r\nold\r\n{End}\r\noutro\r\n")], FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken, lineEnding);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([
                .. TestText.Utf8Bom,
                .. Encoding.UTF8.GetBytes(
                    $"intro{newLine}{ExpectedSection.Replace("\n", newLine, StringComparison.Ordinal)}{newLine}" +
                    $"outro{newLine}")
            ]);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task PlanAsync_SectionIdentical_ReturnsNullAndLeavesFileUnchanged(string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var text = $"intro{newLine}{ExpectedSection.Replace("\n", newLine, StringComparison.Ordinal)}{newLine}" +
                   $"outro{newLine}";
        var original = Encoding.UTF8.GetBytes(text);
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().BeNull();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public async Task PlanAsync_CalledTwice_FileContainsExactlyOneSection()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("# Project\n", FileName);
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);
        var afterFirstCall = tempDirectory.ReadFileBytes(FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().BeNull();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(afterFirstCall);

        TestText.CountLinesStartingWith(tempDirectory.ReadFileText(FileName), ManagedInstructionSection.BeginMarkerPrefix)
            .Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(FilesWithoutMarkers))]
    public async Task PlanAsync_MarkerLinesInCodeFenceOrNotMarkers_AppendsSectionAndKeepsThem(string text)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile(text, FileName);
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken, ToLineEnding(newLine));

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileText(FileName)
            .Should().Be(text + newLine + ExpectedSection.ReplaceLineEndings(newLine) + newLine);
    }

    [Theory]
    [MemberData(nameof(FilesWithSection))]
    public async Task PlanAsync_SectionOutsideCodeFences_ReplacesOnlyThatSection(string text, string expected)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile(text, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Updated);

        tempDirectory.ReadFileText(FileName)
            .Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(InvalidMarkerFiles))]
    public async Task PlanAsync_InvalidMarkers_ThrowsInvalidManagedSectionExceptionAndLeavesFileUnchanged(
        string text, string expectedReason)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = Encoding.UTF8.GetBytes(text);
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionPlanner().PlanAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidManagedSectionException>()).Which;

        exception.FilePath
            .Should().Be(filePath);

        exception.Reason
            .Should().Be(expectedReason);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Theory]
    [InlineData("# Project\n")]
    [InlineData($"intro\n{Begin}\nold\n{End}\n")]
    [InlineData($"intro\n  {Begin}\n\t{End}  ")]
    [InlineData("<!-- shade-kit:end --> trailing text\n<!-- shade-kit:ending -->\n")]
    [InlineData("<!-- SHADE-KIT:begin -->\n<!-- SHADE-KIT:end -->\n")]
    public async Task PlanAsync_ValidFile_DoesNotThrowAndLeavesFileUnchanged(string text)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = Encoding.UTF8.GetBytes(text);
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionPlanner().PlanAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Fact]
    public async Task PlanAsync_FileMissing_DoesNotThrowAndCreatesNoFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = Path.Combine(tempDirectory.RootPath, FileName);

        // Act
        var act = () => new InstructionSectionPlanner().PlanAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();

        File.Exists(filePath)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("CLAUDE.md")]
    public async Task PlanAsync_PathNotAbsolute_ThrowsArgumentException(string? filePath)
    {
        // Act
        var act = () => new InstructionSectionPlanner().PlanAsync(filePath!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PlanAsync_CanceledToken_ThrowsAndCreatesNoFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = Path.Combine(tempDirectory.RootPath, FileName);

        // Act
        var act = () => new InstructionSectionPlanner().PlanAsync(filePath, new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        File.Exists(filePath)
            .Should().BeFalse();
    }

    private static LineEnding ToLineEnding(string newLine)
    {
        return newLine == "\r\n" ? LineEnding.CrLf : LineEnding.Lf;
    }

    /// <summary>
    /// Plans the change of an instruction file and writes it through a <see cref="TextFileWriter" /> with a fixed
    /// line ending, as the initialisation does.
    /// </summary>
    /// <returns>The action of the planned change, or <see langword="null" /> if the file needs no change.</returns>
    private static async Task<InitializedFileAction?> PlanAndWriteAsync(string filePath,
        CancellationToken cancellationToken, LineEnding lineEnding = LineEnding.Lf)
    {
        var change = await new InstructionSectionPlanner().PlanAsync(filePath, cancellationToken);

        if (change is not null)
        {
            await FixedLineEnding.WriteAsync(filePath, change.Content, lineEnding, cancellationToken);
        }

        return change?.Action;
    }
}
