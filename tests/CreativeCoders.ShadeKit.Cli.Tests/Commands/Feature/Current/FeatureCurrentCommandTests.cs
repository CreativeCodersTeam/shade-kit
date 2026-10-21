using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.Current;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Store;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature.Current;

public class FeatureCurrentCommandTests
{
    private const string Id = FeatureCommandTestHelper.FeatureId;

    [Theory]
    [InlineData(CurrentFeatureOrigin.Branch, "branch")]
    [InlineData(CurrentFeatureOrigin.Override, "override")]
    public async Task ExecuteAsync_PlainModeResolved_WritesFeatureAndOrigin(CurrentFeatureOrigin origin,
        string originName)
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateFeatureStore(CreateResolvedResult(origin)));

        // Act
        var result = await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines
            .Should().Equal($"{Id} (feature, active)", $"Resolved from: {originName}");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC6")]
    public async Task ExecuteAsync_PlainModeNotResolved_WritesHintAndReturnsSuccess()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateFeatureStore(CreateUnresolvedResult()));

        // Act
        var result = await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines
            .Should().Equal("No current feature.", "Run 'shade-kit feature set <id>' to select one.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeIdWithMarkupCharacters_WritesIdVerbatim()
    {
        // Arrange
        var console = new TestConsole();
        var resolved = CreateResolvedResult(CurrentFeatureOrigin.Branch);
        var command = CreateCommand(console, CreateFeatureStore(resolved with { FeatureId = "[red]x[/]" }));

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines[0]
            .Should().Be("[red]x[/] (feature, active)");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC5")]
    public async Task ExecuteAsync_PlainModeWithMismatchWarning_WritesWarningBeforeFeature()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateFeatureStore(CreateResolvedResult(CurrentFeatureOrigin.Branch,
            [FeatureWarning.CurrentFeatureMismatch("other-feature", Id)])));

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines.Count
            .Should().Be(3);

        console.Lines[0]
            .Should().StartWith("Warning: ").And.Contain("other-feature").And.Contain(Id);

        console.Lines[1]
            .Should().Be($"{Id} (feature, active)");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC11")]
    public async Task ExecuteAsync_JsonModeResolved_WritesEnvelopeWithFeatureDataShape()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console,
            CreateFeatureStore(CreateResolvedResult(CurrentFeatureOrigin.Override, status: FeatureStatus.Done)),
            jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var data = document.RootElement.GetProperty("data");
        var feature = data.GetProperty("feature");

        FeatureCommandTestHelper.GetPropertyNames(data)
            .Should().Equal("resolved", "resolvedFrom", "feature");

        data.GetProperty("resolved").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("resolvedFrom").GetString()
            .Should().Be("override");

        FeatureCommandTestHelper.GetPropertyNames(feature)
            .Should().Equal("id", "title", "profile", "status", "paths");

        feature.GetProperty("id").GetString()
            .Should().Be(Id);

        feature.GetProperty("title").GetString()
            .Should().Be("Login SSO");

        feature.GetProperty("profile").GetString()
            .Should().Be("feature");

        feature.GetProperty("status").GetString()
            .Should().Be("done");

        feature.GetProperty("paths").GetProperty("featureDir").GetString()
            .Should().Be($".shadekit/features/{Id}/");

        result.ExitCode
            .Should().Be(0);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC6")]
    public async Task ExecuteAsync_JsonModeNotResolved_WritesNotResolvedDataWithNullsAndReturnsSuccess()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateFeatureStore(CreateUnresolvedResult()), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var root = document.RootElement;
        var data = root.GetProperty("data");

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("resolved").GetBoolean()
            .Should().BeFalse();

        data.GetProperty("resolvedFrom").ValueKind
            .Should().Be(JsonValueKind.Null);

        data.GetProperty("feature").ValueKind
            .Should().Be(JsonValueKind.Null);

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithWarnings_WritesFormatThenFeatureWarnings()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var unresolved = CreateUnresolvedResult() with
        {
            Warnings = [FeatureWarning.CurrentFeatureMissing("gone")],
            FormatWarnings = [FeatureCommandTestHelper.UpgradeWarning]
        };
        var command = CreateCommand(new TestConsole(), CreateFeatureStore(unresolved), jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded, ShadeKitCliWarningCodes.CurrentFeatureMissing);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeNotResolvedWithWarnings_WritesFormatThenFeatureWarningsBeforeHint()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var unresolved = CreateUnresolvedResult() with
        {
            Warnings = [FeatureWarning.CurrentFeatureMissing("gone")],
            FormatWarnings = [FeatureCommandTestHelper.UpgradeWarning]
        };
        var command = CreateCommand(console, CreateFeatureStore(unresolved));

        // Act
        var result = await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        console.Lines.Count
            .Should().Be(4);

        console.Lines[0]
            .Should().StartWith("Warning: ").And.Contain("shade-kit upgrade");

        console.Lines[1]
            .Should().StartWith("Warning: ").And.Contain("'gone'");

        console.Lines.Skip(2)
            .Should().Equal("No current feature.", "Run 'shade-kit feature set <id>' to select one.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_RunToken_IsPassedToFeatureStore()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var featureStore = CreateFeatureStore(CreateUnresolvedResult());
        var command = CreateCommand(new TestConsole(), featureStore,
            cancellation: new TestCliCancellation(cancellationSource.Token));

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        A.CallTo(() => featureStore.GetCurrentAsync(A<IKitStore>._, cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PassesStoreOfFactoryToFeatureStore()
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        var featureStore = CreateFeatureStore(CreateUnresolvedResult());
        var command = new FeatureCurrentCommand(new TestConsole(),
            FeatureCommandTestHelper.CreateKitStoreFactory(kitStore), featureStore, new CliJsonWriter(TextWriter.Null),
            CliErrorMapperTests.CreateMapper(), TestCliCancellation.None);

        // Act
        await command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        A.CallTo(() => featureStore.GetCurrentAsync(kitStore, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_GitUnavailable_ThrowsAbortExceptionWithGitUnavailableExitCode()
    {
        // Arrange
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.GetCurrentAsync(A<IKitStore>._, A<CancellationToken>._))
            .ThrowsAsync(new GitUnavailableException(["symbolic-ref", "--short", "-q", "HEAD"], "git not found"));

        var command = CreateCommand(new TestConsole(), featureStore);

        // Act
        var act = () => command.ExecuteAsync(new FeatureCurrentCommandOptions());

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.GitUnavailable);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeKitRootNotFound_WritesErrorEnvelope()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = new FeatureCurrentCommand(new TestConsole(),
            FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")),
            A.Fake<IFeatureStore>(), new CliJsonWriter(jsonOutput), CliErrorMapperTests.CreateMapper(),
            TestCliCancellation.None);

        // Act
        var act = () => command.ExecuteAsync(new FeatureCurrentCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.PrintMessage
            .Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.KitRootNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), CreateFeatureStore(CreateUnresolvedResult()));

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Theory]
    [InlineData(0, "ansiConsole")]
    [InlineData(1, "kitStoreFactory")]
    [InlineData(2, "featureStore")]
    [InlineData(3, "jsonWriter")]
    [InlineData(4, "errorMapper")]
    [InlineData(5, "cancellation")]
    public void Ctor_NullArgument_ThrowsArgumentNullException(int nullIndex, string parameterName)
    {
        // Act
        var act = () => new FeatureCurrentCommand(
            nullIndex == 0 ? null! : new TestConsole(),
            nullIndex == 1 ? null! : A.Fake<IKitStoreFactory>(),
            nullIndex == 2 ? null! : A.Fake<IFeatureStore>(),
            nullIndex == 3 ? null! : A.Fake<ICliJsonWriter>(),
            nullIndex == 4 ? null! : CliErrorMapperTests.CreateMapper(),
            nullIndex == 5 ? null! : TestCliCancellation.None);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void FeatureCurrentCommand_CliCommandAttribute_IsRegisteredAsFeatureCurrentCommand()
    {
        // Act
        var attribute = typeof(FeatureCurrentCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("feature", "current");
    }

    [Fact]
    public void FeatureCurrentCommandOptions_Type_DerivesFromJsonCommandOptions()
    {
        // Assert
        typeof(FeatureCurrentCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    private static CurrentFeatureResult CreateResolvedResult(CurrentFeatureOrigin origin,
        IReadOnlyList<FeatureWarning>? warnings = null, FeatureStatus status = FeatureStatus.Active)
    {
        return new CurrentFeatureResult(Id, FeatureCommandTestHelper.CreateDocument(status: status), origin,
            FeaturePaths.For(Id), warnings ?? [], []);
    }

    private static CurrentFeatureResult CreateUnresolvedResult()
    {
        return new CurrentFeatureResult(null, null, null, null, [], Array.Empty<FormatWarning>());
    }

    private static IFeatureStore CreateFeatureStore(CurrentFeatureResult result)
    {
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.GetCurrentAsync(A<IKitStore>._, A<CancellationToken>._)).Returns(result);

        return featureStore;
    }

    private static FeatureCurrentCommand CreateCommand(IAnsiConsole console, IFeatureStore featureStore,
        TextWriter? jsonOutput = null, ICliCancellation? cancellation = null)
    {
        return new FeatureCurrentCommand(console, FeatureCommandTestHelper.CreateKitStoreFactory(), featureStore,
            new CliJsonWriter(jsonOutput ?? TextWriter.Null), CliErrorMapperTests.CreateMapper(),
            cancellation ?? TestCliCancellation.None);
    }
}
