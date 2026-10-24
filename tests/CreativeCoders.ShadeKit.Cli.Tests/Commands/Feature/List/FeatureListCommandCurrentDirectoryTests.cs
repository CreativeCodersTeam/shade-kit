using AwesomeAssertions;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.List;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature.List;

[Collection(CurrentDirectoryCollectionDefinition.Name)]
public class FeatureListCommandCurrentDirectoryTests
{
    [Fact]
    public async Task ExecuteAsync_NotAGitRepositoryInDirectoryWithMarkupCharacters_EscapesAbortMessage()
    {
        // Arrange
        var kitStoreFactory = A.Fake<IKitStoreFactory>();

        var command = FeatureListCommandTests.CreateCommand(new TestConsole(), kitStoreFactory);

        using var markupDirectory = new TempKitDirectory("shade-kit-[tests]-");
        using var currentDirectory = new CurrentDirectoryScope(markupDirectory.RootPath);

        A.CallTo(() => kitStoreFactory.Create())
            .Throws(new NotAGitRepositoryException(Directory.GetCurrentDirectory()));

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.Message
            .Should().Contain(Markup.Escape(Directory.GetCurrentDirectory()));
    }
}
