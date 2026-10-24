using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.Set;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Core.Warnings;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

public sealed class ShadeKitCommandContextTests : IDisposable
{
    private static readonly KitRootPathNotFoundException KitRootNotFound =
        new KitRootPathNotFoundException("/repo/src", "/repo");

    private readonly TestConsole _console = FeatureCommandTestHelper.CreateWideConsole();

    private readonly StringWriter _jsonOutput = new StringWriter();

    [Theory]
    [InlineData(0, "ansiConsole")]
    [InlineData(1, "kitStoreFactory")]
    [InlineData(2, "jsonWriter")]
    [InlineData(3, "errorMapper")]
    [InlineData(4, "cancellation")]
    public void Ctor_NullArgument_ThrowsArgumentNullException(int nullIndex, string parameterName)
    {
        // Act
        var act = () => new ShadeKitCommandContext(
            nullIndex == 0 ? null! : new TestConsole(),
            nullIndex == 1 ? null! : A.Fake<IKitStoreFactory>(),
            nullIndex == 2 ? null! : A.Fake<ICliJsonWriter>(),
            nullIndex == 3 ? null! : CliErrorMapperTests.CreateMapper(),
            nullIndex == 4 ? null! : TestCliCancellation.None);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void Console_Created_ReturnsGivenConsole()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var console = context.Console;

        // Assert
        console
            .Should().BeSameAs(_console);
    }

    [Fact]
    public void CancellationToken_Created_ReturnsTokenOfCancellation()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var context = CreateContext(cancellation: new TestCliCancellation(cancellationSource.Token));

        // Act
        var token = context.CancellationToken;

        // Assert
        token
            .Should().Be(cancellationSource.Token);
    }

    [Fact]
    public void CreateKitStore_StoreFound_ReturnsStoreOfFactory()
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        var context = CreateContext(FeatureCommandTestHelper.CreateKitStoreFactory(kitStore));

        // Act
        var result = context.CreateKitStore(false);

        // Assert
        result
            .Should().BeSameAs(kitStore);
    }

    [Fact]
    public void CreateKitStore_PlainModeKitRootNotFound_ThrowsPrintedAbortExceptionWithoutJson()
    {
        // Arrange
        var context = CreateContext(FeatureCommandTestHelper.CreateThrowingKitStoreFactory(KitRootNotFound));

        // Act
        var act = () => context.CreateKitStore(false);

        // Assert
        var exception = act.Should().Throw<CliCommandAbortException>().Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.PrintMessage
            .Should().BeTrue();

        exception.InnerException
            .Should().BeSameAs(KitRootNotFound);

        _jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public void CreateKitStore_JsonModeKitRootNotFound_WritesFailureEnvelopeAndThrowsSilentAbortException()
    {
        // Arrange
        var context = CreateContext(FeatureCommandTestHelper.CreateThrowingKitStoreFactory(KitRootNotFound));

        // Act
        var act = () => context.CreateKitStore(true);

        // Assert
        act.Should().Throw<CliCommandAbortException>()
            .Which.PrintMessage.Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(_jsonOutput);

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.KitRootNotFound);
    }

    [Fact]
    public async Task RunAsync_Operation_PassesTokenOfRunAndReturnsResult()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var context = CreateContext(cancellation: new TestCliCancellation(cancellationSource.Token));
        CancellationToken passedToken = default;

        // Act
        var result = await context.RunAsync(false, cancellationToken =>
        {
            passedToken = cancellationToken;

            return Task.FromResult(42);
        });

        // Assert
        result
            .Should().Be(42);

        passedToken
            .Should().Be(cancellationSource.Token);
    }

    [Fact]
    public async Task RunAsync_PlainModeExpectedFailure_ThrowsPrintedAbortExceptionWithoutJson()
    {
        // Arrange
        var failure = new FeatureNotFoundException("unknown-id");
        var context = CreateContext();

        // Act
        var act = () => context.RunAsync<int>(false, _ => Task.FromException<int>(failure));

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.FeatureNotFound);

        exception.PrintMessage
            .Should().BeTrue();

        exception.InnerException
            .Should().BeSameAs(failure);

        _jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_JsonModeExpectedFailure_WritesFailureEnvelopeAndThrowsSilentAbortException()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = () => context.RunAsync<int>(true,
            _ => Task.FromException<int>(new FeatureNotFoundException("unknown-id")));

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.FeatureNotFound);

        exception.PrintMessage
            .Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(_jsonOutput);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.FeatureNotFound);
    }

    [Fact]
    public async Task RunAsync_UnexpectedFailure_LetsExceptionEscapeWithoutJson()
    {
        // Arrange
        var failure = new InvalidOperationException("Unexpected.");
        var context = CreateContext();

        // Act
        var act = () => context.RunAsync<int>(true, _ => Task.FromException<int>(failure));

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(failure);

        _jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_NullOperation_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = () => context.RunAsync(false, (Func<CancellationToken, Task<int>>)null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("operation");
    }

    [Fact]
    public async Task RunAsync_WithKitStore_PassesStoreOfFactoryAndTokenOfRun()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var kitStore = A.Fake<IKitStore>();
        var context = CreateContext(FeatureCommandTestHelper.CreateKitStoreFactory(kitStore),
            new TestCliCancellation(cancellationSource.Token));
        IKitStore? passedStore = null;
        CancellationToken passedToken = default;

        // Act
        var result = await context.RunAsync(false, (store, cancellationToken) =>
        {
            passedStore = store;
            passedToken = cancellationToken;

            return Task.FromResult("done");
        });

        // Assert
        result
            .Should().Be("done");

        passedStore
            .Should().BeSameAs(kitStore);

        passedToken
            .Should().Be(cancellationSource.Token);
    }

    [Fact]
    public async Task RunAsync_WithKitStoreJsonModeKitRootNotFound_WritesFailureEnvelopeWithoutCallingOperation()
    {
        // Arrange
        var context = CreateContext(FeatureCommandTestHelper.CreateThrowingKitStoreFactory(KitRootNotFound));
        var called = false;

        // Act
        var act = () => context.RunAsync(true, (_, _) =>
        {
            called = true;

            return Task.FromResult(0);
        });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.PrintMessage
            .Should().BeFalse();

        called
            .Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(_jsonOutput);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.KitRootNotFound);
    }

    [Fact]
    public async Task RunAsync_WithKitStoreNullOperation_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = () => context.RunAsync(false, (Func<IKitStore, CancellationToken, Task<int>>)null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("operation");
    }

    [Fact]
    public void WriteSuccess_JsonModeWithWarnings_WritesOneEnvelopeWithDataAndWarningsInOrder()
    {
        // Arrange
        var context = CreateContext();
        var called = false;

        // Act
        context.WriteSuccess(true, new FeatureSetCommandData("gh-1-x"), CreateWarnings(), () => called = true);

        // Assert
        called
            .Should().BeFalse();

        _console.Output
            .Should().BeEmpty();

        using var document = FeatureCommandTestHelper.ParseOutput(_jsonOutput);
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        root.GetProperty("data").GetProperty("id").GetString()
            .Should().Be("gh-1-x");

        FeatureCommandTestHelper.GetWarningCodes(root)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded, ShadeKitCliWarningCodes.CurrentFeatureMissing);

        root.GetProperty("error").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void WriteSuccess_PlainModeWithWarnings_WritesWarningLinesBeforePlainLinesWithoutJson()
    {
        // Arrange
        var context = CreateContext();
        var warnings = CreateWarnings();

        // Act
        context.WriteSuccess(false, new FeatureSetCommandData("gh-1-x"), warnings,
            () => _console.WriteLine("Result [red]line[/]"));

        // Assert
        _console.Lines
            .Should().Equal(
                $"Warning: {CliErrorMapper.MapWarning(warnings[0]).Message}",
                $"Warning: {CliErrorMapper.MapWarning(warnings[1]).Message}",
                "Result [red]line[/]");

        _jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public void WriteSuccess_JsonModeWithoutWarnings_WritesEnvelopeWithEmptyWarnings()
    {
        // Arrange
        var context = CreateContext();

        // Act
        context.WriteSuccess(true, new FeatureSetCommandData("gh-1-x"), () => { });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(_jsonOutput);

        document.RootElement.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);
    }

    [Fact]
    public void WriteSuccess_PlainModeWithoutWarnings_WritesOnlyPlainLines()
    {
        // Arrange
        var context = CreateContext();

        // Act
        context.WriteSuccess(false, new FeatureSetCommandData("gh-1-x"), () => _console.WriteLine("Done."));

        // Assert
        _console.Lines
            .Should().Equal("Done.");
    }

    [Theory]
    [InlineData(0, "data")]
    [InlineData(1, "warnings")]
    [InlineData(2, "writePlain")]
    public void WriteSuccess_NullArgument_ThrowsArgumentNullException(int nullIndex, string parameterName)
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = () => context.WriteSuccess(false,
            nullIndex == 0 ? null! : new FeatureSetCommandData("gh-1-x"),
            nullIndex == 1 ? null! : CreateWarnings(),
            nullIndex == 2 ? null! : () => { });

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    public void Dispose()
    {
        _console.Dispose();
        _jsonOutput.Dispose();
    }

    private static KitWarning[] CreateWarnings()
    {
        return [FeatureCommandTestHelper.UpgradeWarning, new CurrentFeatureMissingWarning("gone")];
    }

    private ShadeKitCommandContext CreateContext(IKitStoreFactory? kitStoreFactory = null,
        ICliCancellation? cancellation = null)
    {
        return TestShadeKitCommandContext.Create(_console, kitStoreFactory, _jsonOutput, cancellation);
    }
}
