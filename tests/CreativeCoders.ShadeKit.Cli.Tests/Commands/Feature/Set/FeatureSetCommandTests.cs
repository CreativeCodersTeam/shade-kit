using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.Set;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.SysConsole.Cli.Parsing;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace",
    Target = "~N:CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature.Set",
    Justification = "The test namespace mirrors the command folder Commands/Feature/Set/.")]

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature.Set;

public class FeatureSetCommandTests
{
    private const string Id = FeatureCommandTestHelper.FeatureId;

    [Fact]
    public async Task ExecuteAsync_PlainMode_WritesCurrentFeatureLine()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateFeatureStore(new SetCurrentFeatureResult(Id, [])));

        // Act
        var result = await command.ExecuteAsync(new FeatureSetCommandOptions { FeatureId = Id });

        // Assert
        console.Lines
            .Should().Equal($"Current feature: {Id}.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_RunToken_IsPassedToFeatureStore()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var featureStore = CreateFeatureStore(new SetCurrentFeatureResult(Id, []));
        var command = CreateCommand(new TestConsole(), featureStore,
            cancellation: new TestCliCancellation(cancellationSource.Token));

        // Act
        await command.ExecuteAsync(new FeatureSetCommandOptions { FeatureId = Id });

        // Assert
        A.CallTo(() => featureStore.SetCurrentAsync(A<IKitStore>._, Id, cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PassesIdAndStoreOfFactoryToFeatureStore()
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        var featureStore = CreateFeatureStore(new SetCurrentFeatureResult(Id, []));
        var context = TestShadeKitCommandContext.Create(new TestConsole(),
            kitStoreFactory: FeatureCommandTestHelper.CreateKitStoreFactory(kitStore));
        var command = new FeatureSetCommand(context, featureStore);

        // Act
        await command.ExecuteAsync(new FeatureSetCommandOptions { FeatureId = Id });

        // Assert
        A.CallTo(() => featureStore.SetCurrentAsync(kitStore, Id, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeIdWithMarkupCharacters_WritesIdVerbatim()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateFeatureStore(new SetCurrentFeatureResult("[red]x[/]", [])));

        // Act
        await command.ExecuteAsync(new FeatureSetCommandOptions { FeatureId = "[red]x[/]" });

        // Assert
        console.Lines
            .Should().Equal("Current feature: [red]x[/].");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeBranchMismatch_WritesWarningBeforeResult()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateFeatureStore(new SetCurrentFeatureResult(Id,
            [new CurrentFeatureMismatchWarning(Id, "branch-feature")])));

        // Act
        await command.ExecuteAsync(new FeatureSetCommandOptions { FeatureId = Id });

        // Assert
        console.Lines.Count
            .Should().Be(2);

        console.Lines[0]
            .Should().StartWith("Warning: ").And.Contain("branch-feature");

        console.Lines[1]
            .Should().Be($"Current feature: {Id}.");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeBranchMismatch_WritesIdAndMismatchWarning()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, CreateFeatureStore(new SetCurrentFeatureResult(Id,
            [new CurrentFeatureMismatchWarning(Id, "branch-feature")])), jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureSetCommandOptions { FeatureId = Id, Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var data = document.RootElement.GetProperty("data");

        FeatureCommandTestHelper.GetPropertyNames(data)
            .Should().Equal("id");

        data.GetProperty("id").GetString()
            .Should().Be(Id);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliWarningCodes.CurrentFeatureMismatch);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC7")]
    public async Task ExecuteAsync_UnknownId_ThrowsAbortExceptionWithFeatureNotFoundExitCode()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateThrowingFeatureStore(new FeatureNotFoundException("unknown-id")));

        // Act
        var act = () => command.ExecuteAsync(new FeatureSetCommandOptions { FeatureId = "unknown-id" });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.FeatureNotFound);

        exception.Message
            .Should().Contain("unknown-id");

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC7")]
    public async Task ExecuteAsync_JsonModeInvalidId_WritesErrorEnvelopeWithInvalidFeatureIdCode()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            CreateThrowingFeatureStore(new InvalidFeatureIdException("../x")), jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureSetCommandOptions { FeatureId = "../x", Json = true });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.InvalidFeatureId);

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidFeatureId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_KitRootNotFound_ThrowsAbortExceptionWithoutCallingFeatureStore(bool json)
    {
        // Arrange
        var featureStore = A.Fake<IFeatureStore>();
        using var jsonOutput = new StringWriter();
        var context = TestShadeKitCommandContext.Create(new TestConsole(),
            kitStoreFactory: FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")),
            jsonOutput: jsonOutput);
        var command = new FeatureSetCommand(context, featureStore);

        // Act
        var act = () => command.ExecuteAsync(new FeatureSetCommandOptions { FeatureId = Id, Json = json });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.PrintMessage
            .Should().Be(!json);

        jsonOutput.ToString().Contains(ShadeKitCliErrorCodes.KitRootNotFound, StringComparison.Ordinal)
            .Should().Be(json, "only JSON mode writes an error envelope");

        A.CallTo(featureStore)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), CreateFeatureStore(new SetCurrentFeatureResult(Id, [])));

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Theory]
    [InlineData(0, "context")]
    [InlineData(1, "featureStore")]
    public void Ctor_NullArgument_ThrowsArgumentNullException(int nullIndex, string parameterName)
    {
        // Act
        var act = () => new FeatureSetCommand(
            nullIndex == 0 ? null! : TestShadeKitCommandContext.Create(new TestConsole()),
            nullIndex == 1 ? null! : A.Fake<IFeatureStore>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void FeatureSetCommand_CliCommandAttribute_IsRegisteredAsFeatureSetCommand()
    {
        // Act
        var attribute = typeof(FeatureSetCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("feature", "set");
    }

    [Fact]
    public void FeatureSetCommandOptions_FeatureId_IsRequiredFirstPositionalValue()
    {
        // Act
        var attribute = typeof(FeatureSetCommandOptions).GetProperty(nameof(FeatureSetCommandOptions.FeatureId))!
            .GetCustomAttribute<OptionValueAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Index
            .Should().Be(0);

        attribute.IsRequired
            .Should().BeTrue();

        typeof(FeatureSetCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    private static IFeatureStore CreateFeatureStore(SetCurrentFeatureResult result)
    {
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.SetCurrentAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(result);

        return featureStore;
    }

    private static IFeatureStore CreateThrowingFeatureStore(Exception exception)
    {
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.SetCurrentAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(exception);

        return featureStore;
    }

    private static FeatureSetCommand CreateCommand(IAnsiConsole console, IFeatureStore featureStore,
        TextWriter? jsonOutput = null, ICliCancellation? cancellation = null)
    {
        var context = TestShadeKitCommandContext.Create(console,
            kitStoreFactory: FeatureCommandTestHelper.CreateKitStoreFactory(), jsonOutput: jsonOutput,
            cancellation: cancellation);

        return new FeatureSetCommand(context, featureStore);
    }
}
