using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.List;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature.List;

public class FeatureListCommandTests
{
    [Fact]
    public async Task ExecuteAsync_FeaturesExist_WritesOneLinePerFeatureInStoreOrder()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, FeatureCommandTestHelper.CreateKitStoreFactory(),
            CreateFeatureStore(
                new FeatureSummary("Billing", "Billing run", "quick", FeatureStatus.Done, "finish"),
                new FeatureSummary("auth", "Auth", "bugfix", FeatureStatus.Active, "diagnose"),
                new FeatureSummary("login", "Login SSO", "feature", FeatureStatus.Active, "new")));

        // Act
        await command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                "Billing  done  quick  finish  Billing run",
                "auth  active  bugfix  diagnose  Auth",
                "login  active  feature  new  Login SSO");
    }

    [Fact]
    public async Task ExecuteAsync_FeaturesExist_ReturnsSuccess()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), FeatureCommandTestHelper.CreateKitStoreFactory(),
            CreateFeatureStore(new FeatureSummary("auth", "Auth", "feature", FeatureStatus.Active, "new")));

        // Act
        var result = await command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_TitleContainsMarkupCharacters_WritesTitleVerbatim()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, FeatureCommandTestHelper.CreateKitStoreFactory(),
            CreateFeatureStore(new FeatureSummary("login", "[red]feature[/]", "feature", FeatureStatus.Active, "new")));

        // Act
        await command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        console.Lines
            .Should().Equal("login  active  feature  new  [red]feature[/]");
    }

    [Fact]
    public async Task ExecuteAsync_NoFeatures_WritesHintAndReturnsSuccess()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, FeatureCommandTestHelper.CreateKitStoreFactory(), CreateFeatureStore());

        // Act
        var result = await command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        console.Lines
            .Should().Equal("No features found.");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_RunToken_IsPassedToFeatureStore()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var featureStore = CreateFeatureStore();
        var command = CreateCommand(new TestConsole(), FeatureCommandTestHelper.CreateKitStoreFactory(),
            featureStore, cancellation: new TestCliCancellation(cancellationSource.Token));

        // Act
        await command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        A.CallTo(() => featureStore.ListAsync(A<IKitStore>._, cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PassesStoreOfFactoryToFeatureStore()
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        var featureStore = CreateFeatureStore();
        var command = CreateCommand(new TestConsole(), FeatureCommandTestHelper.CreateKitStoreFactory(kitStore),
            featureStore);

        // Act
        await command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        A.CallTo(() => featureStore.ListAsync(kitStore, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithWarnings_WritesWarningLinesBeforeFeatures()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, FeatureCommandTestHelper.CreateKitStoreFactory(),
            CreateFeatureStore([new FeatureSummary("login", "Login", "feature", FeatureStatus.Active, "new")],
                [FeatureCommandTestHelper.UpgradeWarning, new FeatureFileMissingWarning("broken")]));

        // Act
        await command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        console.Lines.Count
            .Should().Be(3);

        console.Lines[0]
            .Should().StartWith("Warning: ").And.Contain("shade-kit upgrade");

        console.Lines[1]
            .Should().StartWith("Warning: ").And.Contain(".shadekit/features/broken/");

        console.Lines[2]
            .Should().Be("login  active  feature  new  Login");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    public async Task ExecuteAsync_KitRootNotFound_ThrowsAbortExceptionWithKitRootNotFoundExitCodeAndInitHint()
    {
        // Arrange
        var rootNotFoundException = new KitRootPathNotFoundException("/repo/src", "/repo");
        var console = new TestConsole();
        var command = CreateCommand(console,
            FeatureCommandTestHelper.CreateThrowingKitStoreFactory(rootNotFoundException));

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions());

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
    [Trait("Requirement", "FP01-AC2")]
    public async Task ExecuteAsync_NotAGitRepository_ThrowsAbortExceptionWithNotAGitRepositoryExitCodeAndEscapedPath()
    {
        // Arrange
        var notAGitRepositoryException = new NotAGitRepositoryException("/kit/[nested]/src");
        var console = new TestConsole();
        var command = CreateCommand(console,
            FeatureCommandTestHelper.CreateThrowingKitStoreFactory(notAGitRepositoryException));

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.NotAGitRepository);

        exception.IsError
            .Should().BeTrue();

        exception.InnerException
            .Should().BeSameAs(notAGitRepositoryException);

        exception.Message
            .Should().Contain("'/kit/[[nested]]/src'");

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FeatureStoreThrowsInvalidKitFile_ThrowsAbortExceptionWithInvalidFileExitCode()
    {
        // Arrange
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.ListAsync(A<IKitStore>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidKitFileException(".shadekit/features/login/feature.json", "broken"));

        var console = new TestConsole();
        var command = CreateCommand(console, FeatureCommandTestHelper.CreateKitStoreFactory(), featureStore);

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.InvalidFile);

        console.Output
            .Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(PhaseReadFailures))]
    public async Task ExecuteAsync_JsonModeFeatureStoreThrowsPhaseReadFailure_WritesErrorEnvelopeWithMappedCode(
        Exception exception, int exitCode, string errorCode)
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.ListAsync(A<IKitStore>._, A<CancellationToken>._))
            .ThrowsAsync(exception);

        var command = CreateCommand(new TestConsole(), FeatureCommandTestHelper.CreateKitStoreFactory(),
            featureStore, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions { Json = true });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(exitCode);

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(errorCode);
    }

    public static TheoryData<Exception, int, string> PhaseReadFailures() =>
        new TheoryData<Exception, int, string>
        {
            {
                new UnknownProfileException("legacy", ["feature", "bugfix", "quick"],
                    ".shadekit/features/login/feature.json"),
                ShadeKitCliExitCodes.UnknownProfile, ShadeKitCliErrorCodes.UnknownProfile
            },
            {
                new InvalidPhaseStateException(".shadekit/features/login/feature.json", "currentPhase",
                    "names the phase 'review', which is not a phase of the workflow profile 'feature'."),
                ShadeKitCliExitCodes.PhaseStateInvalid, ShadeKitCliErrorCodes.PhaseStateInvalid
            }
        };

    [Fact]
    public async Task ExecuteAsync_FeatureStoreThrowsFormatTooNew_ThrowsAbortExceptionWithFormatTooNewExitCode()
    {
        // Arrange
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.ListAsync(A<IKitStore>._, A<CancellationToken>._))
            .ThrowsAsync(new FormatTooNewException(".shadekit/features/login/feature.json", 9, 1, "9.0.0"));

        var command = CreateCommand(new TestConsole(), FeatureCommandTestHelper.CreateKitStoreFactory(),
            featureStore);

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.FormatTooNew);
    }

    [Fact]
    public async Task ExecuteAsync_FactoryThrowsDirectoryNotFound_PropagatesExceptionUnwrapped()
    {
        // Arrange
        var directoryNotFoundException = new DirectoryNotFoundException("current directory deleted");
        var console = new TestConsole();
        var command = CreateCommand(console,
            FeatureCommandTestHelper.CreateThrowingKitStoreFactory(directoryNotFoundException));

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions());

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
        var command = CreateCommand(new TestConsole(),
            FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")));
        Task<CommandResult>? task = null;

        // Act
        var act = () => { task = command.ExecuteAsync(new FeatureListCommandOptions()); };

        // Assert
        act.Should().NotThrow();

        task
            .Should().NotBeNull();

        task.IsFaulted
            .Should().BeTrue();

        task.Exception
            .Should().NotBeNull();

        task.Exception.InnerException
            .Should().BeOfType<CliCommandAbortException>();
    }

    [Fact]
    public async Task ExecuteAsync_FeatureStoreThrowsUnexpectedIoException_PropagatesExceptionUnwrappedWithoutOutput()
    {
        // Arrange
        var ioException = new IOException("disk error");
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.ListAsync(A<IKitStore>._, A<CancellationToken>._)).ThrowsAsync(ioException);

        var console = new TestConsole();
        var command = CreateCommand(console, FeatureCommandTestHelper.CreateKitStoreFactory(), featureStore);

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .Which.Should().BeSameAs(ioException);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FactoryThrowsUnexpectedAccessException_PropagatesExceptionUnwrapped()
    {
        // Arrange
        var accessException = new UnauthorizedAccessException("access denied");
        var command = CreateCommand(new TestConsole(),
            FeatureCommandTestHelper.CreateThrowingKitStoreFactory(accessException));

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .Which.Should().BeSameAs(accessException);
    }

    [Fact]
    public void Ctor_NullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeatureListCommand(null!, A.Fake<IFeatureStore>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Ctor_NullFeatureStore_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeatureListCommand(TestShadeKitCommandContext.Create(new TestConsole()), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("featureStore");
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), FeatureCommandTestHelper.CreateKitStoreFactory(),
            CreateFeatureStore());

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC12")]
    public async Task ExecuteAsync_JsonModeWithFeatures_WritesEnvelopeWithFeaturesInStoreOrder()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, FeatureCommandTestHelper.CreateKitStoreFactory(),
            CreateFeatureStore(
                new FeatureSummary("Billing", "Billing run", "quick", FeatureStatus.Done, "finish"),
                new FeatureSummary("auth", "Auth", "bugfix", FeatureStatus.Active, "diagnose")),
            jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new FeatureListCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var root = document.RootElement;

        FeatureCommandTestHelper.GetPropertyNames(root)
            .Should().Equal("ok", "data", "warnings", "error");

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        var features = root.GetProperty("data").GetProperty("features").EnumerateArray().ToArray();

        features.Select(FeatureCommandTestHelper.GetPropertyNames)
            .Should().AllSatisfy(x => x.Should().Equal("id", "title", "profile", "status", "currentPhase"));

        features.Select(x => x.GetProperty("id").GetString())
            .Should().Equal("Billing", "auth");

        features[0].GetProperty("title").GetString()
            .Should().Be("Billing run");

        features[0].GetProperty("profile").GetString()
            .Should().Be("quick");

        features.Select(x => x.GetProperty("status").GetString())
            .Should().Equal("done", "active");

        features.Select(x => x.GetProperty("currentPhase").GetString())
            .Should().Equal("finish", "diagnose");

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

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
        var command = CreateCommand(new TestConsole(), FeatureCommandTestHelper.CreateKitStoreFactory(),
            CreateFeatureStore(), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new FeatureListCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        document.RootElement.GetProperty("data").GetProperty("features").GetArrayLength()
            .Should().Be(0);

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithWarnings_WritesWarningsInResultOrderIntoEnvelope()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, FeatureCommandTestHelper.CreateKitStoreFactory(),
            CreateFeatureStore([], [FeatureCommandTestHelper.UpgradeWarning, new FeatureFileMissingWarning("broken")]),
            jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureListCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded, ShadeKitCliWarningCodes.FeatureFileMissing);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC2")]
    [Trait("Requirement", "FP01-AC8")]
    [Trait("Requirement", "FP01-AC12")]
    public async Task ExecuteAsync_JsonModeKitRootNotFound_WritesErrorEnvelopeAndAbortsWithoutPrintedMessage()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console,
            FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")),
            jsonOutput: jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions { Json = true });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        exception.PrintMessage
            .Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var root = document.RootElement;

        FeatureCommandTestHelper.GetPropertyNames(root)
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
    public async Task ExecuteAsync_JsonModeNotAGitRepository_WritesErrorEnvelopeWithNotAGitRepositoryCode()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            FeatureCommandTestHelper.CreateThrowingKitStoreFactory(new NotAGitRepositoryException("/outside")),
            jsonOutput: jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions { Json = true });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.NotAGitRepository);

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.NotAGitRepository);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeUnexpectedException_PropagatesExceptionWithoutOutput()
    {
        // Arrange
        var ioException = new IOException("disk error");
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            FeatureCommandTestHelper.CreateThrowingKitStoreFactory(ioException), jsonOutput: jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions { Json = true });

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
        var command = CreateCommand(new TestConsole(), FeatureCommandTestHelper.CreateKitStoreFactory(),
            CreateFeatureStore(new FeatureSummary("auth", "Auth", "feature", FeatureStatus.Active, "new")), jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureListCommandOptions());

        // Assert
        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeTitleWithMarkupCharacters_WritesTitleVerbatim()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), FeatureCommandTestHelper.CreateKitStoreFactory(),
            CreateFeatureStore(new FeatureSummary("login", "[red]feature[/]", "feature", FeatureStatus.Active, "new")),
            jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureListCommandOptions { Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("data").GetProperty("features")[0].GetProperty("title").GetString()
            .Should().Be("[red]feature[/]");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModePathWithMarkupCharacters_KeepsRawPathInEnvelopeAndAbortMessage()
    {
        // Arrange
        var exception = new NotAGitRepositoryException("/kit/[nested]/src");
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), FeatureCommandTestHelper.CreateThrowingKitStoreFactory(exception),
            jsonOutput: jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureListCommandOptions { Json = true });

        // Assert
        var abortException = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        abortException.Message
            .Should().Contain("'/kit/[nested]/src'");

        abortException.PrintMessage
            .Should().BeFalse();

        abortException.IsError
            .Should().BeTrue();

        abortException.InnerException
            .Should().BeSameAs(exception);

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var root = document.RootElement;

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("'/kit/[nested]/src'");

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        FeatureCommandTestHelper.GetPropertyNames(root.GetProperty("error"))
            .Should().Equal("code", "message");
    }

    [Fact]
    public void FeatureListCommandOptions_Type_DerivesFromJsonCommandOptions()
    {
        // Assert
        typeof(FeatureListCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    [Fact]
    public void FeatureListCommand_CliCommandAttribute_IsRegisteredAsFeatureListCommand()
    {
        // Act
        var attribute = typeof(FeatureListCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("feature", "list");
    }

    [Fact]
    public void CliAssembly_CliCommandGroupAttributes_ContainFeaturesGroup()
    {
        // Act
        var groupAttributes = typeof(FeatureListCommand).Assembly
            .GetCustomAttributes<CliCommandGroupAttribute>();

        // Assert
        groupAttributes
            .Where(x => x.Commands is ["feature"])
            .Should().ContainSingle();
    }

    private static IFeatureStore CreateFeatureStore(params FeatureSummary[] features)
    {
        return CreateFeatureStore(features, []);
    }

    private static IFeatureStore CreateFeatureStore(IReadOnlyList<FeatureSummary> features,
        IReadOnlyList<KitWarning> warnings)
    {
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.ListAsync(A<IKitStore>._, A<CancellationToken>._))
            .Returns(new FeatureListResult(features, warnings));

        return featureStore;
    }

    internal static FeatureListCommand CreateCommand(IAnsiConsole console, IKitStoreFactory kitStoreFactory,
        IFeatureStore? featureStore = null, TextWriter? jsonOutput = null, ICliCancellation? cancellation = null)
    {
        var context = TestShadeKitCommandContext.Create(console, kitStoreFactory: kitStoreFactory,
            jsonOutput: jsonOutput, cancellation: cancellation);

        return new FeatureListCommand(context, featureStore ?? CreateFeatureStore());
    }
}
