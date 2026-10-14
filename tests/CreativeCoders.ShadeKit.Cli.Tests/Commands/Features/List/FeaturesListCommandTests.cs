using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Features.List;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Store;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Features.List;

public class FeaturesListCommandTests
{
    [Fact]
    public async Task ExecuteAsync_FeaturesExist_WritesFeatureNamesInOrdinalOrder()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateKitStoreFactory("login", "Billing", "auth"));

        // Act
        await command.ExecuteAsync(new FeaturesListCommandOptions());

        // Assert
        console.Lines
            .Should().Equal("Billing", "auth", "login");
    }

    [Fact]
    public async Task ExecuteAsync_FeaturesExist_ReturnsSuccess()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), CreateKitStoreFactory("auth"));

        // Act
        var result = await command.ExecuteAsync(new FeaturesListCommandOptions());

        // Assert
        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_FeatureNameContainsMarkupCharacters_WritesNameVerbatim()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateKitStoreFactory("[red]feature[/]"));

        // Act
        await command.ExecuteAsync(new FeaturesListCommandOptions());

        // Assert
        console.Lines
            .Should().Equal("[red]feature[/]");
    }

    [Fact]
    public async Task ExecuteAsync_NoFeatures_WritesHintAndReturnsSuccess()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateKitStoreFactory());

        // Act
        var result = await command.ExecuteAsync(new FeaturesListCommandOptions());

        // Assert
        console.Lines
            .Should().Equal("No features found.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public async Task ExecuteAsync_KitRootNotFound_ThrowsAbortExceptionWithKitRootNotFoundExitCodeAndInitHint()
    {
        // Arrange
        var rootNotFoundException = new KitRootPathNotFoundException("/repo/src", "/repo");
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Throws(rootNotFoundException);

        var console = new TestConsole();
        var command = CreateCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions());

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.IsError
            .Should().BeTrue();

        exception.InnerException
            .Should().BeSameAs(rootNotFoundException);

        exception.Message
            .Should().Contain("shade-kit init");

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

        var kitStoreFactory = CreateKitStoreFactory(kitStore);

        var console = new TestConsole();
        var command = CreateCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions());

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

        var kitStoreFactory = CreateKitStoreFactory(kitStore);

        var console = new TestConsole();
        var command = CreateCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions());

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
        var command = CreateCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions());

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
        A.CallTo(() => kitStoreFactory.Create()).Throws(new KitRootPathNotFoundException("/repo/src", "/repo"));

        var command = CreateCommand(new TestConsole(), kitStoreFactory);
        Task<CommandResult>? task = null;

        // Act
        var act = () => { task = command.ExecuteAsync(new FeaturesListCommandOptions()); };

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

        var kitStoreFactory = CreateKitStoreFactory(kitStore);

        var console = new TestConsole();
        var command = CreateCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions());

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

        var kitStoreFactory = CreateKitStoreFactory(kitStore);

        var command = CreateCommand(new TestConsole(), kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions());

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

        var command = CreateCommand(new TestConsole(), kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions());

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

        var kitStoreFactory = CreateKitStoreFactory(kitStore);

        var console = new TestConsole();
        var command = CreateCommand(console, kitStoreFactory);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions());

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
        var act = () => new FeaturesListCommand(null!, A.Fake<IKitStoreFactory>(), A.Fake<ICliJsonWriter>(),
            CliErrorMapperTests.CreateMapper());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ansiConsole");
    }

    [Fact]
    public void Ctor_NullKitStoreFactory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeaturesListCommand(new TestConsole(), null!, A.Fake<ICliJsonWriter>(),
            CliErrorMapperTests.CreateMapper());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("kitStoreFactory");
    }

    [Fact]
    public void Ctor_NullJsonWriter_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeaturesListCommand(new TestConsole(), A.Fake<IKitStoreFactory>(), null!,
            CliErrorMapperTests.CreateMapper());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jsonWriter");
    }

    [Fact]
    public void Ctor_NullErrorMapper_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeaturesListCommand(new TestConsole(), A.Fake<IKitStoreFactory>(),
            A.Fake<ICliJsonWriter>(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("errorMapper");
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), CreateKitStoreFactory());

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task ExecuteAsync_JsonModeWithFeatures_WritesEnvelopeWithFeatureNamesInOrdinalOrder()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, CreateKitStoreFactory("login", "Billing", "auth"), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new FeaturesListCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.EnumerateObject().Select(x => x.Name)
            .Should().Equal("ok", "data", "warnings", "error");

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        root.GetProperty("data").GetProperty("features").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("Billing", "auth", "login");

        result.ExitCode
            .Should().Be(0);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task ExecuteAsync_JsonModeWithoutFeatures_WritesEmptyFeatureListAsResult()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateKitStoreFactory(), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new FeaturesListCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        document.RootElement.GetProperty("data").GetProperty("features").GetArrayLength()
            .Should().Be(0);

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    [Trait("Requirement", "FP01-AC8")]
    [Trait("Requirement", "FP01-AC12")]
    public async Task ExecuteAsync_JsonModeKitRootNotFound_WritesErrorEnvelopeAndAbortsWithoutPrintedMessage()
    {
        // Arrange
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Throws(new KitRootPathNotFoundException("/repo/src", "/repo"));

        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, kitStoreFactory, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.PrintMessage
            .Should().BeFalse();

        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.EnumerateObject().Select(x => x.Name)
            .Should().Equal("ok", "data", "warnings", "error");

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("data").ValueKind
            .Should().Be(JsonValueKind.Null);

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.KitRootNotFound);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("shade-kit init");

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    public async Task ExecuteAsync_JsonModeFeaturesDirectoryNotFound_WritesErrorEnvelopeWithFeaturesDirCode()
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        A.CallTo(() => kitStore.GetFeatureNames()).Throws(new FeaturesDirectoryNotFoundException("/repo/.shadekit/features"));

        var kitStoreFactory = CreateKitStoreFactory(kitStore);

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), kitStoreFactory, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions { Json = true });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.FeaturesDirectoryNotFound);

        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.FeaturesDirectoryNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeUnexpectedException_PropagatesExceptionWithoutOutput()
    {
        // Arrange
        var ioException = new IOException("disk error");
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Throws(ioException);

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), kitStoreFactory, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions { Json = true });

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .Which.Should().BeSameAs(ioException);

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PlainMode_WritesNoJson()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateKitStoreFactory("auth"), jsonOutput);

        // Act
        await command.ExecuteAsync(new FeaturesListCommandOptions());

        // Assert
        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public void FeaturesListCommandOptions_Type_DerivesFromJsonCommandOptions()
    {
        // Assert
        typeof(FeaturesListCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
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

        return CreateKitStoreFactory(kitStore);
    }

    private static IKitStoreFactory CreateKitStoreFactory(IKitStore kitStore)
    {
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Returns(kitStore);

        return kitStoreFactory;
    }

    [Fact]
    [Trait("Requirement", "FP01-AC8")]
    public async Task ExecuteAsync_JsonModeEnumerationFailsMidway_WritesOnlyErrorEnvelope()
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        A.CallTo(() => kitStore.GetFeatureNames())
            .Returns(ThrowOnEnumeration(new FeaturesDirectoryNotFoundException("/repo/.shadekit/features")));

        var kitStoreFactory = CreateKitStoreFactory(kitStore);

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), kitStoreFactory, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions { Json = true });

        // Assert
        await act.Should().ThrowAsync<CliCommandAbortException>();

        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        jsonOutput.ToString()
            .Should().NotContain("never-listed");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeFeatureNameWithMarkupCharacters_WritesNameVerbatim()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateKitStoreFactory("[red]feature[/]"), jsonOutput);

        // Act
        await command.ExecuteAsync(new FeaturesListCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("data").GetProperty("features")[0].GetString()
            .Should().Be("[red]feature[/]");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModePathWithMarkupCharacters_KeepsRawPathInEnvelopeAndAbortMessage()
    {
        // Arrange
        var exception = new FeaturesDirectoryNotFoundException("/kit/[nested]/.shadekit/features");
        var kitStore = A.Fake<IKitStore>();
        A.CallTo(() => kitStore.GetFeatureNames()).Throws(exception);

        var kitStoreFactory = CreateKitStoreFactory(kitStore);

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), kitStoreFactory, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeaturesListCommandOptions { Json = true });

        // Assert
        var abortException = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        abortException.Message
            .Should().Contain("'/kit/[nested]/.shadekit/features'");

        abortException.PrintMessage
            .Should().BeFalse();

        abortException.IsError
            .Should().BeTrue();

        abortException.InnerException
            .Should().BeSameAs(exception);

        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("'/kit/[nested]/.shadekit/features'");

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        root.GetProperty("error").EnumerateObject().Select(x => x.Name)
            .Should().Equal("code", "message");
    }

    internal static FeaturesListCommand CreateCommand(IAnsiConsole console, IKitStoreFactory kitStoreFactory,
        TextWriter? jsonOutput = null)
    {
        return new FeaturesListCommand(console, kitStoreFactory, new CliJsonWriter(jsonOutput ?? TextWriter.Null),
            CliErrorMapperTests.CreateMapper());
    }
}
