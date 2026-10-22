using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.SysConsole.Cli.Parsing;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature.Show;

public class FeatureShowCommandTests
{
    private const string Id = FeatureCommandTestHelper.FeatureId;

    [Fact]
    public async Task ExecuteAsync_PlainMode_WritesKeyValueLines()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console, CreateFeatureStore(CreateResult()));

        // Act
        var result = await command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id });

        // Assert
        console.Lines
            .Should().Equal(
                $"Id: {Id}",
                "Title: Login SSO",
                "Profile: feature",
                "Status: active",
                "Source: url https://github.com/acme/shop/issues/142",
                "Created: 2026-10-20T15:30:00+02:00 by Jane Doe <jane@example.com>",
                $"Feature directory: .shadekit/features/{Id}/",
                $"Feature file: .shadekit/features/{Id}/feature.json",
                $"Request: .shadekit/features/{Id}/request.md");

        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeTextSource_WritesKindWithoutValue()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console,
            CreateFeatureStore(CreateResult(new FeatureSource { Kind = FeatureSourceKind.Text })));

        // Act
        await command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id });

        // Assert
        console.Lines
            .Should().Contain("Source: text");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeValueWithMarkupCharacters_WritesValueVerbatim()
    {
        // Arrange
        var console = new TestConsole();
        var command = CreateCommand(console,
            CreateFeatureStore(CreateResult(new FeatureSource { Kind = FeatureSourceKind.File, Value = "[docs]/a.md" })));

        // Act
        await command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id });

        // Assert
        console.Lines
            .Should().Contain("Source: file [docs]/a.md");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithFormatWarning_WritesWarningFirst()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console,
            CreateFeatureStore(CreateResult(warnings: [FeatureCommandTestHelper.UpgradeWarning])));

        // Act
        await command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id });

        // Assert
        console.Lines[0]
            .Should().StartWith("Warning: ");

        console.Lines[1]
            .Should().Be($"Id: {Id}");
    }

    [Fact]
    public async Task ExecuteAsync_FeatureFileWithOtherId_WritesFolderIdAndIdMismatchWarning()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        using var jsonOutput = new StringWriter();
        var result = CreateResult(warnings: [new FeatureIdMismatchWarning(Id, "recorded-id")]) with
        {
            Feature = FeatureCommandTestHelper.CreateDocument(id: "recorded-id")
        };
        var plainCommand = CreateCommand(console, CreateFeatureStore(result));
        var jsonCommand = CreateCommand(new TestConsole(), CreateFeatureStore(result), jsonOutput);

        // Act
        await plainCommand.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id });
        await jsonCommand.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id, Json = true });

        // Assert
        console.Lines[0]
            .Should().Be($"Warning: '.shadekit/features/{Id}/feature.json' has the id 'recorded-id'; the folder " +
                         $"name '{Id}' is used.");

        console.Lines[1]
            .Should().Be($"Id: {Id}");

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("data").GetProperty("id").GetString()
            .Should().Be(Id);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliWarningCodes.FeatureIdMismatch);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC9")]
    public async Task ExecuteAsync_JsonMode_WritesEnvelopeWithFeatureDataShapeAndCreator()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console,
            CreateFeatureStore(CreateResult(warnings: [FeatureCommandTestHelper.UpgradeWarning])), jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id, Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);
        var data = document.RootElement.GetProperty("data");

        FeatureCommandTestHelper.GetPropertyNames(data)
            .Should().Equal("id", "title", "profile", "status", "source", "createdAt", "createdBy", "paths");

        data.GetProperty("id").GetString()
            .Should().Be(Id);

        data.GetProperty("status").GetString()
            .Should().Be("active");

        data.GetProperty("source").GetProperty("kind").GetString()
            .Should().Be("url");

        data.GetProperty("createdAt").GetDateTimeOffset()
            .Should().Be(new DateTimeOffset(2026, 10, 20, 15, 30, 0, TimeSpan.FromHours(2)));

        data.GetProperty("createdAt").GetString()
            .Should().EndWith("+02:00");

        FeatureCommandTestHelper.GetPropertyNames(data.GetProperty("createdBy"))
            .Should().Equal("name", "email");

        data.GetProperty("createdBy").GetProperty("name").GetString()
            .Should().Be("Jane Doe");

        data.GetProperty("createdBy").GetProperty("email").GetString()
            .Should().Be("jane@example.com");

        data.GetProperty("paths").GetProperty("request").GetString()
            .Should().Be($".shadekit/features/{Id}/request.md");

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeSourceExtensionData_WritesOnlyKindAndValue()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var source = new FeatureSource
        {
            Kind = FeatureSourceKind.Url,
            Value = "https://example.com/x",
            ExtensionData = new Dictionary<string, JsonElement> { ["future"] = JsonDocument.Parse("1").RootElement }
        };
        var command = CreateCommand(new TestConsole(), CreateFeatureStore(CreateResult(source)), jsonOutput);

        // Act
        await command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id, Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetPropertyNames(document.RootElement.GetProperty("data").GetProperty("source"))
            .Should().Equal("kind", "value");
    }

    [Theory]
    [MemberData(nameof(MappedShowFailures))]
    public async Task ExecuteAsync_JsonModeMappedFailure_WritesErrorEnvelopeWithMappedCode(Exception exception,
        int exitCode, string errorCode)
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.ShowAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(exception);

        var command = CreateCommand(new TestConsole(), featureStore, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id, Json = true });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(exitCode);

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(errorCode);
    }

    public static TheoryData<Exception, int, string> MappedShowFailures() => new()
    {
        { new FeatureNotFoundException(Id), ShadeKitCliExitCodes.FeatureNotFound, ShadeKitCliErrorCodes.FeatureNotFound },
        { new InvalidFeatureIdException("Bad"), ShadeKitCliExitCodes.InvalidFeatureId, ShadeKitCliErrorCodes.InvalidFeatureId },
        { new InvalidKitFileException(".shadekit/features/x/feature.json", "missing"), ShadeKitCliExitCodes.InvalidFile, ShadeKitCliErrorCodes.InvalidFile }
    };

    [Fact]
    public async Task ExecuteAsync_RunToken_IsPassedToFeatureStore()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var featureStore = CreateFeatureStore(CreateResult());
        var command = CreateCommand(new TestConsole(), featureStore,
            cancellation: new TestCliCancellation(cancellationSource.Token));

        // Act
        await command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id });

        // Assert
        A.CallTo(() => featureStore.ShowAsync(A<IKitStore>._, Id, cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PassesIdAndStoreOfFactoryToFeatureStore()
    {
        // Arrange
        var kitStore = A.Fake<IKitStore>();
        var featureStore = CreateFeatureStore(CreateResult());
        var context = TestShadeKitCommandContext.Create(new TestConsole(),
            kitStoreFactory: FeatureCommandTestHelper.CreateKitStoreFactory(kitStore));
        var command = new FeatureShowCommand(context, featureStore);

        // Act
        await command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id });

        // Assert
        A.CallTo(() => featureStore.ShowAsync(kitStore, Id, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeFeatureNotFound_ThrowsPrintedAbortExceptionWithoutOutput()
    {
        // Arrange
        var console = new TestConsole();
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.ShowAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new FeatureNotFoundException("unknown-id"));

        var command = CreateCommand(console, featureStore);

        // Act
        var act = () => command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = "unknown-id" });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.FeatureNotFound);

        exception.PrintMessage
            .Should().BeTrue();

        exception.Message
            .Should().Contain("'unknown-id'");

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_KitRootNotFound_ThrowsAbortExceptionWithoutCallingFeatureStore()
    {
        // Arrange
        var featureStore = A.Fake<IFeatureStore>();
        var context = TestShadeKitCommandContext.Create(new TestConsole(),
            kitStoreFactory: FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")));
        var command = new FeatureShowCommand(context, featureStore);

        // Act
        var act = () => command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        A.CallTo(featureStore)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWarningWithMarkupCharacters_WritesWarningVerbatim()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateFeatureStore(CreateResult(warnings:
            [new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/features/[x]/feature.json", 1, 2)])));

        // Act
        await command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id });

        // Assert
        console.Lines[0]
            .Should().StartWith("Warning: ").And.Contain(".shadekit/features/[x]/feature.json");
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeUtcCreatedAtWithFractionalSecondsUnderOtherCulture_WritesIsoSecondsWithOffset()
    {
        // Arrange
        var console = new TestConsole();
        var document = FeatureCommandTestHelper.CreateDocument();
        var feature = new FeatureDocument
        {
            SchemaVersion = document.SchemaVersion,
            MinEngineVersion = document.MinEngineVersion,
            Id = document.Id,
            Title = document.Title,
            Profile = document.Profile,
            Source = document.Source,
            CreatedAt = new DateTimeOffset(2026, 10, 20, 13, 30, 5, 123, TimeSpan.Zero),
            CreatedBy = document.CreatedBy
        };
        var command = CreateCommand(console,
            CreateFeatureStore(new FeatureShowResult(Id, feature, [])));

        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fi-FI");

        try
        {
            // Act
            await command.ExecuteAsync(new FeatureShowCommandOptions { FeatureId = Id });
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        // Assert
        console.Lines
            .Should().Contain("Created: 2026-10-20T13:30:05+00:00 by Jane Doe <jane@example.com>");
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
        var act = () => new FeatureShowCommand(
            nullIndex == 0 ? null! : TestShadeKitCommandContext.Create(new TestConsole()),
            nullIndex == 1 ? null! : A.Fake<IFeatureStore>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void FeatureShowCommand_CliCommandAttribute_IsRegisteredAsFeatureShowCommand()
    {
        // Act
        var attribute = typeof(FeatureShowCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("feature", "show");
    }

    [Fact]
    public void FeatureShowCommandOptions_FeatureId_IsRequiredFirstPositionalValue()
    {
        // Act
        var attribute = typeof(FeatureShowCommandOptions).GetProperty(nameof(FeatureShowCommandOptions.FeatureId))!
            .GetCustomAttribute<OptionValueAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Index
            .Should().Be(0);

        attribute.IsRequired
            .Should().BeTrue();

        typeof(FeatureShowCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    private static FeatureShowResult CreateResult(FeatureSource? source = null,
        IReadOnlyList<KitWarning>? warnings = null)
    {
        return new FeatureShowResult(Id, FeatureCommandTestHelper.CreateDocument(source: source), warnings ?? []);
    }

    private static IFeatureStore CreateFeatureStore(FeatureShowResult result)
    {
        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.ShowAsync(A<IKitStore>._, A<string>._, A<CancellationToken>._)).Returns(result);

        return featureStore;
    }

    private static FeatureShowCommand CreateCommand(IAnsiConsole console, IFeatureStore featureStore,
        TextWriter? jsonOutput = null, ICliCancellation? cancellation = null)
    {
        var context = TestShadeKitCommandContext.Create(console,
            kitStoreFactory: FeatureCommandTestHelper.CreateKitStoreFactory(), jsonOutput: jsonOutput,
            cancellation: cancellation);

        return new FeatureShowCommand(context, featureStore);
    }
}
