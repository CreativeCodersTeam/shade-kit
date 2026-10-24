using System.Runtime.Versioning;
using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

public class TextFileContentTests
{
    private const string FileName = "notes.txt";

    private const string UnmarkedWideEncodingName = "UTF-16 or UTF-32 without byte order mark";

    private const string InvalidUtf8EncodingName = "an unknown encoding with bytes that are not valid UTF-8";

    public static TheoryData<byte[], string> UnsupportedEncodingFiles { get; } = new TheoryData<byte[], string>
    {
        { [0xFF, 0xFE, (byte)'a', 0x00, (byte)'\n', 0x00], "UTF-16 LE" },
        { [0xFE, 0xFF, 0x00, (byte)'a', 0x00, (byte)'\n'], "UTF-16 BE" },
        { [0xFF, 0xFE, 0x00, 0x00, (byte)'a', 0x00, 0x00, 0x00], "UTF-32 LE" },
        { [0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, (byte)'a'], "UTF-32 BE" },
        { [0xFF, 0xFE], "UTF-16 LE" },
        { [0xFE, 0xFF], "UTF-16 BE" },
        { [0xFF, 0xFE, 0x00, 0x00], "UTF-32 LE" },
        { [0x00, 0x00, 0xFE, 0xFF], "UTF-32 BE" },
        { [0xFF, 0xFE, 0x00], "UTF-16 LE" },
        { [0xFF, 0xFE, 0x00, 0x01], "UTF-16 LE" },
        { [.. Encoding.Unicode.GetBytes("# Project\r\n")], UnmarkedWideEncodingName },
        { [.. Encoding.BigEndianUnicode.GetBytes("# Project\n")], UnmarkedWideEncodingName },
        { [.. new UTF32Encoding(false, false).GetBytes("# Project\n")], UnmarkedWideEncodingName },
        { [(byte)'a', (byte)'\n', 0x00, (byte)'b', (byte)'\n'], UnmarkedWideEncodingName },
        { [0x00, 0x00, 0xFE], UnmarkedWideEncodingName },
        { [0x00, 0xFE, 0xFF], UnmarkedWideEncodingName },
        { [0xEF, 0xBB, 0xBF, 0x00], UnmarkedWideEncodingName },
        { [0x00], UnmarkedWideEncodingName },
        { [0xFF], InvalidUtf8EncodingName },
        { [0xFE], InvalidUtf8EncodingName },
        { [0xFF, 0xC3, (byte)'\n'], InvalidUtf8EncodingName },
        { [0xFE, 0xFE, 0xFF], InvalidUtf8EncodingName },
        { [0xEF, 0xBB, 0xBF, 0xFF, 0xFE, (byte)'\n'], InvalidUtf8EncodingName },
        { [(byte)'a', (byte)'\n', 0xC3], InvalidUtf8EncodingName },
        { [.. Encoding.Latin1.GetBytes("# Grüße\n")], InvalidUtf8EncodingName }
    };

    [Fact]
    public async Task ReadAsync_FileMissing_ReturnsNull()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();

        // Act
        var content = await TextFileContent.ReadAsync(Path.Combine(tempDirectory.RootPath, FileName),
            TestContext.Current.CancellationToken);

        // Assert
        content
            .Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/notes.txt")]
    public async Task ReadAsync_PathNotAbsolute_ThrowsArgumentException(string filePath)
    {
        // Act
        var act = () => TextFileContent.ReadAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReadAsync_NullPath_ThrowsArgumentException()
    {
        // Act
        var act = () => TextFileContent.ReadAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReadAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("text\n", FileName);

        // Act
        var act = () => TextFileContent.ReadAsync(filePath, new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [MemberData(nameof(UnsupportedEncodingFiles))]
    public async Task ReadAsync_FileNotUtf8_ThrowsUnsupportedFileEncodingException(byte[] bytes,
        string expectedEncodingName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(bytes, FileName);

        // Act
        var act = () => TextFileContent.ReadAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UnsupportedFileEncodingException>()).Which;

        exception.FilePath
            .Should().Be(filePath);

        exception.EncodingName
            .Should().Be(expectedEncodingName);
    }

    [Fact]
    public async Task ReadAsync_Utf8FileWithNonAsciiCharacters_ReturnsDecodedLines()
    {
        // Arrange
        var content = await ReadAsync(Encoding.UTF8.GetBytes("# Grüße 漢字\nä\n"));

        // Act
        var lines = content.Lines;

        // Assert
        lines
            .Should().Equal("# Grüße 漢字", "ä");
    }

    [Fact]
    public async Task ReadAsync_PathIsDirectory_ThrowsPathNotAFileException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var directoryPath = tempDirectory.CreateDirectory(FileName);

        // Act
        var act = () => TextFileContent.ReadAsync(directoryPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PathNotAFileException>())
            .Which.Path.Should().Be(directoryPath);
    }

    [Theory]
    [InlineData("a\n\n b \n", new[] { "a", "", " b " }, true)]
    [InlineData("a\r\nb", new[] { "a", "b" }, false)]
    [InlineData("a\r\n\r\nb", new[] { "a", "", "b" }, false)]
    [InlineData("a\nb\r\n", new[] { "a", "b" }, true)]
    [InlineData("\nb", new[] { "", "b" }, false)]
    [InlineData("\n", new[] { "" }, true)]
    [InlineData("\r\n", new[] { "" }, true)]
    [InlineData("\n\n", new[] { "", "" }, true)]
    [InlineData("a", new[] { "a" }, false)]
    [InlineData("", new string[0], false)]
    public async Task ReadAsync_Utf8File_SplitsLinesAtLfAndCrLf(string text, string[] expectedLines,
        bool expectedFinalLineEnding)
    {
        // Act
        var content = await ReadAsync(Encoding.UTF8.GetBytes(text));

        // Assert
        content.Lines
            .Should().Equal(expectedLines);

        content.HasFinalLineEnding
            .Should().Be(expectedFinalLineEnding);

        content.HasUtf8Bom
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("a\rb\n", new[] { "a\rb" }, true)]
    [InlineData("a\r", new[] { "a\r" }, false)]
    [InlineData("a\r\r\nb", new[] { "a\r", "b" }, false)]
    [InlineData("\r", new[] { "\r" }, false)]
    public async Task ReadAsync_LoneCarriageReturn_StaysPartOfLine(string text, string[] expectedLines,
        bool expectedFinalLineEnding)
    {
        // Act
        var content = await ReadAsync(Encoding.UTF8.GetBytes(text));

        // Assert
        content.Lines
            .Should().Equal(expectedLines);

        content.HasFinalLineEnding
            .Should().Be(expectedFinalLineEnding);
    }

    [Theory]
    [InlineData("a\n", new[] { "a" }, true)]
    [InlineData("\nb", new[] { "", "b" }, false)]
    [InlineData("", new string[0], false)]
    public async Task ReadAsync_FileWithBom_RecordsBomAndExcludesItFromFirstLine(string text, string[] expectedLines,
        bool expectedFinalLineEnding)
    {
        // Act
        var content = await ReadAsync([.. TestText.Utf8Bom, .. Encoding.UTF8.GetBytes(text)]);

        // Assert
        content.HasUtf8Bom
            .Should().BeTrue();

        content.Lines
            .Should().Equal(expectedLines);

        content.HasFinalLineEnding
            .Should().Be(expectedFinalLineEnding);
    }

    [Theory]
    [InlineData("a\nb\n", new[] { "a", "b" }, true)]
    [InlineData("a\r\nb", new[] { "a", "b" }, false)]
    [InlineData("a\rb", new[] { "a\rb" }, false)]
    [InlineData("\n", new[] { "" }, true)]
    [InlineData("", new string[0], false)]
    public void FromText_Text_SplitsLinesWithoutBom(string text, string[] expectedLines,
        bool expectedFinalLineEnding)
    {
        // Act
        var content = TextFileContent.FromText(text);

        // Assert
        content.Lines
            .Should().Equal(expectedLines);

        content.HasFinalLineEnding
            .Should().Be(expectedFinalLineEnding);

        content.HasUtf8Bom
            .Should().BeFalse();
    }

    [Fact]
    public void FromText_TextStartsWithBomCharacter_KeepsCharacterInFirstLine()
    {
        // Act
        var content = TextFileContent.FromText("﻿a\n");

        // Assert
        content.HasUtf8Bom
            .Should().BeFalse();

        content.Lines
            .Should().Equal("﻿a");
    }

    [Fact]
    public void FromText_NullText_ThrowsArgumentNullException()
    {
        // Act
        var act = () => TextFileContent.FromText(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("a\nb", new[] { "a", "b" })]
    [InlineData("a\r\nb", new[] { "a", "b" })]
    [InlineData("a", new[] { "a" })]
    [InlineData("a\n", new[] { "a", "" })]
    [InlineData("", new[] { "" })]
    public void CreateNew_Block_ReturnsBlockLinesWithFinalLineEndingAndWithoutBom(string block,
        string[] expectedLines)
    {
        // Act
        var content = TextFileContent.CreateNew(block);

        // Assert
        content.Lines
            .Should().Equal(expectedLines);

        content.HasFinalLineEnding
            .Should().BeTrue();

        content.HasUtf8Bom
            .Should().BeFalse();
    }

    [Fact]
    public void CreateNew_NullBlock_ThrowsArgumentNullException()
    {
        // Act
        var act = () => TextFileContent.CreateNew(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("", "x\ny\n")]
    [InlineData("a\n", "a\n\nx\ny\n")]
    [InlineData("a", "a\n\nx\ny\n")]
    [InlineData("a\r\n", "a\n\nx\ny\n")]
    [InlineData("a\r\nb", "a\nb\n\nx\ny\n")]
    [InlineData("a\n\n", "a\n\nx\ny\n")]
    [InlineData("a\r\n\r\n", "a\n\nx\ny\n")]
    [InlineData("a\n\n\n", "a\n\n\nx\ny\n")]
    [InlineData("\n", "\nx\ny\n")]
    [InlineData("a\n \n", "a\n \n\nx\ny\n")]
    [InlineData("a\r", "a\r\n\nx\ny\n")]
    public async Task AppendBlock_FileContent_AppendsBlockAfterSeparator(string text, string expectedLfText)
    {
        // Arrange
        var content = await ReadAsync(Encoding.UTF8.GetBytes(text));

        // Act
        var result = content.AppendBlock("x\ny");

        // Assert
        ToLfText(result)
            .Should().Be(expectedLfText);

        result.HasFinalLineEnding
            .Should().BeTrue();
    }

    [Fact]
    public async Task AppendBlock_FileWithOnlyBom_KeepsBomWithoutBlankLine()
    {
        // Arrange
        var content = await ReadAsync(TestText.Utf8Bom);

        // Act
        var result = content.AppendBlock("x");

        // Assert
        result.HasUtf8Bom
            .Should().BeTrue();

        ToLfText(result)
            .Should().Be("x\n");
    }

    [Theory]
    [InlineData("\n", "\nx\n")]
    [InlineData("a\n\n", "a\n\nx\n")]
    [InlineData("a\n", "a\n\nx\n")]
    public async Task AppendBlock_FileWithBom_KeepsBomAndAddsBlankLineOnlyIfMissing(string text, string expectedLfText)
    {
        // Arrange
        var content = await ReadAsync([.. TestText.Utf8Bom, .. Encoding.UTF8.GetBytes(text)]);

        // Act
        var result = content.AppendBlock("x");

        // Assert
        result.HasUtf8Bom
            .Should().BeTrue();

        ToLfText(result)
            .Should().Be(expectedLfText);
    }

    [Fact]
    public async Task AppendBlock_Block_LeavesOriginalContentUnchanged()
    {
        // Arrange
        var content = await ReadAsync("a"u8.ToArray());

        // Act
        _ = content.AppendBlock("x");

        // Assert
        content.Lines
            .Should().Equal("a");

        content.HasFinalLineEnding
            .Should().BeFalse();
    }

    [Fact]
    public void AppendBlock_NullBlock_ThrowsArgumentNullException()
    {
        // Arrange
        var content = TextFileContent.FromText("a\n");

        // Act
        var act = () => content.AppendBlock(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ReplaceLines_CrLfFile_ReplacesLinesAndKeepsOtherLines()
    {
        // Arrange
        var content = await ReadAsync("a\r\nb\r\nc\r\nd\r\n"u8.ToArray());

        // Act
        var result = content.ReplaceLines(1, 2, "x\ny");

        // Assert
        result.Lines
            .Should().Equal("a", "x", "y", "d");

        result.HasFinalLineEnding
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0, new[] { "x", "b", "c" })]
    [InlineData(2, 2, new[] { "a", "b", "x" })]
    [InlineData(0, 2, new[] { "x" })]
    [InlineData(1, 1, new[] { "a", "x", "c" })]
    public void ReplaceLines_Range_ReplacesExactlyTheRange(int firstLineIndex, int lastLineIndex,
        string[] expectedLines)
    {
        // Arrange
        var content = TextFileContent.FromText("a\nb\nc\n");

        // Act
        var result = content.ReplaceLines(firstLineIndex, lastLineIndex, "x");

        // Assert
        result.Lines
            .Should().Equal(expectedLines);
    }

    [Fact]
    public async Task ReplaceLines_LastLineWithoutLineEnding_KeepsMissingFinalLineEnding()
    {
        // Arrange
        var content = await ReadAsync("a\nb"u8.ToArray());

        // Act
        var result = content.ReplaceLines(1, 1, "x");

        // Assert
        result.Lines
            .Should().Equal("a", "x");

        result.HasFinalLineEnding
            .Should().BeFalse();
    }

    [Fact]
    public async Task ReplaceLines_FileWithBom_KeepsBom()
    {
        // Arrange
        var content = await ReadAsync([.. TestText.Utf8Bom, .. "a\nb\n"u8]);

        // Act
        var result = content.ReplaceLines(0, 0, "x");

        // Assert
        result.HasUtf8Bom
            .Should().BeTrue();

        result.Lines
            .Should().Equal("x", "b");
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 3)]
    [InlineData(2, 1)]
    public void ReplaceLines_IndexesOutOfRange_ThrowsArgumentOutOfRangeException(int firstLineIndex,
        int lastLineIndex)
    {
        // Arrange
        var content = TextFileContent.FromText("a\nb\nc\n");

        // Act
        var act = () => content.ReplaceLines(firstLineIndex, lastLineIndex, "x");

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReplaceLines_NullBlock_ThrowsArgumentNullException()
    {
        // Arrange
        var content = TextFileContent.FromText("a\n");

        // Act
        var act = () => content.ReplaceLines(0, 0, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("a\nb\n", true)]
    [InlineData("a\r\nb\r\n", true)]
    [InlineData("a\nb", false)]
    [InlineData("a\nc\n", false)]
    [InlineData("a\nb\n\n", false)]
    [InlineData("a\rb\n", false)]
    public async Task HasSameContent_OtherContent_IgnoresOnlyLineEndings(string otherText, bool expected)
    {
        // Arrange
        var content = await ReadAsync("a\nb\n"u8.ToArray());
        var other = await ReadAsync(Encoding.UTF8.GetBytes(otherText));

        // Act
        var hasSameContent = content.HasSameContent(other);

        // Assert
        hasSameContent
            .Should().Be(expected);
    }

    [Fact]
    public async Task HasSameContent_OnlyBomDiffers_ReturnsFalse()
    {
        // Arrange
        var content = await ReadAsync("a\n"u8.ToArray());
        var other = await ReadAsync([.. TestText.Utf8Bom, .. "a\n"u8]);

        // Act
        var hasSameContent = content.HasSameContent(other);

        // Assert
        hasSameContent
            .Should().BeFalse();
    }

    [Fact]
    public void HasSameContent_BothEmpty_ReturnsTrue()
    {
        // Arrange
        var content = TextFileContent.FromText(string.Empty);

        // Act
        var hasSameContent = content.HasSameContent(TextFileContent.FromText(string.Empty));

        // Assert
        hasSameContent
            .Should().BeTrue();
    }

    [Fact]
    public void HasSameContent_NullOther_ThrowsArgumentNullException()
    {
        // Arrange
        var content = TextFileContent.FromText("a\n");

        // Act
        var act = () => content.HasSameContent(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task ReadAsync_FileCannotBeRead_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("text\n", FileName);
        using var unreadableScope = UnixFileModeScope.MakeUnreadable(filePath);

        // Act
        var act = () => TextFileContent.ReadAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ReadAsync_CanceledTokenAndMissingFile_ThrowsOperationCanceledException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();

        // Act
        var act = () => TextFileContent.ReadAsync(Path.Combine(tempDirectory.RootPath, FileName),
            new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void AppendBlock_EmptyBlock_AppendsSeparatorAndOneEmptyLine()
    {
        // Arrange
        var content = TextFileContent.FromText("a\n");

        // Act
        var result = content.AppendBlock(string.Empty);

        // Assert
        ToLfText(result)
            .Should().Be("a\n\n\n");
    }

    [Fact]
    public void ReplaceLines_EmptyContent_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var content = TextFileContent.FromText(string.Empty);

        // Act
        var act = () => content.ReplaceLines(0, 0, "x");

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReplaceLines_EmptyBlock_ReplacesRangeWithOneEmptyLine()
    {
        // Arrange
        var content = TextFileContent.FromText("a\nb\nc\n");

        // Act
        var result = content.ReplaceLines(1, 1, string.Empty);

        // Assert
        result.Lines
            .Should().Equal("a", "", "c");

        result.HasFinalLineEnding
            .Should().BeTrue();
    }

    [Fact]
    public async Task ReplaceLines_Block_LeavesOriginalContentUnchanged()
    {
        // Arrange
        var content = await ReadAsync([.. TestText.Utf8Bom, .. "a\nb"u8]);

        // Act
        _ = content.ReplaceLines(0, 1, "x");

        // Assert
        content.Lines
            .Should().Equal("a", "b");

        content.HasUtf8Bom
            .Should().BeTrue();

        content.HasFinalLineEnding
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("\u00E9\n", "e\u0301\n")]
    [InlineData("a \n", "a\n")]
    [InlineData("A\n", "a\n")]
    public void HasSameContent_LinesDifferOnlyOrdinally_ReturnsFalse(string text, string otherText)
    {
        // Arrange
        var content = TextFileContent.FromText(text);

        // Act
        var hasSameContent = content.HasSameContent(TextFileContent.FromText(otherText));

        // Assert
        hasSameContent
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("a\r\nb\n", LineEnding.Lf, "a\nb\n")]
    [InlineData("a\r\nb\n", LineEnding.CrLf, "a\r\nb\r\n")]
    [InlineData("a\nb", LineEnding.CrLf, "a\r\nb")]
    [InlineData("a\rb\n\n", LineEnding.CrLf, "a\rb\r\n\r\n")]
    [InlineData("\n", LineEnding.CrLf, "\r\n")]
    [InlineData("", LineEnding.CrLf, "")]
    public void ToText_LineEnding_JoinsLinesAndEndsLastLineIfFinalLineEnding(string text, LineEnding lineEnding,
        string expectedText)
    {
        // Arrange
        var content = TextFileContent.FromText(text);

        // Act
        var result = content.ToText(lineEnding);

        // Assert
        result
            .Should().Be(expectedText);
    }

    [Fact]
    public async Task ToText_ContentWithBom_DoesNotContainBom()
    {
        // Arrange
        var content = await ReadAsync([0xEF, 0xBB, 0xBF, (byte)'a', (byte)'\n']);

        // Act
        var result = content.ToText(LineEnding.Lf);

        // Assert
        result
            .Should().Be("a\n");
    }

    [Theory]
    [MemberData(nameof(UnsupportedEncodingFiles))]
    public async Task ReadAsync_FileNotUtf8WithExceptionFactory_ThrowsExceptionOfFactory(byte[] bytes,
        string encodingName)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(bytes, FileName);

        // Act
        var act = () => TextFileContent.ReadAsync(filePath,
            (reason, innerException) => new InvalidOperationException(reason, innerException),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowExactlyAsync<InvalidOperationException>(encodingName))
            .Which.Message.Should().EndWith("Save the file as UTF-8 and run the command again.");
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, (byte)'a', 0x00 }, false)]
    [InlineData(new byte[] { (byte)'a', 0x00 }, false)]
    [InlineData(new byte[] { (byte)'a', 0xC3, 0x28 }, true)]
    public async Task ReadAsync_FileNotUtf8WithExceptionFactory_PassesDecoderExceptionOnlyForInvalidUtf8(
        byte[] bytes, bool expectsDecoderException)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(bytes, FileName);
        Exception? passedInnerException = null;

        // Act
        var act = () => TextFileContent.ReadAsync(filePath, (reason, innerException) =>
        {
            passedInnerException = innerException;

            return new InvalidOperationException(reason, innerException);
        }, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidOperationException>();

        (passedInnerException is DecoderFallbackException)
            .Should().Be(expectsDecoderException);

        (passedInnerException is null)
            .Should().Be(!expectsDecoderException);
    }

    [Fact]
    public async Task ReadAsync_MissingFileWithExceptionFactory_ReturnsNull()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();

        // Act
        var content = await TextFileContent.ReadAsync(Path.Combine(tempDirectory.RootPath, FileName),
            (reason, _) => new InvalidOperationException(reason), TestContext.Current.CancellationToken);

        // Assert
        content
            .Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_Utf8FileWithExceptionFactory_ReturnsContentWithoutCallingFactory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes([0xEF, 0xBB, 0xBF, (byte)'a', (byte)'\r', (byte)'\n'], FileName);
        var factoryCalled = false;

        // Act
        var content = await TextFileContent.ReadAsync(filePath, (reason, _) =>
        {
            factoryCalled = true;

            return new InvalidOperationException(reason);
        }, TestContext.Current.CancellationToken);

        // Assert
        content.Should().NotBeNull();

        content.Lines
            .Should().Equal("a");

        content.HasUtf8Bom
            .Should().BeTrue();

        factoryCalled
            .Should().BeFalse();
    }

    /// <summary>
    /// Renders the lines of the content with <c>\n</c> as line ending, as a writer with <see cref="LineEnding.Lf" />
    /// does, without the byte order mark.
    /// </summary>
    private static string ToLfText(TextFileContent content)
    {
        return string.Join('\n', content.Lines) + (content.HasFinalLineEnding ? "\n" : string.Empty);
    }

    private static async Task<TextFileContent> ReadAsync(byte[] bytes)
    {
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(bytes, FileName);

        var content = await TextFileContent.ReadAsync(filePath, TestContext.Current.CancellationToken);

        return content ?? throw new InvalidOperationException("File content is null");
    }
}
