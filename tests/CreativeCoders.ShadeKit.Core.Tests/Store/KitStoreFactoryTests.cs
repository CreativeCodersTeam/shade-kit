using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

public class KitStoreFactoryTests
{
    [Fact]
    [Trait("Requirement", "FP01-AC1")]
    public void Create_PathIsGitRootWithKitRoot_ReturnsStoreOfShadeKitDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        tempDirectory.CreateDirectory(".shadekit", "features", "billing");

        var factory = CreateFactory();

        // Act
        var kitStore = factory.Create(tempDirectory.RootPath);

        // Assert
        kitStore.RootPath
            .Should().Be(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        kitStore.GetFeatureNames()
            .Should().BeEquivalentTo("auth", "billing");
    }

    [Fact]
    public void Create_FeaturesDirectoryNextToShadeKitDirectory_IgnoresIt()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "inside");
        tempDirectory.CreateDirectory("features", "outside");

        var factory = CreateFactory();

        // Act
        var kitStore = factory.Create(tempDirectory.RootPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("inside");
    }

    [Fact]
    public void Create_ShadeKitBelowRepositoryRoot_IgnoresItAndReturnsStoreOfRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "root");
        tempDirectory.CreateDirectory("inner", ".shadekit", "features", "inner");
        var nestedPath = tempDirectory.CreateDirectory("inner", "src");

        var factory = CreateFactory();

        // Act
        var kitStore = factory.Create(nestedPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().Equal("root");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Create_ShadeKitOnlyBelowRepositoryRoot_ThrowsKitRootPathNotFoundException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory("inner", ".shadekit", "features", "inner");
        var nestedPath = tempDirectory.CreateDirectory("inner", "src");

        var factory = CreateFactory();

        // Act
        var act = () => factory.Create(nestedPath);

        // Assert
        var exception = act.Should().Throw<KitRootPathNotFoundException>().Which;

        exception.StartPath
            .Should().Be(nestedPath);

        exception.GitRootPath
            .Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Create_NestedRepositoryWithoutShadeKit_StopsAtNestedRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit", "features", "outer");
        var submodulePath = tempDirectory.CreateGitRoot("modules", "lib");
        var nestedPath = tempDirectory.CreateDirectory("modules", "lib", "src");

        var factory = CreateFactory();

        // Act
        var act = () => factory.Create(nestedPath);

        // Assert
        act.Should().Throw<KitRootPathNotFoundException>()
            .Which.GitRootPath.Should().Be(submodulePath);
    }

    [Fact]
    public void Create_ShadeKitWithoutFeaturesDirectory_ReturnsStoreWithoutFeatures()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateDirectory(".shadekit");

        var factory = CreateFactory();

        // Act
        var kitStore = factory.Create(tempDirectory.RootPath);

        // Assert
        kitStore.GetFeatureNames()
            .Should().BeEmpty();
    }

    [Fact]
    public void Create_LocatorReturnsRepositoryRoot_ReturnsStoreOfShadeKitDirectoryAtThatRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");
        const string startPath = "/any/start/path";

        var locator = A.Fake<IGitRepositoryLocator>();
        A.CallTo(() => locator.GetRepositoryRootPath(startPath)).Returns(tempDirectory.RootPath);

        var factory = CreateFactory(locator);

        // Act
        var kitStore = factory.Create(startPath);

        // Assert
        kitStore.RootPath
            .Should().Be(Path.Combine(tempDirectory.RootPath, ".shadekit"));

        kitStore.GetFeatureNames()
            .Should().Equal("auth");
    }

    [Fact]
    public void Create_LocatorReturnsRootWithoutShadeKit_ThrowsKitRootPathNotFoundExceptionWithLocatedRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        const string startPath = "/any/start/path";

        var locator = A.Fake<IGitRepositoryLocator>();
        A.CallTo(() => locator.GetRepositoryRootPath(startPath)).Returns(tempDirectory.RootPath);

        var factory = CreateFactory(locator);

        // Act
        var act = () => factory.Create(startPath);

        // Assert
        var exception = act.Should().Throw<KitRootPathNotFoundException>().Which;

        exception.StartPath
            .Should().Be(startPath);

        exception.GitRootPath
            .Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    public void Create_LocatorThrowsNotAGitRepository_PropagatesException()
    {
        // Arrange
        var notAGitRepositoryException = new NotAGitRepositoryException("/outside");

        var locator = A.Fake<IGitRepositoryLocator>();
        A.CallTo(() => locator.GetRepositoryRootPath("/outside")).Throws(notAGitRepositoryException);

        var factory = CreateFactory(locator);

        // Act
        var act = () => factory.Create("/outside");

        // Assert
        act.Should().Throw<NotAGitRepositoryException>()
            .Which.Should().BeSameAs(notAGitRepositoryException);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Create_ShadeKitIsAFile_ThrowsKitRootNotADirectoryException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();
        tempDirectory.CreateFile(".shadekit");

        var factory = CreateFactory();

        // Act
        var act = () => factory.Create(tempDirectory.RootPath);

        // Assert
        act.Should().Throw<KitRootNotADirectoryException>()
            .Which.KitRootPath.Should().Be(Path.Combine(tempDirectory.RootPath, ".shadekit"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_NullOrWhitespacePath_ThrowsArgumentExceptionWithoutSearching(string? startPath)
    {
        // Arrange
        var locator = A.Fake<IGitRepositoryLocator>();
        var factory = CreateFactory(locator);

        // Act
        var act = () => factory.Create(startPath!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("path");

        A.CallTo(locator).MustNotHaveHappened();
    }

    [Fact]
    public void CreateForRepositoryRoot_ExistingKitRoot_ReturnsStoreOfShadeKitDirectoryWithoutSearching()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitRootPath = tempDirectory.CreateDirectory(".shadekit");
        tempDirectory.CreateDirectory(".shadekit", "features", "auth");

        var locator = A.Fake<IGitRepositoryLocator>();
        var factory = CreateFactory(locator);

        // Act
        var kitStore = factory.CreateForRepositoryRoot(tempDirectory.RootPath);

        // Assert
        kitStore.RootPath
            .Should().Be(kitRootPath);

        kitStore.GetFeatureNames()
            .Should().Equal("auth");

        A.CallTo(locator).MustNotHaveHappened();
    }

    [Fact]
    public void CreateForRepositoryRoot_KitRootDoesNotExist_ReturnsStoreWithoutCreatingDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitRootPath = Path.Combine(tempDirectory.RootPath, ".shadekit");

        var factory = CreateFactory();

        // Act
        var kitStore = factory.CreateForRepositoryRoot(tempDirectory.RootPath);

        // Assert
        kitStore.RootPath
            .Should().Be(kitRootPath);

        Path.Exists(kitRootPath)
            .Should().BeFalse();
    }

    [Fact]
    public async Task CreateForRepositoryRoot_KitRootDoesNotExist_StoreCreatesKitRootOnFirstWrite()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitRootPath = Path.Combine(tempDirectory.RootPath, ".shadekit");

        var factory = CreateFactory();
        var kitStore = factory.CreateForRepositoryRoot(tempDirectory.RootPath);

        // Act
        await kitStore.WriteJsonAsync(KitStorePaths.ConfigFileName, FormatIds.Config, new KitConfig(),
            FormatAccess.Write, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        File.Exists(Path.Combine(kitRootPath, KitStorePaths.ConfigFileName))
            .Should().BeTrue();
    }

    [Fact]
    public void CreateForRepositoryRoot_RootWithTrailingSeparator_ReturnsStoreWithRootPathWithoutTrailingSeparator()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitRootPath = tempDirectory.CreateDirectory(".shadekit");

        var factory = CreateFactory();

        // Act
        var kitStore = factory.CreateForRepositoryRoot(tempDirectory.RootPath + Path.DirectorySeparatorChar);

        // Assert
        kitStore.RootPath
            .Should().Be(kitRootPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void CreateForRepositoryRoot_NullOrWhitespacePath_ThrowsArgumentException(string? repositoryRootPath)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => factory.CreateForRepositoryRoot(repositoryRootPath!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(repositoryRootPath));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public void Create_GitRootWithTrailingSeparatorWithoutShadeKit_ReportsGitRootWithoutTrailingSeparator()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.CreateGitRoot();

        var factory = CreateFactory();

        // Act
        var act = () => factory.Create(tempDirectory.RootPath + Path.DirectorySeparatorChar);

        // Assert
        act.Should().Throw<KitRootPathNotFoundException>()
            .Which.GitRootPath.Should().Be(tempDirectory.RootPath);
    }

    [Fact]
    public void Ctor_NullGitRepositoryLocator_ThrowsArgumentNullException()
    {
        // Arrange
        var formatRegistry = FormatRegistry.CreateDefault();

        // Act
        var act = () => new KitStoreFactory(null!, new VersionedJsonSerializer(formatRegistry), formatRegistry,
            FixedLineEnding.CreateWriter(LineEnding.Lf));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitRepositoryLocator");
    }

    [Fact]
    public void Ctor_NullSerializer_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new KitStoreFactory(new GitRepositoryLocator(), null!, FormatRegistry.CreateDefault(),
            FixedLineEnding.CreateWriter(LineEnding.Lf));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serializer");
    }

    [Fact]
    public void Ctor_NullFormatRegistry_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new KitStoreFactory(new GitRepositoryLocator(),
            new VersionedJsonSerializer(FormatRegistry.CreateDefault()), null!,
            FixedLineEnding.CreateWriter(LineEnding.Lf));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formatRegistry");
    }

    [Fact]
    public void Ctor_NullTextFileWriter_ThrowsArgumentNullException()
    {
        // Arrange
        var formatRegistry = FormatRegistry.CreateDefault();

        // Act
        var act = () => new KitStoreFactory(new GitRepositoryLocator(), new VersionedJsonSerializer(formatRegistry),
            formatRegistry, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("textFileWriter");
    }

    [Fact]
    public async Task CreateForRepositoryRoot_StoreWritesFile_WritesThroughInjectedWriterWithRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var textFileWriter = A.Fake<ITextFileWriter>();
        var warning = new EditorConfigUnreadableWarning("/repo/.editorconfig", "Access denied.");
        A.CallTo(() => textFileWriter.WriteAsync(A<string>._, A<string>._, A<TextFileContent>._,
                A<string?>._, A<CancellationToken>._))
            .Returns([warning]);
        var factory = CreateFactory(textFileWriter: textFileWriter);
        var kitStore = factory.CreateForRepositoryRoot(tempDirectory.RootPath);
        var content = TextFileContent.FromText("# Spec\n");

        // Act
        var warnings = await kitStore.WriteFileAsync("templates/spec.md", content,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => textFileWriter.WriteAsync(tempDirectory.RootPath,
                Path.Combine(".shadekit", "templates", "spec.md"), content, A<string?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        warnings
            .Should().Equal(warning);
    }

    internal static KitStoreFactory CreateFactory(IGitRepositoryLocator? gitRepositoryLocator = null,
        ITextFileWriter? textFileWriter = null)
    {
        var formatRegistry = FormatRegistry.CreateDefault();

        return new KitStoreFactory(gitRepositoryLocator ?? new GitRepositoryLocator(),
            new VersionedJsonSerializer(formatRegistry), formatRegistry,
            textFileWriter ?? FixedLineEnding.CreateWriter(LineEnding.Lf));
    }
}
