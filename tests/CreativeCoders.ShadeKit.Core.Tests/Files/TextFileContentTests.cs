using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

public class TextFileContentTests
{
    private const string FileName = "notes.txt";

    private const string UnmarkedWideEncodingName = "UTF-16 or UTF-32 without byte order mark";

    public static TheoryData<byte[], string> UnsupportedEncodingFiles { get; } = new()
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
        { [0x00], UnmarkedWideEncodingName }
    };

    public static TheoryData<byte[]> AcceptedPrefixFiles { get; } = new()
    {
        new byte[] { 0xFF },
        new byte[] { 0xFE },
        new byte[] { 0xFF, 0xC3, (byte)'\n' },
        new byte[] { 0xFE, 0xFE, 0xFF },
        new byte[] { 0xEF, 0xBB, 0xBF, 0xFF, 0xFE, (byte)'\n' },
        Encoding.UTF8.GetBytes("# Grüße 漢字\n")
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
    public async Task ReadAsync_FileInUtf16OrUtf32_ThrowsUnsupportedFileEncodingException(byte[] bytes,
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

    [Theory]
    [MemberData(nameof(AcceptedPrefixFiles))]
    public async Task ReadAsync_FileWithoutUtf16OrUtf32BomOrNulByte_ReturnsContent(byte[] bytes)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(bytes, FileName);

        // Act
        var content = await TextFileContent.ReadAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        content
            .Should().NotBeNull();

        content.HasBytes(bytes)
            .Should().BeTrue();
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

    [Fact]
    public async Task Lines_LfFile_ReturnsLinesWithoutTerminators()
    {
        // Arrange
        var content = await ReadAsync("a\n\n b \n");

        // Act
        var lines = content.Lines;

        // Assert
        lines.Select(line => line.Text)
            .Should().Equal("a", "", " b ");

        lines.Select(line => (line.Start, line.ContentEnd))
            .Should().Equal((0, 1), (2, 2), (3, 6));
    }

    [Fact]
    public async Task Lines_CrLfFileWithoutFinalNewline_ExcludesCrAndKeepsLastLine()
    {
        // Arrange
        var content = await ReadAsync("a\r\nb");

        // Act
        var lines = content.Lines;

        // Assert
        lines.Select(line => line.Text)
            .Should().Equal("a", "b");

        lines.Select(line => (line.Start, line.ContentEnd))
            .Should().Equal((0, 1), (3, 4));
    }

    [Fact]
    public async Task Lines_FileWithBom_FirstLineStartsAfterBom()
    {
        // Arrange
        var content = await ReadAsync([.. TestText.Utf8Bom, .. "a\n"u8]);

        // Act
        var lines = content.Lines;

        // Assert
        lines
            .Should().Equal(new TextFileLine("a", 3, 4));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 3)]
    public async Task Lines_FileStartsWithNewline_FirstLineIsEmpty(bool withBom, int expectedStart)
    {
        // Arrange
        byte[] prefix = withBom ? TestText.Utf8Bom : [];
        var content = await ReadAsync([.. prefix, .. "\nb"u8]);

        // Act
        var lines = content.Lines;

        // Assert
        lines
            .Should().Equal(new TextFileLine("", expectedStart, expectedStart),
                new TextFileLine("b", expectedStart + 1, expectedStart + 2));
    }

    [Fact]
    public async Task Lines_BlankCrLfLine_IsEmptyWithoutCr()
    {
        // Arrange
        var content = await ReadAsync("a\r\n\r\nb");

        // Act
        var lines = content.Lines;

        // Assert
        lines
            .Should().Equal(new TextFileLine("a", 0, 1), new TextFileLine("", 3, 3), new TextFileLine("b", 5, 6));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lines_EmptyFile_IsEmpty(bool withBom)
    {
        // Arrange
        var content = await ReadAsync(withBom ? TestText.Utf8Bom : []);

        // Act
        var lines = content.Lines;

        // Assert
        lines
            .Should().BeEmpty();
    }

    [Fact]
    public async Task AppendBlock_InvalidUtf8Content_KeepsExistingBytes()
    {
        // Arrange
        byte[] original = [0xFF, 0xC3, (byte)'\n'];
        var content = await ReadAsync(original);

        // Act
        var bytes = content.AppendBlock("x");

        // Assert
        bytes
            .Should().Equal([.. original, .. "\nx\n"u8]);
    }

    [Fact]
    public async Task HasBytes_EmptyFileAndEmptyBytes_ReturnsTrue()
    {
        // Arrange
        var content = await ReadAsync([]);

        // Act
        var hasBytes = content.HasBytes([]);

        // Assert
        hasBytes
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("a\nb\n", "\n")]
    [InlineData("a\r\nb\r\n", "\r\n")]
    [InlineData("a\nb\r\n", "\r\n")]
    [InlineData("", "\n")]
    public async Task NewLine_FileContent_IsCrLfOnlyWhenFileContainsCrLf(string text, string expectedNewLine)
    {
        // Arrange
        var content = await ReadAsync(text);

        // Act
        var newLine = content.NewLine;

        // Assert
        newLine
            .Should().Be(expectedNewLine);
    }

    [Fact]
    public void CreateNew_Block_ReturnsUtf8WithoutBomAndFinalLf()
    {
        // Act
        var bytes = TextFileContent.CreateNew("a\nb");

        // Assert
        bytes
            .Should().Equal("a\nb\n"u8.ToArray());
    }

    [Theory]
    [InlineData("", "x\ny\n")]
    [InlineData("a\n", "a\n\nx\ny\n")]
    [InlineData("a", "a\n\nx\ny\n")]
    [InlineData("a\r\n", "a\r\n\r\nx\r\ny\r\n")]
    [InlineData("a\r\nb", "a\r\nb\r\n\r\nx\r\ny\r\n")]
    [InlineData("a\n\n", "a\n\nx\ny\n")]
    [InlineData("a\r\n\r\n", "a\r\n\r\nx\r\ny\r\n")]
    [InlineData("a\n\n\n", "a\n\n\nx\ny\n")]
    [InlineData("\n", "\nx\ny\n")]
    [InlineData("a\n \n", "a\n \n\nx\ny\n")]
    public async Task AppendBlock_FileContent_AppendsBlockAfterSeparator(string text, string expectedText)
    {
        // Arrange
        var content = await ReadAsync(text);

        // Act
        var bytes = content.AppendBlock("x\ny");

        // Assert
        Encoding.UTF8.GetString(bytes)
            .Should().Be(expectedText);
    }

    [Fact]
    public async Task AppendBlock_FileWithOnlyBom_KeepsBomWithoutBlankLine()
    {
        // Arrange
        var content = await ReadAsync(TestText.Utf8Bom);

        // Act
        var bytes = content.AppendBlock("x");

        // Assert
        bytes
            .Should().Equal([.. TestText.Utf8Bom, .. "x\n"u8]);
    }

    [Theory]
    [InlineData("\n", "\nx\n")]
    [InlineData("a\n\n", "a\n\nx\n")]
    [InlineData("a\n", "a\n\nx\n")]
    public async Task AppendBlock_FileWithBom_KeepsBomAndAddsBlankLineOnlyIfMissing(string text, string expectedText)
    {
        // Arrange
        var content = await ReadAsync([.. TestText.Utf8Bom, .. Encoding.UTF8.GetBytes(text)]);

        // Act
        var bytes = content.AppendBlock("x");

        // Assert
        bytes
            .Should().Equal([.. TestText.Utf8Bom, .. Encoding.UTF8.GetBytes(expectedText)]);
    }

    [Fact]
    public async Task ReplaceLines_CrLfFile_ReplacesLinesAndKeepsTerminatorOfLastLine()
    {
        // Arrange
        var content = await ReadAsync("a\r\nb\r\nc\r\nd\r\n");

        // Act
        var bytes = content.ReplaceLines(content.Lines[1], content.Lines[2], "x\ny");

        // Assert
        Encoding.UTF8.GetString(bytes)
            .Should().Be("a\r\nx\r\ny\r\nd\r\n");
    }

    [Fact]
    public async Task ReplaceLines_LastLineWithoutTerminator_EndsWithoutTerminator()
    {
        // Arrange
        var content = await ReadAsync("a\nb");

        // Act
        var bytes = content.ReplaceLines(content.Lines[1], content.Lines[1], "x");

        // Assert
        Encoding.UTF8.GetString(bytes)
            .Should().Be("a\nx");
    }

    [Fact]
    public async Task ReplaceLines_InvalidUtf8OutsideReplacedLines_KeepsBytes()
    {
        // Arrange
        byte[] original = [0xFF, 0xC3, (byte)'\n', (byte)'b', (byte)'\n', 0xC3, (byte)'\n'];
        var content = await ReadAsync(original);

        // Act
        var bytes = content.ReplaceLines(content.Lines[1], content.Lines[1], "x");

        // Assert
        bytes
            .Should().Equal(0xFF, 0xC3, (byte)'\n', (byte)'x', (byte)'\n', 0xC3, (byte)'\n');
    }

    [Theory]
    [InlineData("a\n", true)]
    [InlineData("a", false)]
    [InlineData("a\r\n", false)]
    public async Task HasBytes_Bytes_ReturnsWhetherFileConsistsOfThem(string text, bool expected)
    {
        // Arrange
        var content = await ReadAsync("a\n");

        // Act
        var hasBytes = content.HasBytes(Encoding.UTF8.GetBytes(text));

        // Assert
        hasBytes
            .Should().Be(expected);
    }

    private static Task<TextFileContent> ReadAsync(string text)
    {
        return ReadAsync(Encoding.UTF8.GetBytes(text));
    }

    private static async Task<TextFileContent> ReadAsync(byte[] bytes)
    {
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(bytes, FileName);

        var content = await TextFileContent.ReadAsync(filePath, TestContext.Current.CancellationToken);

        return content ?? throw new InvalidOperationException("File content is null");
    }
}
