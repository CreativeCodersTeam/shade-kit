using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.New;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.SysConsole.Cli.Parsing;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace",
    Target = "~N:CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature.New",
    Justification = "The test namespace mirrors the command folder Commands/Feature/New/.")]

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature.New;

public class FeatureNewCommandTests
{
    private const string Id = FeatureCommandTestHelper.FeatureId;

    [Fact]
    public async Task ExecuteAsync_PlainMode_WritesCreatedLinePathsAndSuggestedBranch()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateFeatureStore(CreateResult()));

        // Act
        var result = await command.ExecuteAsync(CreateOptions());

        // Assert
        console.Lines
            .Should().Equal(
                $"Created feature {Id} (feature).",
                $"  .shadekit/features/{Id}/feature.json",
                $"  .shadekit/features/{Id}/request.md",
                $"Suggested branch: feature/{Id}");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PassesOptionsAndCurrentDirectoryInRequestToStoreOfFactory()
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        var featureStore = CreateFeatureStore(CreateResult());
        var command = CreateCommand(new TestConsole(), featureStore, FeatureCommandTestHelper.CreateKitStoreFactory(kitStore));

        // Act
        await command.ExecuteAsync(new FeatureNewCommandOptions
        {
            Source = "https://github.com/acme/shop/issues/142", Title = "Login SSO", Profile = "bugfix"
        });

        // Assert
        A.CallTo(() => featureStore.CreateAsync(kitStore,
                new NewFeatureRequest("https://github.com/acme/shop/issues/142", "Login SSO", "bugfix",
                    Directory.GetCurrentDirectory()),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_RunToken_IsPassedToFeatureStore()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var featureStore = CreateFeatureStore(CreateResult());
        var command = CreateCommand(new TestConsole(), featureStore,
            cancellation: new TestCliCancellation(cancellationSource.Token));

        // Act
        await command.ExecuteAsync(new FeatureNewCommandOptions { Source = "Login SSO via Azure" });

        // Assert
        A.CallTo(() => featureStore.CreateAsync(A<IKitStore>._, A<NewFeatureRequest>._, cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_CreateCanceled_PropagatesOperationCanceledExceptionWithoutOutput()
    {
        // Arrange
        var canceledException = new OperationCanceledException(new CancellationToken(true));
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.CreateAsync(A<IKitStore>._, A<NewFeatureRequest>._, A<CancellationToken>._))
            .ThrowsAsync(canceledException);

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), featureStore, jsonOutput: jsonOutput,
            cancellation: TestCliCancellation.Canceled);

        // Act
        var act = () => command.ExecuteAsync(new FeatureNewCommandOptions { Source = "Login SSO", Json = true });

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceledException);

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_TitleAndProfileOmitted_PassesNullForBoth()
    {
        // Arrange
        var featureStore = CreateFeatureStore(CreateResult());
        var command = CreateCommand(new TestConsole(), featureStore);

        // Act
        await command.ExecuteAsync(new FeatureNewCommandOptions { Source = "Login SSO via Azure" });

        // Assert
        A.CallTo(() => featureStore.CreateAsync(A<IKitStore>._,
                A<NewFeatureRequest>.That.Matches(x => x.Title == null && x.Profile == null &&
                                                       x.Source == "Login SSO via Azure"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeUnexpectedException_PropagatesWithoutEnvelope()
    {
        // Arrange
        var ioException = new IOException("disk error");
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateThrowingFeatureStore(ioException), jsonOutput: jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(CreateOptions(json: true));

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .Which.Should().BeSameAs(ioException);

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ProfileWithMarkupCharacters_WritesProfileVerbatim()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateFeatureStore(CreateResult(profile: "[bold]x[/]")));

        // Act
        await command.ExecuteAsync(CreateOptions());

        // Assert
        console.Lines[0]
            .Should().Be($"Created feature {Id} ([bold]x[/]).");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithFormatWarning_WritesWarningLineFirst()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console,
            CreateFeatureStore(CreateResult(warnings: [FeatureCommandTestHelper.UpgradeWarning])));

        // Act
        await command.ExecuteAsync(CreateOptions());

        // Assert
        console.Lines[0]
            .Should().StartWith("Warning: ").And.Contain(".shadekit/config.json");

        console.Lines[1]
            .Should().Be($"Created feature {Id} (feature).");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC1")]
    public async Task ExecuteAsync_JsonMode_WritesEnvelopeWithFeatureDataShape()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, CreateFeatureStore(CreateResult()), jsonOutput: jsonOutput);

        // Act
        var result = await command.ExecuteAsync(CreateOptions(json: true));

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var root = document.RootElement;
        var data = root.GetProperty("data");

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        FeatureCommandTestHelper.GetPropertyNames(data)
            .Should().Equal("id", "title", "profile", "status", "source", "paths", "suggestedBranch");

        data.GetProperty("id").GetString()
            .Should().Be(Id);

        data.GetProperty("title").GetString()
            .Should().Be("Login SSO");

        data.GetProperty("profile").GetString()
            .Should().Be("feature");

        data.GetProperty("status").GetString()
            .Should().Be("active");

        data.GetProperty("source").GetProperty("kind").GetString()
            .Should().Be("url");

        data.GetProperty("source").GetProperty("value").GetString()
            .Should().Be("https://github.com/acme/shop/issues/142");

        FeatureCommandTestHelper.GetPropertyNames(data.GetProperty("paths"))
            .Should().Equal("featureDir", "featureFile", "request");

        data.GetProperty("paths").GetProperty("featureDir").GetString()
            .Should().Be($".shadekit/features/{Id}/");

        data.GetProperty("paths").GetProperty("featureFile").GetString()
            .Should().Be($".shadekit/features/{Id}/feature.json");

        data.GetProperty("paths").GetProperty("request").GetString()
            .Should().Be($".shadekit/features/{Id}/request.md");

        data.GetProperty("suggestedBranch").GetString()
            .Should().Be($"feature/{Id}");

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        result.ExitCode
            .Should().Be(0);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeTextSource_WritesNullSourceValue()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            CreateFeatureStore(CreateResult(source: new FeatureSource { Kind = FeatureSourceKind.Text })),
            jsonOutput: jsonOutput);

        // Act
        await command.ExecuteAsync(CreateOptions(json: true));

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var source = document.RootElement.GetProperty("data").GetProperty("source");

        source.GetProperty("kind").GetString()
            .Should().Be("text");

        source.GetProperty("value").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithFormatWarning_WritesWarningIntoEnvelope()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            CreateFeatureStore(CreateResult(warnings: [FeatureCommandTestHelper.UpgradeWarning])),
            jsonOutput: jsonOutput);

        // Act
        await command.ExecuteAsync(CreateOptions(json: true));

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC3")]
    public async Task ExecuteAsync_FeatureAlreadyExists_ThrowsAbortExceptionWithFeatureAlreadyExistsExitCode()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateThrowingFeatureStore(new FeatureAlreadyExistsException(Id)));

        // Act
        var act = () => command.ExecuteAsync(CreateOptions());

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.FeatureAlreadyExists);

        console.Output
            .Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(MappedCreateFailures))]
    public async Task ExecuteAsync_JsonModeMappedFailure_WritesErrorEnvelopeWithMappedCode(Exception exception,
        int exitCode, string errorCode)
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateThrowingFeatureStore(exception), jsonOutput: jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(CreateOptions(json: true));

        // Assert
        var abortException = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        abortException.ExitCode
            .Should().Be(exitCode);

        abortException.PrintMessage
            .Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(errorCode);
    }

    public static TheoryData<Exception, int, string> MappedCreateFailures() =>
        new TheoryData<Exception, int, string>
    {
        { new FeatureAlreadyExistsException(Id), ShadeKitCliExitCodes.FeatureAlreadyExists, ShadeKitCliErrorCodes.FeatureAlreadyExists },
        { new InvalidFeatureSourceException(" ", "It is empty."), ShadeKitCliExitCodes.InvalidFeatureSource, ShadeKitCliErrorCodes.InvalidFeatureSource },
        { new InvalidFeatureTitleException("A title is required."), ShadeKitCliExitCodes.InvalidFeatureTitle, ShadeKitCliErrorCodes.InvalidFeatureTitle },
        { new UnknownProfileException("epic"), ShadeKitCliExitCodes.UnknownProfile, ShadeKitCliErrorCodes.UnknownProfile },
        { new GitIdentityNotConfiguredException("/repo", ["user.email"]), ShadeKitCliExitCodes.GitIdentityMissing, ShadeKitCliErrorCodes.GitIdentityMissing }
    };

    [Fact]
    public async Task ExecuteAsync_KitRootNotFound_ThrowsAbortExceptionAndDoesNotCallFeatureStore()
    {
        // Arrange
        var featureStore = CreateFeatureStore(CreateResult());
        var command = CreateCommand(new TestConsole(), featureStore,
            FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")));

        // Act
        var act = () => command.ExecuteAsync(CreateOptions());

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        A.CallTo(featureStore)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_SourceWithMarkupCharactersInvalid_EscapesAbortMessage()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(),
            CreateThrowingFeatureStore(new InvalidFeatureSourceException("[red]x", "No number.")));

        // Act
        var act = () => command.ExecuteAsync(CreateOptions());

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.Message.Should().Contain("'[[red]]x'");
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedException_PropagatesUnwrapped()
    {
        // Arrange
        var ioException = new IOException("disk error");
        var command = CreateCommand(new TestConsole(), CreateThrowingFeatureStore(ioException));

        // Act
        var act = () => command.ExecuteAsync(CreateOptions());

        // Assert
        (await act.Should().ThrowAsync<IOException>())
            .Which.Should().BeSameAs(ioException);
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), CreateFeatureStore(CreateResult()));

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
        var act = () => new FeatureNewCommand(
            nullIndex == 0 ? null! : TestShadeKitCommandContext.Create(new TestConsole()),
            nullIndex == 1 ? null! : A.Fake<IFeatureStore>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void FeatureNewCommand_CliCommandAttribute_IsRegisteredAsFeatureNewCommand()
    {
        // Act
        var attribute = typeof(FeatureNewCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("feature", "new");
    }

    [Theory]
    [InlineData(nameof(FeatureNewCommandOptions.Profile), "p", "profile", false)]
    [InlineData(nameof(FeatureNewCommandOptions.Source), "s", "source", true)]
    [InlineData(nameof(FeatureNewCommandOptions.Title), "t", "title", false)]
    public void FeatureNewCommandOptions_Property_HasOptionParameterWithNames(string propertyName, string shortName,
        string longName, bool isRequired)
    {
        // Act
        var attribute = typeof(FeatureNewCommandOptions).GetProperty(propertyName)!
            .GetCustomAttribute<OptionParameterAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.ShortName
            .Should().Be(shortName);

        attribute.LongName
            .Should().Be(longName);

        attribute.IsRequired
            .Should().Be(isRequired);
    }

    [Fact]
    public void FeatureNewCommandOptions_Type_DerivesFromJsonCommandOptions()
    {
        // Assert
        typeof(FeatureNewCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    private static FeatureNewCommandOptions CreateOptions(bool json = false)
    {
        return new FeatureNewCommandOptions
        {
            Source = "https://github.com/acme/shop/issues/142", Title = "Login SSO", Json = json
        };
    }

    private static NewFeatureResult CreateResult(string profile = WorkflowProfiles.Feature,
        FeatureSource? source = null, IReadOnlyList<KitWarning>? warnings = null)
    {
        var document = FeatureCommandTestHelper.CreateDocument(source: source);
        var feature = new FeatureDocument
        {
            SchemaVersion = document.SchemaVersion,
            MinEngineVersion = document.MinEngineVersion,
            Id = document.Id,
            Title = document.Title,
            Profile = profile,
            Source = document.Source,
            CreatedAt = document.CreatedAt,
            CreatedBy = document.CreatedBy,
            Status = document.Status
        };

        return new NewFeatureResult(feature, warnings ?? []);
    }

    private static IFeatureStore CreateFeatureStore(NewFeatureResult result)
    {
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.CreateAsync(A<IKitStore>._, A<NewFeatureRequest>._, A<CancellationToken>._))
            .Returns(result);

        return featureStore;
    }

    private static IFeatureStore CreateThrowingFeatureStore(Exception exception)
    {
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.CreateAsync(A<IKitStore>._, A<NewFeatureRequest>._, A<CancellationToken>._))
            .ThrowsAsync(exception);

        return featureStore;
    }

    private static FeatureNewCommand CreateCommand(IAnsiConsole console, IFeatureStore featureStore,
        IKitStoreFactory? kitStoreFactory = null, TextWriter? jsonOutput = null, ICliCancellation? cancellation = null)
    {
        var context = TestShadeKitCommandContext.Create(console,
            kitStoreFactory: kitStoreFactory ?? FeatureCommandTestHelper.CreateKitStoreFactory(),
            jsonOutput: jsonOutput, cancellation: cancellation);

        return new FeatureNewCommand(context, featureStore);
    }
}
