using System.Text.RegularExpressions;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

public sealed class KitStoreDirectoryExtensionsTests : IDisposable
{
    private const string DirectoryPath = "features/login";

    private readonly TempKitDirectory _tempDirectory = new();

    private readonly KitStore _realStore;

    private readonly IKitStore _store;

    public KitStoreDirectoryExtensionsTests()
    {
        var formatRegistry = FormatRegistry.CreateDefault();
        _realStore = new KitStore(_tempDirectory.RootPath, new VersionedJsonSerializer(formatRegistry),
            formatRegistry, FixedLineEnding.CreateWriter(LineEnding.Lf));
        _store = A.Fake<IKitStore>(options => options.Wrapping(_realStore));
    }

    public void Dispose()
    {
        _tempDirectory.Dispose();
    }

    [Theory]
    [InlineData(".login", true)]
    [InlineData(".login.1a2b3c4d.tmp", true)]
    [InlineData(".login.1a2b3c4d.dropping", true)]
    [InlineData(".", true)]
    [InlineData("login", false)]
    [InlineData("login.tmp", false)]
    [InlineData("", false)]
    public void IsHiddenName_Name_ReturnsWhetherItStartsWithDot(string name, bool expected)
    {
        // Act
        var result = KitStoreDirectoryExtensions.IsHiddenName(name);

        // Assert
        result
            .Should().Be(expected);
    }

    [Fact]
    public void IsHiddenName_NullName_ThrowsArgumentNullException()
    {
        // Act
        var act = () => KitStoreDirectoryExtensions.IsHiddenName(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_NewDirectory_PopulatesHiddenStagingDirectoryAndMovesItIntoPlace()
    {
        // Arrange
        string? stagingPath = null;

        // Act
        await _store.CreateDirectoryExclusiveAsync(DirectoryPath, async (path, cancellationToken) =>
        {
            stagingPath = path;

            // The directory appears only with the move.
            _realStore.DirectoryExists(DirectoryPath)
                .Should().BeFalse();

            await _realStore.WriteFileAsync($"{path}/a.txt", TextFileContent.FromText("a"), cancellationToken: cancellationToken);
        }, TestContext.Current.CancellationToken);

        // Assert
        stagingPath
            .Should().MatchRegex(StagingPathRegex("features/", "login"));

        A.CallTo(() => _store.CreateDirectory(stagingPath))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _store.MoveDirectory(stagingPath, DirectoryPath))
                .MustHaveHappenedOnceExactly());

        _realStore.GetFiles(DirectoryPath)
            .Should().Equal("features/login/a.txt");

        GetDirectoryNames("features")
            .Should().Equal("login");
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_PathWithoutParent_StagesNextToDirectoryInRoot()
    {
        // Arrange
        string? stagingPath = null;

        // Act
        await _store.CreateDirectoryExclusiveAsync("login", (path, _) =>
        {
            stagingPath = path;

            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        // Assert
        stagingPath
            .Should().MatchRegex(StagingPathRegex("", "login"));

        _realStore.DirectoryExists("login")
            .Should().BeTrue();
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_TwoRuns_UseDifferentStagingDirectories()
    {
        // Arrange
        List<string> stagingPaths = [];

        Task Populate(string path, CancellationToken _)
        {
            stagingPaths.Add(path);

            return Task.CompletedTask;
        }

        // Act
        await _store.CreateDirectoryExclusiveAsync("features/a", Populate, TestContext.Current.CancellationToken);
        await _store.CreateDirectoryExclusiveAsync("features/a-copy", Populate, TestContext.Current.CancellationToken);

        // Assert
        stagingPaths.Select(x => x.Split('.')[^2])
            .Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("features/")]
    [InlineData("")]
    public async Task CreateDirectoryExclusiveAsync_PathWithoutName_ThrowsArgumentExceptionAndCreatesNothing(
        string relativePath)
    {
        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(relativePath, (_, _) => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        A.CallTo(() => _store.CreateDirectory(A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_DirectoryCreatedBeforeMove_ThrowsExistsAndKeepsOtherDirectory()
    {
        // Arrange
        // Another run creates the directory between the caller's existence check and the move.
        Task Populate(string path, CancellationToken cancellationToken)
        {
            _tempDirectory.WriteFile("other run", "features", "login", "other.txt");

            return _realStore.WriteFileAsync($"{path}/a.txt", TextFileContent.FromText("a"), cancellationToken: cancellationToken);
        }

        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(DirectoryPath, Populate,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<KitDirectoryExistsException>()).Which;

        exception.RelativePath
            .Should().Be(DirectoryPath);

        exception.FileSystemException
            .Should().BeAssignableTo<IOException>()
            .And.BeSameAs(exception.InnerException);

        _realStore.GetFiles(DirectoryPath)
            .Should().Equal("features/login/other.txt");

        GetDirectoryNames("features")
            .Should().Equal("login");
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task CreateDirectoryExclusiveAsync_MoveDeniedAndOtherRunCreatedDirectory_ThrowsExists(
        Type exceptionType)
    {
        // Arrange
        var moveException = (Exception)Activator.CreateInstance(exceptionType, "Denied.")!;
        A.CallTo(() => _store.MoveDirectory(A<string>._, DirectoryPath))
            .Invokes(() => _tempDirectory.CreateDirectory("features", "login"))
            .Throws(moveException);

        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(DirectoryPath, (_, _) => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<KitDirectoryExistsException>()).Which.FileSystemException
            .Should().BeSameAs(moveException);

        GetDirectoryNames("features")
            .Should().Equal("login");
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(DirectoryNotFoundException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task CreateDirectoryExclusiveAsync_StagingDirectoryCannotBeCreated_ThrowsIoFailedAndMovesNothing(
        Type exceptionType)
    {
        // Arrange
        var createException = (Exception)Activator.CreateInstance(exceptionType, "Denied.")!;
        A.CallTo(() => _store.CreateDirectory(A<string>._)).Throws(createException);
        var populated = false;

        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(DirectoryPath, (_, _) =>
        {
            populated = true;

            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<KitDirectoryIoFailedException>()).Which;

        exception.RelativePath
            .Should().Be(DirectoryPath);

        exception.FileSystemException
            .Should().BeSameAs(createException)
            .And.BeSameAs(exception.InnerException);

        populated
            .Should().BeFalse();

        A.CallTo(() => _store.MoveDirectory(A<string>._, A<string>._))
            .MustNotHaveHappened();

        _realStore.DirectoryExists("features")
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(DirectoryNotFoundException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task CreateDirectoryExclusiveAsync_MoveFailsAndNothingAtDestination_ThrowsIoFailedAndRemovesStaging(
        Type exceptionType)
    {
        // Arrange
        var moveException = (Exception)Activator.CreateInstance(exceptionType, "The file is locked.")!;
        A.CallTo(() => _store.MoveDirectory(A<string>._, A<string>._)).Throws(moveException);

        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(DirectoryPath, WriteFileAsync,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<KitDirectoryIoFailedException>()).Which.FileSystemException
            .Should().BeSameAs(moveException);

        GetDirectoryNames("features")
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_FileAtDestination_RethrowsIOExceptionUnwrappedAndKeepsFile()
    {
        // Arrange
        _tempDirectory.WriteFile("file", "features", "login");

        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(DirectoryPath, WriteFileAsync,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<IOException>();

        _tempDirectory.ReadFileText("features", "login")
            .Should().Be("file");

        GetDirectoryNames("features")
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_MoveFailsWithOtherException_RethrowsItAndRemovesStaging()
    {
        // Arrange
        var moveException = new InvalidOperationException("Unexpected.");
        A.CallTo(() => _store.MoveDirectory(A<string>._, A<string>._)).Throws(moveException);

        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(DirectoryPath, WriteFileAsync,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which
            .Should().BeSameAs(moveException);

        A.CallTo(() => _store.DirectoryExists(A<string>._))
            .MustNotHaveHappened();

        GetDirectoryNames("features")
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_PopulateFailsWithIOException_RethrowsItUnwrappedAndRemovesStaging()
    {
        // Arrange
        var populateException = new IOException("The disk is full.");

        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(DirectoryPath, async (path, cancellationToken) =>
        {
            await WriteFileAsync(path, cancellationToken);

            throw populateException;
        }, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<IOException>()).Which
            .Should().BeSameAs(populateException);

        A.CallTo(() => _store.MoveDirectory(A<string>._, A<string>._))
            .MustNotHaveHappened();

        GetDirectoryNames("features")
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_CanceledWhilePopulating_ThrowsAndRemovesStagingWithoutMove()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(DirectoryPath, async (path, cancellationToken) =>
        {
            await WriteFileAsync(path, cancellationToken);
            await cancellationTokenSource.CancelAsync();
        }, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(() => _store.MoveDirectory(A<string>._, A<string>._))
            .MustNotHaveHappened();

        GetDirectoryNames("features")
            .Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_PopulateReceivesToken_PassesCallerToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken receivedToken = default;

        // Act
        await _store.CreateDirectoryExclusiveAsync(DirectoryPath, (_, cancellationToken) =>
        {
            receivedToken = cancellationToken;

            return Task.CompletedTask;
        }, cancellationTokenSource.Token);

        // Assert
        receivedToken
            .Should().Be(cancellationTokenSource.Token);
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_FailureAndStagingCannotBeDeleted_RethrowsOriginalAndKeepsStaging()
    {
        // Arrange
        var moveException = new IOException("The file is locked.");
        A.CallTo(() => _store.MoveDirectory(A<string>._, A<string>._)).Throws(moveException);
        A.CallTo(() => _store.DeleteDirectory(A<string>._)).Throws(new IOException("Still locked."));

        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(DirectoryPath, WriteFileAsync,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<KitDirectoryIoFailedException>()).Which.FileSystemException
            .Should().BeSameAs(moveException);

        GetDirectoryNames("features")
            .Should().ContainSingle()
            .Which.Should().MatchRegex(StagingPathRegex("", "login"));
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_NullStore_ThrowsArgumentNullException()
    {
        // Act
        var act = () => KitStoreDirectoryExtensions.CreateDirectoryExclusiveAsync(null!, DirectoryPath,
            (_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateDirectoryExclusiveAsync_NullPopulate_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _store.CreateDirectoryExclusiveAsync(DirectoryPath, null!,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();

        A.CallTo(() => _store.CreateDirectory(A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public void UndoCreateDirectoryExclusive_ExistingDirectory_MovesItToHiddenStagingDirectoryAndDeletesIt()
    {
        // Arrange
        _tempDirectory.WriteFile("a", "features", "login", "a.txt");
        string? hiddenPath = null;
        A.CallTo(() => _store.MoveDirectory(DirectoryPath, A<string>._))
            .Invokes((string _, string destination) => hiddenPath = destination)
            .CallsWrappedMethod();

        // Act
        _store.UndoCreateDirectoryExclusive(DirectoryPath);

        // Assert
        hiddenPath
            .Should().MatchRegex(StagingPathRegex("features/", "login"));

        A.CallTo(() => _store.DeleteDirectory(hiddenPath))
            .MustHaveHappenedOnceExactly();

        GetDirectoryNames("features")
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void UndoCreateDirectoryExclusive_MoveFails_KeepsDirectoryInPlaceWithoutThrowing(Type exceptionType)
    {
        // Arrange
        _tempDirectory.WriteFile("a", "features", "login", "a.txt");
        A.CallTo(() => _store.MoveDirectory(A<string>._, A<string>._))
            .Throws((Exception)Activator.CreateInstance(exceptionType, "The folder is locked.")!);

        // Act
        var act = () => _store.UndoCreateDirectoryExclusive(DirectoryPath);

        // Assert
        act.Should().NotThrow();

        A.CallTo(() => _store.DeleteDirectory(A<string>._))
            .MustNotHaveHappened();

        _realStore.GetFiles(DirectoryPath)
            .Should().Equal("features/login/a.txt");
    }

    [Fact]
    public void UndoCreateDirectoryExclusive_DeleteFails_KeepsHiddenStagingDirectoryWithoutThrowing()
    {
        // Arrange
        _tempDirectory.WriteFile("a", "features", "login", "a.txt");
        A.CallTo(() => _store.DeleteDirectory(A<string>._)).Throws(new UnauthorizedAccessException("Denied."));

        // Act
        var act = () => _store.UndoCreateDirectoryExclusive(DirectoryPath);

        // Assert
        act.Should().NotThrow();

        GetDirectoryNames("features")
            .Should().ContainSingle()
            .Which.Should().MatchRegex(StagingPathRegex("", "login"));
    }

    [Fact]
    public void UndoCreateDirectoryExclusive_MoveFailsWithOtherException_RethrowsIt()
    {
        // Arrange
        var moveException = new InvalidOperationException("Unexpected.");
        A.CallTo(() => _store.MoveDirectory(A<string>._, A<string>._)).Throws(moveException);

        // Act
        var act = () => _store.UndoCreateDirectoryExclusive(DirectoryPath);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .Which.Should().BeSameAs(moveException);
    }

    [Fact]
    public void UndoCreateDirectoryExclusive_NullStore_ThrowsArgumentNullException()
    {
        // Act
        var act = () => KitStoreDirectoryExtensions.UndoCreateDirectoryExclusive(null!, DirectoryPath);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DeleteDirectoryAtomically_ExistingDirectory_RenamesToHiddenDroppingDirectoryThenDeletesIt()
    {
        // Arrange
        _tempDirectory.WriteFile("a", "features", "login", "sub", "a.txt");
        _tempDirectory.CreateDirectory("features", "other");
        string? droppingPath = null;
        A.CallTo(() => _store.MoveDirectory(DirectoryPath, A<string>._))
            .Invokes((string _, string destination) => droppingPath = destination)
            .CallsWrappedMethod();

        // Act
        _store.DeleteDirectoryAtomically(DirectoryPath);

        // Assert
        droppingPath
            .Should().MatchRegex(DroppingPathRegex("features/", "login"));

        A.CallTo(() => _store.MoveDirectory(DirectoryPath, droppingPath))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _store.DeleteDirectory(droppingPath))
                .MustHaveHappenedOnceExactly());

        A.CallTo(() => _store.DeleteDirectory(DirectoryPath))
            .MustNotHaveHappened();

        GetDirectoryNames("features")
            .Should().Equal("other");
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(DirectoryNotFoundException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void DeleteDirectoryAtomically_RenameFails_ThrowsIoFailedAndDeletesNothing(Type exceptionType)
    {
        // Arrange
        _tempDirectory.WriteFile("a", "features", "login", "a.txt");
        var renameException = (Exception)Activator.CreateInstance(exceptionType, "The file is locked.")!;
        A.CallTo(() => _store.MoveDirectory(A<string>._, A<string>._)).Throws(renameException);

        // Act
        var act = () => _store.DeleteDirectoryAtomically(DirectoryPath);

        // Assert
        var exception = act.Should().Throw<KitDirectoryIoFailedException>().Which;

        exception.RelativePath
            .Should().Be(DirectoryPath);

        exception.FileSystemException
            .Should().BeSameAs(renameException);

        A.CallTo(() => _store.DeleteDirectory(A<string>._))
            .MustNotHaveHappened();

        _realStore.GetFiles(DirectoryPath)
            .Should().Equal("features/login/a.txt");
    }

    [Fact]
    public void DeleteDirectoryAtomically_MissingDirectory_ThrowsIoFailedWithDirectoryNotFound()
    {
        // Act
        var act = () => _store.DeleteDirectoryAtomically(DirectoryPath);

        // Assert
        act.Should().Throw<KitDirectoryIoFailedException>()
            .Which.FileSystemException.Should().BeOfType<DirectoryNotFoundException>();

        A.CallTo(() => _store.DeleteDirectory(A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public void DeleteDirectoryAtomically_RenameFailsWithOtherException_RethrowsItUnwrapped()
    {
        // Arrange
        var renameException = new InvalidOperationException("Unexpected.");
        A.CallTo(() => _store.MoveDirectory(A<string>._, A<string>._)).Throws(renameException);

        // Act
        var act = () => _store.DeleteDirectoryAtomically(DirectoryPath);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .Which.Should().BeSameAs(renameException);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void DeleteDirectoryAtomically_DeleteFails_RethrowsUnwrappedAndDirectoryIsGoneFromItsPath(
        Type exceptionType)
    {
        // Arrange
        _tempDirectory.WriteFile("a", "features", "login", "a.txt");
        var deleteException = (Exception)Activator.CreateInstance(exceptionType, "The folder is locked.")!;
        A.CallTo(() => _store.DeleteDirectory(A<string>._)).Throws(deleteException);

        // Act
        var act = () => _store.DeleteDirectoryAtomically(DirectoryPath);

        // Assert
        act.Should().Throw<Exception>()
            .Which.Should().BeSameAs(deleteException);

        _realStore.DirectoryExists(DirectoryPath)
            .Should().BeFalse();

        GetDirectoryNames("features")
            .Should().ContainSingle()
            .Which.Should().MatchRegex(DroppingPathRegex("", "login"));
    }

    [Fact]
    public void DeleteDirectoryAtomically_NullStore_ThrowsArgumentNullException()
    {
        // Act
        var act = () => KitStoreDirectoryExtensions.DeleteDirectoryAtomically(null!, DirectoryPath);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void KitDirectoryExistsException_NullFileSystemException_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new KitDirectoryExistsException(DirectoryPath, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void KitDirectoryIoFailedException_NullFileSystemException_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new KitDirectoryIoFailedException(DirectoryPath, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static string StagingPathRegex(string parentPath, string name)
    {
        return $"^{Regex.Escape(parentPath)}\\.{Regex.Escape(name)}\\.[0-9a-f]{{8}}\\.tmp$";
    }

    private static string DroppingPathRegex(string parentPath, string name)
    {
        return $"^{Regex.Escape(parentPath)}\\.{Regex.Escape(name)}\\.[0-9a-f]{{8}}\\.dropping$";
    }

    private Task WriteFileAsync(string directoryPath, CancellationToken cancellationToken)
    {
        return _realStore.WriteFileAsync($"{directoryPath}/a.txt", TextFileContent.FromText("a"), cancellationToken: cancellationToken);
    }

    private IEnumerable<string> GetDirectoryNames(string relativePath)
    {
        var path = Path.Combine(_tempDirectory.RootPath, relativePath);

        return Directory.Exists(path)
            ? Directory.EnumerateDirectories(path).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)
            : [];
    }
}
