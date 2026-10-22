using System.Reflection;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.Drop;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.SysConsole.Cli.Parsing;
using FakeItEasy;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature.Drop;

public sealed class FeatureDropCommandTests : IDisposable
{
    private const string Id = FeatureCommandTestHelper.FeatureId;

    private static readonly string[] Files =
    [
        $".shadekit/features/{Id}/feature.json",
        $".shadekit/features/{Id}/request.md"
    ];

    private readonly CancellationTokenSource _cancellationSource = new();

    private readonly IConsoleConfirmation _confirmation = A.Fake<IConsoleConfirmation>();

    private readonly TestConsole _console = new();

    private readonly IFeatureStore _featureStore = A.Fake<IFeatureStore>();

    private readonly IKitStore _kitStore = A.Fake<IKitStore>();

    public FeatureDropCommandTests()
    {
        A.CallTo(() => _featureStore.PreviewDropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(new FeatureDropPreview(Id, Files, true));
        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(new FeatureDropResult(Id, Files, true));
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithYes_DropsWithoutAskingAndWritesResult()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Yes = true });

        // Assert
        A.CallTo(() => _featureStore.DropAsync(_kitStore, Id, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(_confirmation)
            .MustNotHaveHappened();

        A.CallTo(() => _featureStore.PreviewDropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        _console.Lines
            .Should().Equal($"Dropped feature {Id}.", "The local override pointed to this feature and was cleared.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithYesOverrideNotCleared_WritesOnlyDroppedLine()
    {
        // Arrange
        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(new FeatureDropResult(Id, Files, false));

        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Yes = true });

        // Assert
        _console.Lines
            .Should().Equal($"Dropped feature {Id}.");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task ExecuteAsync_JsonModeWithYes_WritesDroppedData()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Yes = true, Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var data = document.RootElement.GetProperty("data");

        FeatureCommandTestHelper.GetPropertyNames(data)
            .Should().Equal("id", "dropped", "files", "overrideAffected", "overrideCleared");

        data.GetProperty("id").GetString()
            .Should().Be(Id);

        data.GetProperty("dropped").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("files").EnumerateArray().Select(x => x.GetString())
            .Should().Equal(Files);

        data.GetProperty("overrideAffected").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("overrideCleared").GetBoolean()
            .Should().BeTrue();

        A.CallTo(_confirmation)
            .MustNotHaveHappened();

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task ExecuteAsync_JsonModeWithoutYes_WritesPreviewAndDropsNothing()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(true);

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var root = document.RootElement;
        var data = root.GetProperty("data");

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        FeatureCommandTestHelper.GetPropertyNames(data)
            .Should().Equal("id", "dropped", "files", "overrideAffected", "overrideCleared");

        data.GetProperty("dropped").GetBoolean()
            .Should().BeFalse();

        data.GetProperty("files").EnumerateArray().Select(x => x.GetString())
            .Should().Equal(Files);

        data.GetProperty("overrideAffected").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("overrideCleared").GetBoolean()
            .Should().BeFalse();

        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        result.ExitCode
            .Should().Be(0);

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC10")]
    public async Task ExecuteAsync_PlainModeInteractiveConfirmed_WritesPreviewAsksAndDrops()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(true);

        var command = CreateCommand();

        // Act
        var result = await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id });

        // Assert
        A.CallTo(() => _confirmation.ConfirmAsync($"Drop feature {Id}?", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _featureStore.DropAsync(_kitStore, Id, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        _console.Lines
            .Should().Equal(
                $"Feature {Id} would be dropped. Files:",
                $"  {Files[0]}",
                $"  {Files[1]}",
                "The local override points to this feature and would be cleared.",
                $"Dropped feature {Id}.",
                "The local override pointed to this feature and was cleared.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC10")]
    public async Task ExecuteAsync_PlainModeInteractiveDeclined_DropsNothing()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(false);

        var command = CreateCommand();

        // Act
        var result = await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id });

        // Assert
        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        _console.Lines[^1]
            .Should().Be("Nothing was dropped.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC10")]
    public async Task ExecuteAsync_PlainModeCannotAsk_WritesPreviewAndHintWithoutAskingOrDropping()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(false);
        A.CallTo(() => _featureStore.PreviewDropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(new FeatureDropPreview(Id, Files, false));

        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console: console);

        // Act
        var result = await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id });

        // Assert
        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        console.Lines
            .Should().Equal(
                $"Feature {Id} would be dropped. Files:",
                $"  {Files[0]}",
                $"  {Files[1]}",
                $"Nothing was dropped. Run 'shade-kit feature drop {Id} --yes' to drop it.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeFileWithMarkupCharacters_WritesFileVerbatim()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(false);
        A.CallTo(() => _featureStore.PreviewDropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(new FeatureDropPreview(Id, [$".shadekit/features/{Id}/[x].md"], false));

        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id });

        // Assert
        _console.Lines[1]
            .Should().Be($"  .shadekit/features/{Id}/[x].md");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC7")]
    public async Task ExecuteAsync_UnknownId_ThrowsAbortExceptionWithFeatureNotFoundExitCodeWithoutAsking()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        A.CallTo(() => _featureStore.PreviewDropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new FeatureNotFoundException("unknown-id"));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = "unknown-id" });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.FeatureNotFound);

        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithYesUnknownId_WritesErrorEnvelope()
    {
        // Arrange
        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new FeatureNotFoundException("unknown-id"));

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(
            new FeatureDropCommandOptions { FeatureId = "unknown-id", Yes = true, Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.FeatureNotFound);

        exception.PrintMessage
            .Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.FeatureNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_KitRootNotFound_ThrowsAbortExceptionWithoutCallingFeatureStore()
    {
        // Arrange
        var context = TestShadeKitCommandContext.Create(_console,
            kitStoreFactory: FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")));
        var command = new FeatureDropCommand(context, _featureStore, _confirmation);

        // Act
        var act = () => command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Yes = true });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        A.CallTo(_featureStore)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeConfirmedButDropFails_ThrowsAbortExceptionAfterPreviewWithoutDroppedLine()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new FeatureNotFoundException(Id));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.FeatureNotFound);

        _console.Lines[0]
            .Should().Be($"Feature {Id} would be dropped. Files:");

        _console.Lines
            .Should().NotContain(x => x.StartsWith("Dropped feature", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_JsonModeKitRootNotFound_WritesErrorEnvelopeWithoutPrintedMessage(bool yes)
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var context = TestShadeKitCommandContext.Create(_console,
            kitStoreFactory: FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")),
            jsonOutput: jsonOutput);
        var command = new FeatureDropCommand(context, _featureStore, _confirmation);

        // Act
        var act = () => command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Yes = yes, Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.PrintMessage
            .Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.KitRootNotFound);

        A.CallTo(_featureStore)
            .MustNotHaveHappened();

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeConfirmed_UsesOneStoreForPreviewAndDrop()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(true);

        var kitStoreFactory = FeatureCommandTestHelper.CreateKitStoreFactory(_kitStore);
        var context = TestShadeKitCommandContext.Create(_console, kitStoreFactory: kitStoreFactory);
        var command = new FeatureDropCommand(context, _featureStore, _confirmation);

        // Act
        await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id });

        // Assert
        A.CallTo(() => kitStoreFactory.Create())
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _featureStore.PreviewDropAsync(_kitStore, Id, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _featureStore.DropAsync(_kitStore, Id, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task ExecuteAsync_JsonModePreviewFails_WritesOnlyErrorEnvelope()
    {
        // Arrange
        A.CallTo(() => _featureStore.PreviewDropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidFeatureIdException("Bad"));

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = "Bad", Json = true });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.InvalidFeatureId);

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidFeatureId);

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeConfirmationCancelled_PropagatesWithoutDropping()
    {
        // Arrange
        var cancelledException = new OperationCanceledException("cancelled");
        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._))
            .ThrowsAsync(cancelledException);

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id });

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(cancelledException);

        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        _console.Lines
            .Should().NotContain("Nothing was dropped.");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task ExecuteAsync_JsonModeWithoutYesCannotAsk_WritesOnlyPreviewEnvelope()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(false);

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("data").GetProperty("dropped").GetBoolean()
            .Should().BeFalse();

        _console.Output
            .Should().BeEmpty();

        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task ExecuteAsync_JsonModeWithoutYesOverrideNotAffected_WritesOverrideAffectedFalse()
    {
        // Arrange
        A.CallTo(() => _featureStore.PreviewDropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(new FeatureDropPreview(Id, Files, false));

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var data = document.RootElement.GetProperty("data");

        data.GetProperty("overrideAffected").GetBoolean()
            .Should().BeFalse();

        data.GetProperty("overrideCleared").GetBoolean()
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_JsonModeWithYes_WritesOverrideAffectedAndClearedFromResult(bool overrideAffected)
    {
        // Arrange
        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .Returns(new FeatureDropResult(Id, Files, overrideAffected));

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Yes = true, Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var data = document.RootElement.GetProperty("data");

        data.GetProperty("overrideAffected").GetBoolean()
            .Should().Be(overrideAffected);

        data.GetProperty("overrideCleared").GetBoolean()
            .Should().Be(overrideAffected);
    }

    [Fact]
    public async Task ExecuteAsync_WithYes_PassesRunTokenToDrop()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Yes = true });

        // Assert
        A.CallTo(() => _featureStore.DropAsync(_kitStore, Id, _cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeInteractiveConfirmed_PassesRunTokenToPreviewConfirmationAndDrop()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(true);

        var command = CreateCommand();

        // Act
        await command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id });

        // Assert
        A.CallTo(() => _featureStore.PreviewDropAsync(_kitStore, Id, _cancellationSource.Token))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, _cancellationSource.Token))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _featureStore.DropAsync(_kitStore, Id, _cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_DropCanceled_PropagatesOperationCanceledException()
    {
        // Arrange
        await _cancellationSource.CancelAsync();

        var canceledException = new OperationCanceledException(_cancellationSource.Token);
        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(canceledException);

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Yes = true, Json = true });

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceledException);

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_PreviewCanceled_PropagatesWithoutOutputConfirmationOrDrop(bool json)
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        var canceledException = new OperationCanceledException(new CancellationToken(true));
        A.CallTo(() => _featureStore.PreviewDropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(canceledException);

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id, Json = json });

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceledException);

        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        jsonOutput.ToString()
            .Should().BeEmpty();

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeConfirmedThenDropCanceled_PropagatesAfterPreviewWithoutDroppedLines()
    {
        // Arrange
        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _featureStore.DropAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException(new CancellationToken(true)));

        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(new FeatureDropCommandOptions { FeatureId = Id });

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _console.Lines[0]
            .Should().Be($"Feature {Id} would be dropped. Files:");

        _console.Lines
            .Should().NotContain(x => x.StartsWith("Dropped feature", StringComparison.Ordinal))
            .And.NotContain("The local override pointed to this feature and was cleared.");
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Theory]
    [InlineData(0, "context")]
    [InlineData(1, "featureStore")]
    [InlineData(2, "confirmation")]
    public void Ctor_NullArgument_ThrowsArgumentNullException(int nullIndex, string parameterName)
    {
        // Act
        var act = () => new FeatureDropCommand(
            nullIndex == 0 ? null! : TestShadeKitCommandContext.Create(new TestConsole()),
            nullIndex == 1 ? null! : A.Fake<IFeatureStore>(),
            nullIndex == 2 ? null! : A.Fake<IConsoleConfirmation>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void FeatureDropCommand_CliCommandAttribute_IsRegisteredAsFeatureDropCommand()
    {
        // Act
        var attribute = typeof(FeatureDropCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("feature", "drop");
    }

    [Fact]
    public void FeatureDropCommandOptions_Attributes_DeclarePositionalIdAndYesSwitch()
    {
        // Act
        var idAttribute = typeof(FeatureDropCommandOptions)
            .GetProperty(nameof(FeatureDropCommandOptions.FeatureId))!
            .GetCustomAttribute<OptionValueAttribute>();
        var yesAttribute = typeof(FeatureDropCommandOptions)
            .GetProperty(nameof(FeatureDropCommandOptions.Yes))!
            .GetCustomAttribute<OptionParameterAttribute>();

        // Assert
        idAttribute
            .Should().NotBeNull();

        idAttribute.Index
            .Should().Be(0);

        idAttribute.IsRequired
            .Should().BeTrue();

        yesAttribute
            .Should().NotBeNull();

        yesAttribute.ShortName
            .Should().Be("y");

        yesAttribute.LongName
            .Should().Be("yes");

        typeof(FeatureDropCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    public void Dispose()
    {
        _console.Dispose();
        _cancellationSource.Dispose();
    }

    private FeatureDropCommand CreateCommand(TextWriter? jsonOutput = null, TestConsole? console = null)
    {
        var context = TestShadeKitCommandContext.Create(console ?? _console,
            kitStoreFactory: FeatureCommandTestHelper.CreateKitStoreFactory(_kitStore), jsonOutput: jsonOutput,
            cancellation: new TestCliCancellation(_cancellationSource.Token));

        return new FeatureDropCommand(context, _featureStore, _confirmation);
    }
}
