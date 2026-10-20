using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Init;

public class GitIgnorePlannerTests
{
    private const string FileName = ".gitignore";

    private const string Comment = "# SHADE kit: local-only files";

    private const string LocalEntry = ".shadekit/local.json";

    private const string CurrentFeatureEntry = ".shadekit/features/current-feature.json";

    [Fact]
    [Trait("Requirement", "FP02-AC4")]
    public async Task PlanAsync_FileMissing_CreatesFileWithCommentAndBothEntries()
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
            .Should().Equal(Encoding.UTF8.GetBytes($"{Comment}\n{LocalEntry}\n{CurrentFeatureEntry}\n"));
    }

    [Theory]
    [Trait("Requirement", "FP02-AC4")]
    [InlineData("bin/\n.shadekit/local.json\n.shadekit/features/current-feature.json\n")]
    [InlineData("/.shadekit/local.json\n/.shadekit/features/current-feature.json\n")]
    [InlineData(".shadekit/local.json  \n/.shadekit/features/current-feature.json \n")]
    [InlineData(".shadekit/features/current-feature.json\r\n.shadekit/local.json")]
    [InlineData(".shadekit/local.json\r\n/.shadekit/features/current-feature.json\r\n")]
    [InlineData(".shadekit/local.json   \r\n.shadekit/features/current-feature.json \r\n")]
    [InlineData(".shadekit/features/current-feature.json\n.shadekit/local.json  ")]
    public async Task PlanAsync_BothEntriesPresent_ReturnsNullAndLeavesFileUnchanged(string text)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
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
    public async Task PlanAsync_OneEntryMissing_AppendsOnlyMissingEntryUnderComment()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("bin/\n/.shadekit/local.json\n", FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"bin/\n/.shadekit/local.json\n\n{Comment}\n{CurrentFeatureEntry}\n");
    }

    [Fact]
    public async Task PlanAsync_NoEntryPresent_AppendsBothEntriesAfterBlankLine()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("bin/\nobj/\n", FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"bin/\nobj/\n\n{Comment}\n{LocalEntry}\n{CurrentFeatureEntry}\n");
    }

    [Theory]
    [InlineData("# .shadekit/local.json\n")]
    [InlineData(".shadekit/local.json.bak\n")]
    [InlineData("!.shadekit/local.json\n")]
    [InlineData(".shadekit/\n")]
    [InlineData(".shadekit/local.json\\ \n")]
    [InlineData(".ShadeKit/Local.json\n")]
    public async Task PlanAsync_LineSimilarToEntry_DoesNotCountAsEntry(string text)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile(text, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"{text}\n{Comment}\n{LocalEntry}\n{CurrentFeatureEntry}\n");
    }

    [Theory]
    [InlineData("  .shadekit/local.json\n.shadekit/features/current-feature.json\n")]
    [InlineData("\t/.shadekit/local.json\n.shadekit/features/current-feature.json\n")]
    public async Task PlanAsync_EntryWithLeadingWhitespace_DoesNotCountAndAppendsEntry(string text)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile(text, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"{text}\n{Comment}\n{LocalEntry}\n");
    }

    [Theory]
    [InlineData(".shadekit/local.json\t\n.shadekit/features/current-feature.json\n", "\n")]
    [InlineData("/.shadekit/local.json \t\n.shadekit/features/current-feature.json\n", "\n")]
    [InlineData(".shadekit/local.json\t\r\n.shadekit/features/current-feature.json\r\n", "\r\n")]
    public async Task PlanAsync_EntryWithTrailingTab_DoesNotCountAndAppendsEntry(string text, string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile(text, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"{text}{newLine}{Comment}{newLine}{LocalEntry}{newLine}");
    }

    [Fact]
    public async Task PlanAsync_NoTrailingNewline_AddsNewlineBeforeBlankLine()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("bin/", FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be($"bin/\n\n{Comment}\n{LocalEntry}\n{CurrentFeatureEntry}\n");
    }

    [Fact]
    public async Task PlanAsync_EmptyFile_AppendsWithoutBlankLine()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile(string.Empty, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"{Comment}\n{LocalEntry}\n{CurrentFeatureEntry}\n");
    }

    [Fact]
    public async Task PlanAsync_CrLfFile_AppendsWithCrLf()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("bin/\r\nobj/", FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be($"bin/\r\nobj/\r\n\r\n{Comment}\r\n{LocalEntry}\r\n{CurrentFeatureEntry}\r\n");
    }

    [Fact]
    public async Task PlanAsync_FileWithBom_KeepsBom()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes([.. TestText.Utf8Bom, .. "bin/\n"u8], FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([
                .. TestText.Utf8Bom,
                .. Encoding.UTF8.GetBytes($"bin/\n\n{Comment}\n{LocalEntry}\n{CurrentFeatureEntry}\n")
            ]);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC4")]
    public async Task PlanAsync_CalledTwice_ContainsEachEntryExactlyOnce()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("bin/\n", FileName);
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);
        var afterFirstCall = tempDirectory.ReadFileBytes(FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().BeNull();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(afterFirstCall);

        var lines = tempDirectory.ReadFileText(FileName).Split('\n');

        lines.Count(line => line == LocalEntry)
            .Should().Be(1);

        lines.Count(line => line == CurrentFeatureEntry)
            .Should().Be(1);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC4")]
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

    [Fact]
    public async Task PlanAsync_BomFileWithEntryOnFirstLine_CountsEntryAsPresent()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        byte[] original = [.. TestText.Utf8Bom, .. Encoding.UTF8.GetBytes($"{LocalEntry}\n")];
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([.. original, .. Encoding.UTF8.GetBytes($"\n{Comment}\n{CurrentFeatureEntry}\n")]);
    }

    [Fact]
    public async Task PlanAsync_FirstEntryMissing_AppendsOnlyFirstEntry()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile($"{CurrentFeatureEntry}\n", FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be($"{CurrentFeatureEntry}\n\n{Comment}\n{LocalEntry}\n");
    }

    [Fact]
    public async Task PlanAsync_CommentPresentButEntryRemoved_AppendsEntryUnderNewComment()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile($"{Comment}\n{LocalEntry}\n", FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be($"{Comment}\n{LocalEntry}\n\n{Comment}\n{CurrentFeatureEntry}\n");
    }

    [Fact]
    public async Task PlanAsync_FileWithOnlyNewline_AppendsWithoutAnotherBlankLine()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("\n", FileName);

        // Act
        await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be($"\n{Comment}\n{LocalEntry}\n{CurrentFeatureEntry}\n");
    }

    [Theory]
    [InlineData("bin/\n\n", "\n")]
    [InlineData("bin/\r\n\r\n", "\r\n")]
    public async Task PlanAsync_FileEndsWithBlankLine_AppendsWithoutAnotherBlankLine(string text,
        string newLine)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile(text, FileName);

        // Act
        var action = await PlanAndWriteAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        action
            .Should().Be(InitializedFileAction.Appended);

        tempDirectory.ReadFileText(FileName)
            .Should().Be($"{text}{Comment}{newLine}{LocalEntry}{newLine}{CurrentFeatureEntry}{newLine}");
    }

    [Fact]
    public async Task PlanAsync_Utf16File_ThrowsUnsupportedFileEncodingExceptionAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        byte[] original = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("bin/\r\n")];
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new GitIgnorePlanner().PlanAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UnsupportedFileEncodingException>()).Which;

        exception.FilePath
            .Should().Be(filePath);

        exception.EncodingName
            .Should().Be("UTF-16 LE");

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
        var act = () => new GitIgnorePlanner().PlanAsync(directoryPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<PathNotAFileException>())
            .Which.Path.Should().Be(directoryPath);

        Directory.Exists(directoryPath)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("bin/\n")]
    [InlineData("")]
    public async Task PlanAsync_Utf8File_DoesNotThrowAndLeavesFileUnchanged(string text)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = Encoding.UTF8.GetBytes(text);
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new GitIgnorePlanner().PlanAsync(filePath, TestContext.Current.CancellationToken);

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
        var act = () => new GitIgnorePlanner().PlanAsync(filePath, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();

        File.Exists(filePath)
            .Should().BeFalse();
    }

    [Fact]
    public async Task PlanAsync_CanceledTokenWithExistingFile_ThrowsAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var original = "bin/\n"u8.ToArray();
        var filePath = tempDirectory.WriteFileBytes(original, FileName);

        // Act
        var act = () => new GitIgnorePlanner().PlanAsync(filePath, new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(original);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(".gitignore")]
    public async Task PlanAsync_PathNotAbsolute_ThrowsArgumentException(string? filePath)
    {
        // Act
        var act = () => new GitIgnorePlanner().PlanAsync(filePath!, TestContext.Current.CancellationToken);

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
        var act = () => new GitIgnorePlanner().PlanAsync(filePath, new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        File.Exists(filePath)
            .Should().BeFalse();
    }

    /// <summary>
    /// Plans the change of a <c>.gitignore</c> file and writes it, as the initialisation does.
    /// </summary>
    /// <returns>The action of the planned change, or <see langword="null" /> if the file needs no change.</returns>
    private static async Task<InitializedFileAction?> PlanAndWriteAsync(string filePath,
        CancellationToken cancellationToken)
    {
        var change = await new GitIgnorePlanner().PlanAsync(filePath, cancellationToken);

        if (change is not null)
        {
            await File.WriteAllBytesAsync(filePath, change.Content, cancellationToken);
        }

        return change?.Action;
    }
}
