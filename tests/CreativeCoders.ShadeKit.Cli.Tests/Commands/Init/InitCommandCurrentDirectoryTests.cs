using CreativeCoders.ShadeKit.Cli.Commands.Init;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Init;

[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed class InitCommandCurrentDirectoryTests : IDisposable
{
    private readonly CurrentDirectoryScope _currentDirectory = new CurrentDirectoryScope();

    private readonly TempKitDirectory _tempDirectory = new TempKitDirectory("shade-kit-[init]-");

    [Fact]
    public async Task ExecuteAsync_CurrentDirectory_PassesCurrentDirectoryToInitializer()
    {
        // Arrange
        var initializer = InitCommandTests.CreateInitializer(new RepositoryInitResult("/repo", [], []));
        var command = InitCommandTests.CreateCommand(new TestConsole(), initializer);

        Directory.SetCurrentDirectory(_tempDirectory.RootPath);
        var currentDirectory = Directory.GetCurrentDirectory();

        // Act
        await command.ExecuteAsync(new InitCommandOptions());

        // Assert
        A.CallTo(() => initializer.InitializeAsync(currentDirectory, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    public void Dispose()
    {
        _currentDirectory.Dispose();
        _tempDirectory.Dispose();
    }
}
