using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Tests.Store;

namespace CreativeCoders.ShadeKit.Core.Tests.Init;

public class InstructionSectionWriterTests
{
    private const string FileName = "CLAUDE.md";

    private const string Begin = "<!-- shade-kit:begin -->";

    private const string End = "<!-- shade-kit:end -->";

    private const string ExpectedSection = ManagedInstructionSection.Text;

    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

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
        { $"<!-- shade-kit:beginning -->\n{Begin}\n{End}\n", "The file contains more than one begin marker (lines 1, 2)." }
    };

    public static TheoryData<byte[], string> UnsupportedEncodingFiles { get; } = new()
    {
        { [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("# Project\r\n")], "UTF-16 LE" },
        { [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes("# Project\n")], "UTF-16 BE" },
        { [0xFF, 0xFE, 0x00, 0x00, .. Encoding.UTF32.GetBytes("# Project\n")], "UTF-32 LE" },
        { [0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, (byte)'#'], "UTF-32 BE" }
    };

    [Fact]
    public async Task ApplyAsync_FileCreatedByApply_SecondCallReturnsNullAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = Path.Combine(tempDirectory.RootPath, FileName);
        var writer = new InstructionSectionWriter();
        await writer.ApplyAsync(filePath, TestContext.Current.CancellationToken);
        var afterFirstCall = tempDirectory.ReadFileBytes(FileName);

        // Act
        var action = await writer.ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().BeNull();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(afterFirstCall);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplyAsync_SectionAtStartOfFile_ReplacesSectionAndKeepsBom(bool withBom)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        byte[] prefix = withBom ? Bom : [];
        var filePath = tempDirectory.WriteFileBytes([.. prefix, .. Encoding.UTF8.GetBytes($"{Begin}\nold\n{End}\nrest\n")],
            FileName);

        // Act
        var action = await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Updated);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([.. prefix, .. Encoding.UTF8.GetBytes($"{ExpectedSection}\nrest\n")]);
    }

    [Theory]
    [InlineData(true, "\n")]
    [InlineData(false, "")]
    public async Task ApplyAsync_IdenticalSectionWithBomOrWithoutTerminator_ReturnsNullAndLeavesFileUnchanged(
        bool withBom, string terminator)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        byte[] prefix = withBom ? Bom : [];
        byte[] original = [.. prefix, .. Encoding.UTF8.GetBytes($"intro\n{ExpectedSection}{terminator}")];
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var action = await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().BeNull();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Fact]
    public async Task ApplyAsync_LfSectionInFileWithCrLf_UpdatesSectionToCrLfOnce()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile($"intro\r\n{ExpectedSection}\n", FileName);
        var writer = new InstructionSectionWriter();

        // Act
        var firstAction = await writer.ApplyAsync(filePath, TestContext.Current.CancellationToken);
        var secondAction = await writer.ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        firstAction
            .Should().Be(InitializedFileAction.Updated);

        secondAction
            .Should().BeNull();

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"intro\r\n{ToCrLf(ExpectedSection)}\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplyAsync_EmptyFile_AppendsOnlyTheSection(bool withBom)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        byte[] prefix = withBom ? Bom : [];
        var filePath = tempDirectory.WriteFileBytes(prefix, FileName);

        // Act
        var action = await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([.. prefix, .. Encoding.UTF8.GetBytes(ExpectedSection + "\n")]);
    }

    [Fact]
    public async Task ApplyAsync_CanceledTokenWithExistingFile_ThrowsAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = "# Project\n"u8.ToArray();
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionWriter().ApplyAsync(filePath, new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public async Task ApplyAsync_FileMissing_CreatesFileWithOnlyTheSection()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = Path.Combine(tempDirectory.RootPath, FileName);

        // Act
        var action = await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Created);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(Encoding.UTF8.GetBytes(ExpectedSection + "\n"));
    }

    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public async Task ApplyAsync_FileWithoutMarkers_AppendsSectionAndKeepsExistingContentUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = Encoding.UTF8.GetBytes("# Project\n\nRules für Ümlaute.\n");
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var action = await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([.. original, .. Encoding.UTF8.GetBytes("\n" + ExpectedSection + "\n")]);
    }

    [Fact]
    public async Task ApplyAsync_FileWithoutTrailingNewline_AddsNewlineBeforeBlankLine()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("# Project", FileName);

        // Act
        await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be("# Project\n\n" + ExpectedSection + "\n");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task ApplyAsync_FileEndsWithBlankLine_AppendsSectionWithoutAnotherBlankLine(string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var text = $"# Project{newLine}{newLine}";
        var filePath = tempDirectory.WriteFile(text, FileName);

        // Act
        var action = await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileText(FileName)
            .Should().Be(text + ExpectedSection.Replace("\n", newLine, StringComparison.Ordinal) + newLine);
    }

    [Theory]
    [MemberData(nameof(UnsupportedEncodingFiles))]
    public async Task ApplyAsync_Utf16OrUtf32File_ThrowsUnsupportedFileEncodingExceptionAndLeavesFileUnchanged(
        byte[] original, string expectedEncodingName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UnsupportedFileEncodingException>()).Which;

        exception.FilePath
            .Should().Be(filePath);

        exception.EncodingName
            .Should().Be(expectedEncodingName);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Theory]
    [MemberData(nameof(UnsupportedEncodingFiles))]
    public async Task ValidateAsync_Utf16OrUtf32File_ThrowsUnsupportedFileEncodingExceptionAndLeavesFileUnchanged(
        byte[] original, string expectedEncodingName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionWriter().ValidateAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UnsupportedFileEncodingException>())
            .Which.EncodingName.Should().Be(expectedEncodingName);

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Fact]
    public async Task ApplyAsync_PathIsDirectory_ThrowsPathNotAFileException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var directoryPath = tempDirectory.CreateDirectory(FileName);

        // Act
        var act = () => new InstructionSectionWriter().ApplyAsync(directoryPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PathNotAFileException>())
            .Which.Path.Should().Be(directoryPath);

        Directory.Exists(directoryPath)
            .Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_PathIsDirectory_ThrowsPathNotAFileException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var directoryPath = tempDirectory.CreateDirectory(FileName);

        // Act
        var act = () => new InstructionSectionWriter().ValidateAsync(directoryPath,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PathNotAFileException>())
            .Which.Path.Should().Be(directoryPath);
    }

    [Fact]
    public async Task ApplyAsync_CrLfFileWithBomWithoutMarkers_AppendsWithCrLfAndKeepsBom()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes([.. Bom, .. "# Project\r\n"u8], FileName);

        // Act
        await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([
                .. Bom,
                .. Encoding.UTF8.GetBytes("# Project\r\n\r\n" + ToCrLf(ExpectedSection) + "\r\n")
            ]);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public async Task ApplyAsync_SectionWithOtherContent_ReplacesSectionAndKeepsContentOutsideUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        const string before = "# Project\n\nIntro.\n";
        const string after = "\n## Other\n\nMore.\n";
        var filePath = tempDirectory.WriteFile($"{before}{Begin}\nold content\n{End}{after}", FileName);

        // Act
        var action = await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Updated);

        var text = tempDirectory.ReadFileText(FileName);

        text
            .Should().Be(before + ExpectedSection + after);

        CountLinesStartingWith(text, ManagedInstructionSection.BeginMarkerPrefix)
            .Should().Be(1);

        CountLinesStartingWith(text, ManagedInstructionSection.EndMarker)
            .Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_IndentedMarkers_ReplacesWholeMarkerLines()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile($"intro\n  {Begin}  \nold\n\t{End}\t\noutro\n", FileName);

        // Act
        var action = await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Updated);

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"intro\n{ExpectedSection}\noutro\n");
    }

    [Fact]
    public async Task ApplyAsync_SectionIsLastLineWithoutTerminator_ReplacesSectionWithoutAddingTerminator()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile($"intro\n{Begin}\nold\n{End}", FileName);

        // Act
        await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be($"intro\n{ExpectedSection}");
    }

    [Fact]
    public async Task ApplyAsync_CrLfFileWithBomAndOtherSection_ReplacesWithCrLfAndKeepsBom()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(
            [.. Bom, .. Encoding.UTF8.GetBytes($"intro\r\n{Begin}\r\nold\r\n{End}\r\noutro\r\n")], FileName);

        // Act
        await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([
                .. Bom,
                .. Encoding.UTF8.GetBytes($"intro\r\n{ToCrLf(ExpectedSection)}\r\noutro\r\n")
            ]);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task ApplyAsync_SectionIdentical_ReturnsNullAndLeavesFileUnchanged(string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var text = $"intro{newLine}{ExpectedSection.Replace("\n", newLine, StringComparison.Ordinal)}{newLine}" +
                   $"outro{newLine}";
        var original = Encoding.UTF8.GetBytes(text);
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var action = await new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().BeNull();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public async Task ApplyAsync_CalledTwice_FileContainsExactlyOneSection()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("# Project\n", FileName);
        var writer = new InstructionSectionWriter();
        await writer.ApplyAsync(filePath, TestContext.Current.CancellationToken);
        var afterFirstCall = tempDirectory.ReadFileBytes(FileName);

        // Act
        var action = await writer.ApplyAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().BeNull();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(afterFirstCall);

        CountLinesStartingWith(tempDirectory.ReadFileText(FileName), ManagedInstructionSection.BeginMarkerPrefix)
            .Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(InvalidMarkerFiles))]
    public async Task ApplyAsync_InvalidMarkers_ThrowsInvalidManagedSectionExceptionAndLeavesFileUnchanged(
        string text, string expectedReason)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = Encoding.UTF8.GetBytes(text);
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionWriter().ApplyAsync(filePath, TestContext.Current.CancellationToken);

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
    [MemberData(nameof(InvalidMarkerFiles))]
    public async Task ValidateAsync_InvalidMarkers_ThrowsInvalidManagedSectionExceptionAndLeavesFileUnchanged(
        string text, string expectedReason)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = Encoding.UTF8.GetBytes(text);
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionWriter().ValidateAsync(filePath, TestContext.Current.CancellationToken);

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
    public async Task ValidateAsync_ValidFile_DoesNotThrowAndLeavesFileUnchanged(string text)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = Encoding.UTF8.GetBytes(text);
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new InstructionSectionWriter().ValidateAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Fact]
    public async Task ValidateAsync_FileMissing_DoesNotThrowAndCreatesNoFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = Path.Combine(tempDirectory.RootPath, FileName);

        // Act
        var act = () => new InstructionSectionWriter().ValidateAsync(filePath, TestContext.Current.CancellationToken);

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
    public async Task ApplyAsync_PathNotAbsolute_ThrowsArgumentException(string? filePath)
    {
        // Act
        var act = () => new InstructionSectionWriter().ApplyAsync(filePath!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("CLAUDE.md")]
    public async Task ValidateAsync_PathNotAbsolute_ThrowsArgumentException(string? filePath)
    {
        // Act
        var act = () => new InstructionSectionWriter().ValidateAsync(filePath!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ApplyAsync_CanceledToken_ThrowsAndCreatesNoFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = Path.Combine(tempDirectory.RootPath, FileName);

        // Act
        var act = () => new InstructionSectionWriter().ApplyAsync(filePath, new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        File.Exists(filePath)
            .Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("# Project\n", FileName);

        // Act
        var act = () => new InstructionSectionWriter().ValidateAsync(filePath, new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static string ToCrLf(string text)
    {
        return text.Replace("\n", "\r\n", StringComparison.Ordinal);
    }

    private static int CountLinesStartingWith(string text, string prefix)
    {
        return text.Split('\n').Count(line => line.Trim().StartsWith(prefix, StringComparison.Ordinal));
    }
}
