using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Git.Warnings;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

public class LineEndingResolverTests
{
    private const string RepositoryRootPath = "/repo";

    private const string RelativePath = "docs/readme.md";

    private static readonly string FullPath = Path.GetFullPath(Path.Combine(RepositoryRootPath, "docs", "readme.md"));

    private static readonly RepositoryFile File = RepositoryFile.Resolve(RepositoryRootPath, RelativePath);

    private readonly IGitLineEndingResolver _gitLineEndingResolver = A.Fake<IGitLineEndingResolver>();

    private readonly IEditorConfigLineEndingResolver _editorConfigLineEndingResolver =
        A.Fake<IEditorConfigLineEndingResolver>();

    public LineEndingResolverTests()
    {
        SetUpGitLineEnding(null);
        SetUpEditorConfigLineEnding(null);
    }

    [Fact]
    public void Constructor_NullGitLineEndingResolver_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LineEndingResolver(null!, _editorConfigLineEndingResolver);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitLineEndingResolver");
    }

    [Fact]
    public void Constructor_NullEditorConfigLineEndingResolver_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LineEndingResolver(_gitLineEndingResolver, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("editorConfigLineEndingResolver");
    }

    [Fact]
    public void InternalConstructor_NullGitLineEndingResolver_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LineEndingResolver(null!, _editorConfigLineEndingResolver, LineEnding.Lf);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitLineEndingResolver");
    }

    [Fact]
    public void InternalConstructor_NullEditorConfigLineEndingResolver_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LineEndingResolver(_gitLineEndingResolver, null!, LineEnding.Lf);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("editorConfigLineEndingResolver");
    }

    [Theory]
    [InlineData(LineEnding.Lf, LineEnding.CrLf, LineEnding.CrLf)]
    [InlineData(LineEnding.CrLf, LineEnding.Lf, LineEnding.Lf)]
    public async Task GetLineEndingAsync_GitDecides_ReturnsGitLineEnding(LineEnding gitLineEnding,
        LineEnding editorConfigLineEnding, LineEnding operatingSystemLineEnding)
    {
        // Arrange
        SetUpGitLineEnding(gitLineEnding);
        SetUpEditorConfigLineEnding(editorConfigLineEnding);
        var sut = CreateSut(operatingSystemLineEnding);

        // Act
        var lineEnding = await GetLineEndingAsync(sut);

        // Assert
        lineEnding
            .Should().Be(gitLineEnding);
    }

    [Fact]
    public async Task GetLineEndingAsync_GitDecides_DoesNotReadEditorConfig()
    {
        // Arrange
        SetUpGitLineEnding(LineEnding.CrLf);
        var sut = CreateSut(LineEnding.Lf);

        // Act
        await GetLineEndingAsync(sut);

        // Assert
        A.CallTo(_editorConfigLineEndingResolver)
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(LineEnding.Lf, LineEnding.CrLf)]
    [InlineData(LineEnding.CrLf, LineEnding.Lf)]
    public async Task GetLineEndingAsync_GitDoesNotDecide_ReturnsEditorConfigLineEnding(
        LineEnding editorConfigLineEnding, LineEnding operatingSystemLineEnding)
    {
        // Arrange
        SetUpEditorConfigLineEnding(editorConfigLineEnding);
        var sut = CreateSut(operatingSystemLineEnding);

        // Act
        var lineEnding = await GetLineEndingAsync(sut);

        // Assert
        lineEnding
            .Should().Be(editorConfigLineEnding);
    }

    [Theory]
    [InlineData(LineEnding.Lf)]
    [InlineData(LineEnding.CrLf)]
    public async Task GetLineEndingAsync_NeitherGitNorEditorConfigDecides_ReturnsOperatingSystemLineEnding(
        LineEnding operatingSystemLineEnding)
    {
        // Arrange
        var sut = CreateSut(operatingSystemLineEnding);

        // Act
        var lineEnding = await GetLineEndingAsync(sut);

        // Assert
        lineEnding
            .Should().Be(operatingSystemLineEnding);
    }

    [Theory]
    [InlineData(LineEnding.Lf)]
    [InlineData(LineEnding.CrLf)]
    public async Task GetLineEndingAsync_EditorConfigUnreadable_ReturnsOperatingSystemLineEndingAndWarning(
        LineEnding operatingSystemLineEnding)
    {
        // Arrange
        var warning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access to the path is denied.");
        A.CallTo(() => _editorConfigLineEndingResolver.GetLineEnding(A<string>._))
            .Returns(new EditorConfigLineEndingResult(null, warning));
        var sut = CreateSut(operatingSystemLineEnding);

        // Act
        var resolution = await ResolveAsync(sut);

        // Assert
        resolution.LineEnding
            .Should().Be(operatingSystemLineEnding);

        resolution.Warnings
            .Should().Equal(warning);
    }

    [Theory]
    [InlineData(LineEnding.Lf)]
    [InlineData(LineEnding.CrLf)]
    public async Task GetLineEndingAsync_EditorConfigInvalid_ReturnsOperatingSystemLineEndingAndWarning(
        LineEnding operatingSystemLineEnding)
    {
        // Arrange
        var warning = new EditorConfigInvalidWarning("/repo/.editorconfig",
            "Value was either too large or too small for an Int32.");
        A.CallTo(() => _editorConfigLineEndingResolver.GetLineEnding(A<string>._))
            .Returns(new EditorConfigLineEndingResult(null, warning));
        var sut = CreateSut(operatingSystemLineEnding);

        // Act
        var resolution = await ResolveAsync(sut);

        // Assert
        resolution.LineEnding
            .Should().Be(operatingSystemLineEnding);

        resolution.Warnings
            .Should().Equal(warning);
    }

    [Fact]
    public async Task GetLineEndingAsync_GitDecides_ReturnsNoWarnings()
    {
        // Arrange
        SetUpGitLineEnding(LineEnding.CrLf);
        var sut = CreateSut(LineEnding.Lf);

        // Act
        var resolution = await ResolveAsync(sut);

        // Assert
        resolution.LineEnding
            .Should().Be(LineEnding.CrLf);

        resolution.Warnings
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(LineEnding.CrLf)]
    [InlineData(null)]
    public async Task GetLineEndingAsync_EditorConfigReadable_ReturnsNoWarnings(LineEnding? editorConfigLineEnding)
    {
        // Arrange
        SetUpEditorConfigLineEnding(editorConfigLineEnding);
        var sut = CreateSut(LineEnding.Lf);

        // Act
        var resolution = await ResolveAsync(sut);

        // Assert
        resolution.LineEnding
            .Should().Be(editorConfigLineEnding ?? LineEnding.Lf);

        resolution.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task GetLineEndingAsync_PublicConstructorAndNoSourceDecides_ReturnsLineEndingOfCurrentOperatingSystem()
    {
        // Arrange
        var sut = new LineEndingResolver(_gitLineEndingResolver, _editorConfigLineEndingResolver);

        // Act
        var lineEnding = await GetLineEndingAsync(sut);

        // Assert
        lineEnding
            .Should().Be(OperatingSystem.IsWindows() ? LineEnding.CrLf : LineEnding.Lf);
    }

    [Fact]
    public async Task GetLineEndingAsync_AnyPath_PassesArgumentsAndTokenToGitResolver()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var sut = CreateSut(LineEnding.Lf);

        // Act
        await sut.GetLineEndingAsync(File, cancellationToken);

        // Assert
        A.CallTo(() => _gitLineEndingResolver.GetLineEndingAsync(File, cancellationToken))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PrepareAsync_Files_PreloadsTheirGitAttributesWithToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        RepositoryFile[] files = [File, RepositoryFile.Resolve(RepositoryRootPath, "other.md")];
        var sut = CreateSut(LineEnding.Lf);

        // Act
        await sut.PrepareAsync(files, cancellationToken);

        // Assert
        A.CallTo(() => _gitLineEndingResolver.PreloadAttributesAsync(files, cancellationToken))
            .MustHaveHappenedOnceExactly();

        A.CallTo(_editorConfigLineEndingResolver)
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("docs/readme.md")]
    [InlineData("./docs/readme.md")]
    [InlineData("docs/../docs/readme.md")]
    public async Task GetLineEndingAsync_GitDoesNotDecide_PassesFullPathToEditorConfigResolver(string relativePath)
    {
        // Arrange
        var sut = CreateSut(LineEnding.Lf);

        // Act
        await sut.GetLineEndingAsync(RepositoryFile.Resolve(RepositoryRootPath, relativePath),
            TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _editorConfigLineEndingResolver.GetLineEnding(FullPath))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetLineEndingAsync_RelativePathWithOperatingSystemSeparator_PassesFullPathToEditorConfigResolver()
    {
        // Arrange
        var sut = CreateSut(LineEnding.Lf);

        // Act
        await sut.GetLineEndingAsync(RepositoryFile.Resolve(RepositoryRootPath, Path.Combine("docs", "readme.md")),
            TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _editorConfigLineEndingResolver.GetLineEnding(FullPath))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetLineEndingAsync_CanceledAfterGitDoesNotDecide_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        A.CallTo(() => _gitLineEndingResolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .Invokes(() => cancellationTokenSource.Cancel())
            .Returns(new GitLineEndingResult(null, null));
        var sut = CreateSut(LineEnding.Lf);

        // Act
        var act = () => sut.GetLineEndingAsync(File, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(_editorConfigLineEndingResolver)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetLineEndingAsync_GitResolverCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        A.CallTo(() => _gitLineEndingResolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .Throws(new OperationCanceledException());
        var sut = CreateSut(LineEnding.Lf);

        // Act
        var act = () => GetLineEndingAsync(sut);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetLineEndingAsync_GitResolverThrowsUnexpectedException_PropagatesWithoutReadingEditorConfig()
    {
        // Arrange
        A.CallTo(() => _gitLineEndingResolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("unexpected"));
        var sut = CreateSut(LineEnding.Lf);

        // Act
        var act = () => GetLineEndingAsync(sut);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        A.CallTo(_editorConfigLineEndingResolver)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetLineEndingAsync_EditorConfigResolverThrows_PropagatesException()
    {
        // Arrange
        A.CallTo(() => _editorConfigLineEndingResolver.GetLineEnding(A<string>._))
            .Throws(new IOException("The .editorconfig file cannot be read."));
        var sut = CreateSut(LineEnding.Lf);

        // Act
        var act = () => GetLineEndingAsync(sut);

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task GetLineEndingAsync_NullFile_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut(LineEnding.Lf);

        // Act
        var act = () => sut.GetLineEndingAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("file");

        A.CallTo(_gitLineEndingResolver)
            .MustNotHaveHappened();

        A.CallTo(_editorConfigLineEndingResolver)
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(LineEnding.Lf, LineEnding.CrLf)]
    [InlineData(LineEnding.CrLf, LineEnding.Lf)]
    public async Task GetLineEndingAsync_GitFailsWithWarning_ReturnsEditorConfigLineEndingAndGitWarning(
        LineEnding editorConfigLineEnding, LineEnding operatingSystemLineEnding)
    {
        // Arrange
        var gitWarning = new GitAttributesUnavailableWarning("/repo", "git check-attr",
            "git exited with the unexpected exit code 128.");
        A.CallTo(() => _gitLineEndingResolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .Returns(new GitLineEndingResult(null, gitWarning));
        SetUpEditorConfigLineEnding(editorConfigLineEnding);
        var sut = CreateSut(operatingSystemLineEnding);

        // Act
        var resolution = await ResolveAsync(sut);

        // Assert
        resolution.LineEnding
            .Should().Be(editorConfigLineEnding);

        resolution.Warnings
            .Should().Equal(gitWarning);
    }

    [Fact]
    public async Task GetLineEndingAsync_GitFailsAndEditorConfigUnreadable_ReturnsOperatingSystemLineEndingAndBothWarnings()
    {
        // Arrange
        var gitWarning = new GitAttributesUnavailableWarning("/repo", "git check-attr",
            "git exited with the unexpected exit code 128.");
        var editorConfigWarning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        A.CallTo(() => _gitLineEndingResolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .Returns(new GitLineEndingResult(null, gitWarning));
        A.CallTo(() => _editorConfigLineEndingResolver.GetLineEnding(A<string>._))
            .Returns(new EditorConfigLineEndingResult(null, editorConfigWarning));
        var sut = CreateSut(LineEnding.CrLf);

        // Act
        var resolution = await ResolveAsync(sut);

        // Assert
        resolution.LineEnding
            .Should().Be(LineEnding.CrLf);

        resolution.Warnings
            .Should().Equal(gitWarning, editorConfigWarning);
    }

    private static async Task<LineEnding> GetLineEndingAsync(LineEndingResolver sut)
    {
        var resolution = await ResolveAsync(sut);

        return resolution.LineEnding;
    }

    private static Task<LineEndingResolution> ResolveAsync(LineEndingResolver sut)
    {
        return sut.GetLineEndingAsync(File, TestContext.Current.CancellationToken);
    }

    private LineEndingResolver CreateSut(LineEnding operatingSystemLineEnding)
    {
        return new LineEndingResolver(_gitLineEndingResolver, _editorConfigLineEndingResolver,
            operatingSystemLineEnding);
    }

    private void SetUpGitLineEnding(LineEnding? lineEnding)
    {
        A.CallTo(() => _gitLineEndingResolver.GetLineEndingAsync(A<RepositoryFile>._, A<CancellationToken>._))
            .Returns(new GitLineEndingResult(lineEnding, null));
    }

    private void SetUpEditorConfigLineEnding(LineEnding? lineEnding)
    {
        A.CallTo(() => _editorConfigLineEndingResolver.GetLineEnding(A<string>._))
            .Returns(new EditorConfigLineEndingResult(lineEnding, null));
    }
}
