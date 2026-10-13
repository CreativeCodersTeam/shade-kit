using AwesomeAssertions;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands.Features.List;
using CreativeCoders.ShadeKit.Core.Store;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Features.List;

[Collection(CurrentDirectoryCollectionDefinition.Name)]
public class FeaturesListCommandCurrentDirectoryTests
{
    [Fact]
    public async Task ExecuteAsync_KitRootNotFoundInDirectoryWithMarkupCharacters_EscapesAbortMessage()
    {
        // Arrange
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Throws(new KitRootPathNotFoundException(string.Empty));

        var command = new FeaturesListCommand(new TestConsole(), kitStoreFactory);

        var originalDirectory = Directory.GetCurrentDirectory();
        var markupDirectory = Directory.CreateTempSubdirectory("shade-kit-[tests]-");

        try
        {
            Directory.SetCurrentDirectory(markupDirectory.FullName);

            // Act
            var act = () => command.ExecuteAsync();

            // Assert
            var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

            exception.Message
                .Should().Contain(Markup.Escape(Directory.GetCurrentDirectory()));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            markupDirectory.Delete();
        }
    }
}
