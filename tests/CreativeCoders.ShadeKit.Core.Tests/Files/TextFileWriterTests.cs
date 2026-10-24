using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

public class TextFileWriterTests
{
    private const string FileName = "notes.txt";

    [Theory]
    [InlineData(LineEnding.Lf, "a\nb\n")]
    [InlineData(LineEnding.CrLf, "a\r\nb\r\n")]
    public async Task WriteAsync_ContentWithFinalLineEnding_WritesLinesWithResolvedLineEnding(LineEnding lineEnding,
        string expectedText)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var writer = FixedLineEnding.CreateWriter(lineEnding);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("a\nb\n"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(Encoding.UTF8.GetBytes(expectedText));
    }

    [Theory]
    [InlineData(LineEnding.Lf, "a\nb")]
    [InlineData(LineEnding.CrLf, "a\r\nb")]
    public async Task WriteAsync_ContentWithoutFinalLineEnding_WritesNoFinalLineEnding(LineEnding lineEnding,
        string expectedText)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var writer = FixedLineEnding.CreateWriter(lineEnding);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("a\r\nb"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(Encoding.UTF8.GetBytes(expectedText));
    }

    [Theory]
    [InlineData(LineEnding.Lf, "a\nb\n")]
    [InlineData(LineEnding.CrLf, "a\r\nb\r\n")]
    public async Task WriteAsync_MixedLineEndingsRead_NormalisesWholeFile(LineEnding lineEnding, string expectedText)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("a\r\nb\n", FileName);
        var content = await TextFileContent.ReadAsync(filePath, TestContext.Current.CancellationToken);
        var writer = FixedLineEnding.CreateWriter(lineEnding);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, FileName), content!, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(Encoding.UTF8.GetBytes(expectedText));
    }

    [Theory]
    [InlineData(LineEnding.Lf, "a\n")]
    [InlineData(LineEnding.CrLf, "a\r\n")]
    public async Task WriteAsync_ContentWithBom_WritesBomBeforeText(LineEnding lineEnding, string expectedText)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes([.. TestText.Utf8Bom, .. "a\n"u8], FileName);
        var content = await TextFileContent.ReadAsync(filePath, TestContext.Current.CancellationToken);
        var writer = FixedLineEnding.CreateWriter(lineEnding);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, FileName), content!, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal([.. TestText.Utf8Bom, .. Encoding.UTF8.GetBytes(expectedText)]);
    }

    [Theory]
    [InlineData(false, new byte[0])]
    [InlineData(true, new byte[] { 0xEF, 0xBB, 0xBF })]
    public async Task WriteAsync_EmptyContent_WritesOnlyBomIfPresent(bool withBom, byte[] expectedBytes)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFileBytes(withBom ? TestText.Utf8Bom : [], FileName);
        var content = await TextFileContent.ReadAsync(filePath, TestContext.Current.CancellationToken);
        var writer = FixedLineEnding.CreateWriter(LineEnding.CrLf);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, FileName), content!, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(expectedBytes);
    }

    [Fact]
    public async Task WriteAsync_NonAsciiAndLoneCarriageReturn_WritesUtf8WithoutBomAndReplacesCarriageReturn()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var writer = FixedLineEnding.CreateWriter(LineEnding.Lf);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("Grüße\rä\n"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(Encoding.UTF8.GetBytes("Grüße\nä\n"));
    }

    [Fact]
    public async Task WriteAsync_ExistingFile_OverwritesIt()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("old content that is longer\n", FileName);
        var writer = FixedLineEnding.CreateWriter(LineEnding.Lf);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("new\n"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(FileName)
            .Should().Be("new\n");
    }

    [Fact]
    public async Task WriteAsync_ParentDirectoryMissing_CreatesIt()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var writer = FixedLineEnding.CreateWriter(LineEnding.Lf);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, "docs/sub/notes.md"), TextFileContent.FromText("a\n"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText("docs", "sub", "notes.md")
            .Should().Be("a\n");
    }

    [Fact]
    public async Task WriteAsync_ResolverReturnsWarnings_ReturnsThem()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var warning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        var writer = FixedLineEnding.CreateWriter(LineEnding.Lf, warning);

        // Act
        var warnings = await writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("a\n"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        warnings
            .Should().Equal(warning);
    }

    [Fact]
    public async Task WriteAsync_ResolverReturnsNoWarnings_ReturnsEmpty()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var writer = FixedLineEnding.CreateWriter(LineEnding.Lf);

        // Act
        var warnings = await writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("a\n"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task WriteAsync_Paths_ResolvesLineEndingForSamePathsAndToken()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var resolver = FixedLineEnding.CreateResolver(LineEnding.Lf);
        var writer = new TextFileWriter(resolver);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, "docs/notes.md"), TextFileContent.FromText("a\n"),
            cancellationToken: cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => resolver.GetLineEndingAsync(FileIn(tempDirectory, "docs/notes.md"),
                cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WriteAsync_LineEndingPath_ResolvesLineEndingForLineEndingPathOnly()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var resolver = FixedLineEnding.CreateResolver(LineEnding.Lf);
        var writer = new TextFileWriter(resolver);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, "staging/notes.md"), TextFileContent.FromText("a\n"),
            FileIn(tempDirectory, "final/notes.md"), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => resolver.GetLineEndingAsync(FileIn(tempDirectory, "final/notes.md"),
                TestContext.Current.CancellationToken))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => resolver.GetLineEndingAsync(
                A<RepositoryFile>.That.Matches(file => file.GitPath == "staging/notes.md"), A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(LineEnding.Lf, "a\nb\n")]
    [InlineData(LineEnding.CrLf, "a\r\nb\r\n")]
    public async Task WriteAsync_LineEndingPath_WritesRelativePathWithLineEndingOfLineEndingPath(
        LineEnding finalLineEnding, string expectedText)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var resolver = A.Fake<ILineEndingResolver>();
        A.CallTo(() => resolver.GetLineEndingAsync(
                A<RepositoryFile>.That.Matches(file => file.GitPath == "staging/notes.md"), A<CancellationToken>._))
            .Returns(new LineEndingResolution(
                finalLineEnding == LineEnding.Lf ? LineEnding.CrLf : LineEnding.Lf, []));
        A.CallTo(() => resolver.GetLineEndingAsync(
                A<RepositoryFile>.That.Matches(file => file.GitPath == "final/notes.md"), A<CancellationToken>._))
            .Returns(new LineEndingResolution(finalLineEnding, []));
        var writer = new TextFileWriter(resolver);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, "staging/notes.md"), TextFileContent.FromText("a\nb\n"),
            FileIn(tempDirectory, "final/notes.md"), TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes("staging", "notes.md")
            .Should().Equal(Encoding.UTF8.GetBytes(expectedText));

        File.Exists(Path.Combine(tempDirectory.RootPath, "final", "notes.md"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_NullFile_ThrowsArgumentNullException()
    {
        // Arrange
        var resolver = FixedLineEnding.CreateResolver(LineEnding.Lf);
        var writer = new TextFileWriter(resolver);

        // Act
        var act = () => writer.WriteAsync(null!, TextFileContent.FromText("a\n"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("file");

        A.CallTo(resolver)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task WriteAsync_NullContent_ThrowsArgumentNullExceptionAndWritesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var writer = FixedLineEnding.CreateWriter(LineEnding.Lf);

        // Act
        var act = () => writer.WriteAsync(FileIn(tempDirectory, FileName), null!,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();

        File.Exists(Path.Combine(tempDirectory.RootPath, FileName))
            .Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_CanceledToken_ThrowsAndWritesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var writer = FixedLineEnding.CreateWriter(LineEnding.Lf);

        // Act
        var act = () => writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("a\n"),
            cancellationToken: new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        File.Exists(Path.Combine(tempDirectory.RootPath, FileName))
            .Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_ResolverThrows_PropagatesExceptionAndWritesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var resolver = A.Fake<ILineEndingResolver>();
        A.CallTo(() => resolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());
        var writer = new TextFileWriter(resolver);

        // Act
        var act = () => writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("a\n"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        File.Exists(Path.Combine(tempDirectory.RootPath, FileName))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("\n", "\r\n")]
    [InlineData("a\rb\nc", "a\r\nb\r\nc")]
    [InlineData("\n\n", "\r\n\r\n")]
    [InlineData("a\r", "a\r\n")]
    [InlineData("a\r\rb", "a\r\n\r\nb")]
    public async Task WriteAsync_CrLf_JoinsLinesWithCrLfAndTreatsLoneCarriageReturnAsLineEnding(string text,
        string expectedText)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var writer = FixedLineEnding.CreateWriter(LineEnding.CrLf);

        // Act
        await writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText(text),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(FileName)
            .Should().Equal(Encoding.UTF8.GetBytes(expectedText));
    }

    [Fact]
    public async Task WriteAsync_ResolverReturnsSeveralWarnings_ReturnsAllInOrder()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var firstWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        var secondWarning = new EditorConfigUnreadableWarning("/repo/docs/.editorconfig", "Access denied.");
        var writer = FixedLineEnding.CreateWriter(LineEnding.Lf, firstWarning, secondWarning);

        // Act
        var warnings = await writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("a\n"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        warnings
            .Should().Equal(firstWarning, secondWarning);
    }

    [Fact]
    public async Task WriteAsync_PathIsExistingDirectory_ThrowsAndKeepsDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var directoryPath = tempDirectory.CreateDirectory(FileName);
        var writer = FixedLineEnding.CreateWriter(LineEnding.Lf);

        // Act
        var act = () => writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("a\n"),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<Exception>())
            .Which.Should().Match(exception => exception is IOException || exception is UnauthorizedAccessException);

        Directory.Exists(directoryPath)
            .Should().BeTrue();
    }

    [Fact]
    public async Task WriteAsync_ParentIsExistingFile_ThrowsIOExceptionAndKeepsFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("content\n", FileName);
        var writer = FixedLineEnding.CreateWriter(LineEnding.Lf);

        // Act
        var act = () => writer.WriteAsync(FileIn(tempDirectory, $"{FileName}/sub.md"),
            TextFileContent.FromText("a\n"), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>();

        tempDirectory.ReadFileText(FileName)
            .Should().Be("content\n");
    }

    [Fact]
    public async Task WriteAsync_CanceledWhileResolvingLineEnding_KeepsExistingFile()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("old\n", FileName);
        using var cancellationTokenSource = new CancellationTokenSource();
        var resolver = A.Fake<ILineEndingResolver>();
        A.CallTo(() => resolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Returns(new LineEndingResolution(LineEnding.Lf, []));
        var writer = new TextFileWriter(resolver);

        // Act
        var act = () => writer.WriteAsync(FileIn(tempDirectory, FileName), TextFileContent.FromText("new\n"),
            cancellationToken: cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        tempDirectory.ReadFileText(FileName)
            .Should().Be("old\n");
    }

    [Fact]
    public async Task WriteAsync_CanceledWhileResolvingLineEnding_CreatesNoParentDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        using var cancellationTokenSource = new CancellationTokenSource();
        var resolver = A.Fake<ILineEndingResolver>();
        A.CallTo(() => resolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Returns(new LineEndingResolution(LineEnding.Lf, []));
        var writer = new TextFileWriter(resolver);

        // Act
        var act = () => writer.WriteAsync(FileIn(tempDirectory, "docs/notes.md"), TextFileContent.FromText("a\n"),
            cancellationToken: cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        Directory.Exists(Path.Combine(tempDirectory.RootPath, "docs"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task PrepareAsync_Files_ForwardsThemAndTokenToResolver()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var resolver = FixedLineEnding.CreateResolver(LineEnding.Lf);
        var writer = new TextFileWriter(resolver);
        RepositoryFile[] files = [FileIn(tempDirectory, "a.md"), FileIn(tempDirectory, "docs/b.md")];

        // Act
        await writer.PrepareAsync(files, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => resolver.PrepareAsync(files, TestContext.Current.CancellationToken))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Constructor_NullResolver_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TextFileWriter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static RepositoryFile FileIn(TempKitDirectory tempDirectory, string relativePath)
    {
        return RepositoryFile.Resolve(tempDirectory.RootPath, relativePath);
    }
}
