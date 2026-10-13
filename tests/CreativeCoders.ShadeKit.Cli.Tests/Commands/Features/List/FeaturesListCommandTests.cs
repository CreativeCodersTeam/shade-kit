using System.Reflection;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands.Features.List;
using CreativeCoders.ShadeKit.Core.Store;
using FakeItEasy;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Features.List;

public class FeaturesListCommandTests
{
    [Fact]
    public async Task ExecuteAsync_FeaturesExist_WritesFeatureNamesInOrdinalOrder()
    {
        // Arrange
        var console = new TestConsole();
        var command = new FeaturesListCommand(console, CreateKitStoreFactory("login", "Billing", "auth"));

        // Act
        await command.ExecuteAsync();

        // Assert
        console.Lines
            .Should().Equal("Billing", "auth", "login");
    }

    [Fact]
    public async Task ExecuteAsync_FeaturesExist_ReturnsSuccess()
    {
        // Arrange
        var command = new FeaturesListCommand(new TestConsole(), CreateKitStoreFactory("auth"));

        // Act
        var result = await command.ExecuteAsync();

        // Assert
        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_FeatureNameContainsMarkupCharacters_WritesNameVerbatim()
    {
        // Arrange
        var console = new TestConsole();
        var command = new FeaturesListCommand(console, CreateKitStoreFactory("[red]feature[/]"));

        // Act
        await command.ExecuteAsync();

        // Assert
        console.Lines
            .Should().Equal("[red]feature[/]");
    }

    [Fact]
    public async Task ExecuteAsync_NoFeatures_WritesHintAndReturnsSuccess()
    {
        // Arrange
        var console = new TestConsole();
        var command = new FeaturesListCommand(console, CreateKitStoreFactory());

        // Act
        var result = await command.ExecuteAsync();

        // Assert
        console.Lines
            .Should().Equal("No features found.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_KitRootNotFound_ThrowsAbortExceptionWithKitRootNotFoundExitCode()
    {
        // Arrange
        var rootNotFoundException = new KitRootPathNotFoundException(string.Empty);
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Throws(rootNotFoundException);

        var console = new TestConsole();
        var command = new FeaturesListCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync();

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.IsError
            .Should().BeTrue();

        exception.InnerException
            .Should().BeSameAs(rootNotFoundException);

        exception.Message
            .Should().Contain(".shadekit");

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FeaturesDirectoryNotFound_ThrowsAbortExceptionWithFeaturesDirectoryNotFoundExitCode()
    {
        // Arrange
        var featuresDirectoryNotFoundException = new FeaturesDirectoryNotFoundException("/kit/[nested]/.shadekit/features");
        var kitStore = A.Fake<IKitStore>();
        A.CallTo(() => kitStore.GetFeatureNames()).Throws(featuresDirectoryNotFoundException);

        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Returns(kitStore);

        var console = new TestConsole();
        var command = new FeaturesListCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync();

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.FeaturesDirectoryNotFound);

        exception.IsError
            .Should().BeTrue();

        exception.InnerException
            .Should().BeSameAs(featuresDirectoryNotFoundException);

        exception.Message
            .Should().Contain("'features'")
            .And.Contain("'/kit/[[nested]]/.shadekit/features'");

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_LazyFeatureNamesThrowFeaturesDirectoryNotFoundOnEnumeration_ThrowsAbortExceptionWithFeaturesDirectoryNotFoundExitCode()
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        A.CallTo(() => kitStore.GetFeatureNames()).Returns(ThrowOnEnumeration(new FeaturesDirectoryNotFoundException("/kit/.shadekit/features")));

        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Returns(kitStore);

        var console = new TestConsole();
        var command = new FeaturesListCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync();

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.FeaturesDirectoryNotFound);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FactoryThrowsDirectoryNotFound_PropagatesExceptionUnwrapped()
    {
        // Arrange
        var directoryNotFoundException = new DirectoryNotFoundException("current directory deleted");
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Throws(directoryNotFoundException);

        var console = new TestConsole();
        var command = new FeaturesListCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync();

        // Assert
        (await act.Should().ThrowAsync<DirectoryNotFoundException>())
            .Which.Should().BeSameAs(directoryNotFoundException);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public void ExecuteAsync_KitRootNotFound_ReturnsFaultedTaskInsteadOfThrowingSynchronously()
    {
        // Arrange
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Throws(new KitRootPathNotFoundException(string.Empty));

        var command = new FeaturesListCommand(new TestConsole(), kitStoreFactory);
        Task<CommandResult>? task = null;

        // Act
        var act = () => { task = command.ExecuteAsync(); };

        // Assert
        act.Should().NotThrow();

        task!.IsFaulted
            .Should().BeTrue();

        task.Exception!.InnerException
            .Should().BeOfType<CliCommandAbortException>();
    }

    [Fact]
    public async Task ExecuteAsync_LazyFeatureNamesThrowBclDirectoryNotFoundOnEnumeration_PropagatesExceptionUnwrapped()
    {
        // Arrange
        var directoryNotFoundException = new DirectoryNotFoundException("kit root removed during enumeration");
        var kitStore = A.Fake<IKitStore>();
        A.CallTo(() => kitStore.GetFeatureNames()).Returns(ThrowOnEnumeration(directoryNotFoundException));

        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Returns(kitStore);

        var console = new TestConsole();
        var command = new FeaturesListCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync();

        // Assert
        (await act.Should().ThrowAsync<DirectoryNotFoundException>())
            .Which.Should().BeSameAs(directoryNotFoundException);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_StoreThrowsBclDirectoryNotFound_PropagatesExceptionUnwrapped()
    {
        // Arrange
        var directoryNotFoundException = new DirectoryNotFoundException("kit root removed during read");
        var kitStore = A.Fake<IKitStore>();
        A.CallTo(() => kitStore.GetFeatureNames()).Throws(directoryNotFoundException);

        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Returns(kitStore);

        var command = new FeaturesListCommand(new TestConsole(), kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync();

        // Assert
        (await act.Should().ThrowAsync<DirectoryNotFoundException>())
            .Which.Should().BeSameAs(directoryNotFoundException);
    }

    [Fact]
    public async Task ExecuteAsync_FactoryThrowsUnexpectedIoException_PropagatesExceptionUnwrapped()
    {
        // Arrange
        var accessException = new UnauthorizedAccessException("access denied");
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Throws(accessException);

        var command = new FeaturesListCommand(new TestConsole(), kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync();

        // Assert
        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .Which.Should().BeSameAs(accessException);
    }

    [Fact]
    public async Task ExecuteAsync_StoreThrowsUnexpectedIoException_PropagatesExceptionUnwrappedWithoutOutput()
    {
        // Arrange
        var ioException = new IOException("disk error");
        var kitStore = A.Fake<IKitStore>();
        A.CallTo(() => kitStore.GetFeatureNames()).Throws(ioException);

        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Returns(kitStore);

        var console = new TestConsole();
        var command = new FeaturesListCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync();

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .Which.Should().BeSameAs(ioException);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public void Ctor_NullConsole_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeaturesListCommand(null!, A.Fake<IKitStoreFactory>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ansiConsole");
    }

    [Fact]
    public void Ctor_NullKitStoreFactory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeaturesListCommand(new TestConsole(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("kitStoreFactory");
    }

    [Fact]
    public void FeaturesListCommand_CliCommandAttribute_IsRegisteredAsFeaturesListCommand()
    {
        // Act
        var attribute = typeof(FeaturesListCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute!.Commands
            .Should().Equal("features", "list");
    }

    [Fact]
    public void CliAssembly_CliCommandGroupAttributes_ContainFeaturesGroup()
    {
        // Act
        var groupAttributes = typeof(FeaturesListCommand).Assembly
            .GetCustomAttributes<CliCommandGroupAttribute>();

        // Assert
        groupAttributes
            .Where(x => x.Commands is ["features"])
            .Should().ContainSingle();
    }

    private static IEnumerable<string> ThrowOnEnumeration(Exception exception)
    {
        yield return "never-listed";

        throw exception;
    }

    private static IKitStoreFactory CreateKitStoreFactory(params string[] featureNames)
    {
        var kitStore = A.Fake<IKitStore>();
        A.CallTo(() => kitStore.GetFeatureNames()).Returns(featureNames);

        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Returns(kitStore);

        return kitStoreFactory;
    }
}
