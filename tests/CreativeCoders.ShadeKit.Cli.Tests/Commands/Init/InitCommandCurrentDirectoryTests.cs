using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Init;
using CreativeCoders.ShadeKit.Core.Init;
using FakeItEasy;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Init;

[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed class InitCommandCurrentDirectoryTests : IDisposable
{
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    private readonly DirectoryInfo _tempDirectory = Directory.CreateTempSubdirectory("shade-kit-[init]-");

    [Fact]
    public async Task ExecuteAsync_CurrentDirectory_PassesCurrentDirectoryToInitializer()
    {
        // Arrange
        var initializer = InitCommandTests.CreateInitializer(new RepositoryInitResult("/repo", []));
        var command = InitCommandTests.CreateCommand(new TestConsole(), initializer);

        Directory.SetCurrentDirectory(_tempDirectory.FullName);
        var currentDirectory = Directory.GetCurrentDirectory();

        // Act
        await command.ExecuteAsync(new InitCommandOptions());

        // Assert
        A.CallTo(() => initializer.InitializeAsync(currentDirectory, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        _tempDirectory.Delete(true);
    }
}
