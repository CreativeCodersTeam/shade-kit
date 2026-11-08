using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Gate;
using CreativeCoders.ShadeKit.Cli.Commands.Gate.Info;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;
using CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;
using CreativeCoders.SysConsole.Cli.Parsing;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Gate.Info;

public class GateInfoCommandTests
{
    private const string Id = PhaseCommandTestHelper.FeatureId;

    private static readonly GateInfoGate DefinitionOfReadyGate = new GateInfoGate("plan", true,
        "checks/definition-of-ready.md", $".shadekit/features/{Id}/checks/definition-of-ready.md", true);

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ExecuteAsync_Json_WritesEnvelopeWithFeaturePhasesGateAndThreshold()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateReader(CreateResult(DefinitionOfReadyGate, 80.5)),
            jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "plan", Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetPropertyNames(document.RootElement)
            .Should().Equal("ok", "data", "warnings", "error");

        document.RootElement.GetProperty("data").ShouldBeJson(
            """
            {
              "featureId": "gh-142-login-sso", "profile": "feature",
              "featureDir": ".shadekit/features/gh-142-login-sso/",
              "phases": [
                { "name": "new", "state": "approved" }, { "name": "refine", "state": "approved" },
                { "name": "map", "state": "in-progress" }, { "name": "plan", "state": "pending" },
                { "name": "implement", "state": "pending" }, { "name": "verify", "state": "pending" },
                { "name": "finish", "state": "pending" }
              ],
              "gate": { "phase": "plan", "approval": true, "checklist": "checks/definition-of-ready.md",
                        "checklistPath": ".shadekit/features/gh-142-login-sso/checks/definition-of-ready.md",
                        "checklistExists": true },
              "coverageThreshold": 80.5
            }
            """);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ExecuteAsync_PlainText_WritesFeatureGateAndThreshold()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateReader(CreateResult(DefinitionOfReadyGate, 80.5)));

        // Act
        var result = await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "plan" });

        // Assert
        result.ExitCode
            .Should().Be(0);

        console.Lines
            .Should().Equal(
                $"Feature: {Id} (profile feature)",
                $"Feature directory: .shadekit/features/{Id}/",
                "Phases:",
                "  new: approved",
                "  refine: approved",
                "  map: in-progress",
                "  plan: pending",
                "  implement: pending",
                "  verify: pending",
                "  finish: pending",
                "Gate: plan (approval: required, checklist: checks/definition-of-ready.md)",
                $"Checklist copy: .shadekit/features/{Id}/checks/definition-of-ready.md (exists)",
                "Coverage threshold: 80.5");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ExecuteAsync_PlainTextMissingChecklistCopy_WritesMissing()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var gate = DefinitionOfReadyGate with { ChecklistExists = false };
        var command = CreateCommand(console, CreateReader(CreateResult(gate, 80)));

        // Act
        await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "plan" });

        // Assert
        console.Lines
            .Should().Contain($"Checklist copy: .shadekit/features/{Id}/checks/definition-of-ready.md (missing)")
            .And.Contain("Coverage threshold: 80");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ExecuteAsync_JsonGateWithoutChecklist_WritesChecklistFieldsNull()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var gate = new GateInfoGate("refine", false, null, null, null);
        var command = CreateCommand(new TestConsole(), CreateReader(CreateResult(gate, null)), jsonOutput);

        // Act
        await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "refine", Json = true });

        // Assert
        var data = PhaseCommandTestHelper.GetData(jsonOutput);

        data.GetProperty("gate").ShouldBeJson(
            """
            { "phase": "refine", "approval": false, "checklist": null, "checklistPath": null, "checklistExists": null }
            """);

        data.GetProperty("coverageThreshold").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ExecuteAsync_PlainTextGateWithoutChecklistAndNoThreshold_WritesNoChecklistCopyAndNotSet()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var gate = new GateInfoGate("refine", false, null, null, null);
        var command = CreateCommand(console, CreateReader(CreateResult(gate, null)));

        // Act
        await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "refine" });

        // Assert
        console.Lines.Skip(10)
            .Should().Equal(
                "Gate: refine (approval: not required, checklist: none)",
                "Coverage threshold: not set");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task ExecuteAsync_NullGate_WritesGateNull()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), CreateReader(CreateResult(null, null)), jsonOutput);

        // Act
        var result = await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "new", Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        var data = PhaseCommandTestHelper.GetData(jsonOutput);

        data.GetProperty("gate").ValueKind
            .Should().Be(JsonValueKind.Null);

        data.GetProperty("featureId").GetString()
            .Should().Be(Id);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task ExecuteAsync_PlainTextNullGate_WritesGateNone()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateReader(CreateResult(null, null)));

        // Act
        await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "new" });

        // Assert
        console.Lines.Skip(10)
            .Should().Equal(
                "Gate: none (first phase of the profile)",
                "Coverage threshold: not set");
    }

    [Fact]
    public async Task ExecuteAsync_PlainTextValuesWithMarkupCharacters_WritesValuesVerbatim()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var result = new GateInfoResult("[red]x[/]", "fea[ture]", new FeaturePaths("[dir]/", "[dir]/f", "[dir]/r"),
            [new PhaseSnapshot("[bold]new[/]", PhaseState.InProgress)],
            new GateInfoGate("pl[an]", true, "checks/[dor].md", "[dir]/checks/[dor].md", true), null, []);
        var command = CreateCommand(console, CreateReader(result));

        // Act
        await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "plan" });

        // Assert
        console.Lines
            .Should().Equal(
                "Feature: [red]x[/] (profile fea[ture])",
                "Feature directory: [dir]/",
                "Phases:",
                "  [bold]new[/]: in-progress",
                "Gate: pl[an] (approval: required, checklist: checks/[dor].md)",
                "Checklist copy: [dir]/checks/[dor].md (exists)",
                "Coverage threshold: not set");
    }

    [Fact]
    public async Task ExecuteAsync_JsonWithWarning_WritesWarningCode()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(),
            CreateReader(CreateResult(DefinitionOfReadyGate, null, [FeatureCommandTestHelper.UpgradeWarning])),
            jsonOutput);

        // Act
        await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "plan", Json = true });

        // Assert
        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        FeatureCommandTestHelper.GetWarningCodes(document.RootElement)
            .Should().Equal(ShadeKitCliErrorCodes.UpgradeNeeded);
    }

    [Theory]
    [Trait("Requirement", "FP06-4-AC5")]
    [InlineData("gh-7-other-feature")]
    [InlineData(null)]
    public async Task ExecuteAsync_FeatureOption_PassesFeatureIdToReader(string? featureId)
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        var kitStore = A.Fake<IKitStore>();
        var reader = CreateReader(CreateResult(DefinitionOfReadyGate, null));
        var context = PhaseCommandTestHelper.CreateContext(new TestConsole(),
            cancellation: new TestCliCancellation(cancellationSource.Token), kitStore: kitStore);
        var command = new GateInfoCommand(context, reader);

        // Act
        await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "plan", FeatureId = featureId });

        // Assert
        A.CallTo(() => reader.ReadAsync(kitStore, "plan", featureId, cancellationSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [Trait("Requirement", "FP06-4-AC5")]
    [Trait("Requirement", "FP06-4-AC6")]
    [MemberData(nameof(MappedReadFailures))]
    public async Task ExecuteAsync_JsonMappedFailure_WritesErrorEnvelopeWithMappedCode(Exception exception,
        int exitCode, string errorCode)
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var reader = A.Fake<IGateInfoReader>();
        A.CallTo(() => reader.ReadAsync(A<IKitStore>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .ThrowsAsync(exception);

        var command = CreateCommand(new TestConsole(), reader, jsonOutput);

        // Act
        var act = () => command.ExecuteAsync(new GateInfoCommandOptions { Phase = "plan", Json = true });

        // Assert
        var abort = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        abort.ExitCode
            .Should().Be(exitCode);

        abort.PrintMessage
            .Should().BeFalse();

        using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(errorCode);
    }

    public static TheoryData<Exception, int, string> MappedReadFailures() =>
        new TheoryData<Exception, int, string>
        {
            { new FeatureNotFoundException(Id), ShadeKitCliExitCodes.FeatureNotFound, ShadeKitCliErrorCodes.FeatureNotFound },
            { new InvalidFeatureIdException("Bad"), ShadeKitCliExitCodes.InvalidFeatureId, ShadeKitCliErrorCodes.InvalidFeatureId },
            { new NoCurrentFeatureException(), ShadeKitCliExitCodes.NoCurrentFeature, ShadeKitCliErrorCodes.NoCurrentFeature },
            {
                new PhaseNotInProfileException("nope", "feature", ["new", "refine", "map", "plan"]),
                ShadeKitCliExitCodes.PhaseNotInProfile, ShadeKitCliErrorCodes.PhaseNotInProfile
            },
            {
                new GitUnavailableException(["symbolic-ref", "--short", "-q", "HEAD"], "git not found"),
                ShadeKitCliExitCodes.GitUnavailable, ShadeKitCliErrorCodes.GitUnavailable
            },
            {
                new InvalidKitFileException(".shadekit/gates.json", "invalid JSON"),
                ShadeKitCliExitCodes.InvalidFile, ShadeKitCliErrorCodes.InvalidFile
            },
            {
                new FormatTooNewException(".shadekit/config.json", 9, 1, "9.0.0"),
                ShadeKitCliExitCodes.FormatTooNew, ShadeKitCliErrorCodes.FormatTooNew
            },
            {
                new UpgradeNeededException(".shadekit/gates.json", 1, 2),
                ShadeKitCliExitCodes.UpgradeNeeded, ShadeKitCliErrorCodes.UpgradeNeeded
            },
            {
                new UnknownProfileException("legacy", ["feature", "bugfix", "quick"], $".shadekit/features/{Id}/feature.json"),
                ShadeKitCliExitCodes.UnknownProfile, ShadeKitCliErrorCodes.UnknownProfile
            },
            {
                new InvalidPhaseStateException($".shadekit/features/{Id}/feature.json", "pendingTransition",
                    "is missing, but the current phase 'refine' is awaiting-approval."),
                ShadeKitCliExitCodes.PhaseStateInvalid, ShadeKitCliErrorCodes.PhaseStateInvalid
            }
        };

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task ExecuteAsync_PlainTextMappedFailure_ThrowsPrintedAbortExceptionWithoutOutput()
    {
        // Arrange
        var console = new TestConsole();
        var reader = A.Fake<IGateInfoReader>();
        A.CallTo(() => reader.ReadAsync(A<IKitStore>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .ThrowsAsync(new PhaseNotInProfileException("nope", "feature", ["new", "refine"]));

        var command = CreateCommand(console, reader);

        // Act
        var act = () => command.ExecuteAsync(new GateInfoCommandOptions { Phase = "nope" });

        // Assert
        var abort = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        abort.ExitCode
            .Should().Be(ShadeKitCliExitCodes.PhaseNotInProfile);

        abort.PrintMessage
            .Should().BeTrue();

        abort.Message
            .Should().Contain("'nope'");

        console.Output
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_KitRootNotFound_ThrowsAbortExceptionWithoutCallingReader(bool json)
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var reader = A.Fake<IGateInfoReader>();
        var context = TestShadeKitCommandContext.Create(new TestConsole(),
            kitStoreFactory: FeatureCommandTestHelper.CreateThrowingKitStoreFactory(
                new KitRootPathNotFoundException("/repo/src", "/repo")),
            jsonOutput: jsonOutput);
        var command = new GateInfoCommand(context, reader);

        // Act
        var act = () => command.ExecuteAsync(new GateInfoCommandOptions { Phase = "plan", Json = json });

        // Assert
        (await act.Should().ThrowAsync<CliCommandAbortException>())
            .Which.ExitCode.Should().Be(ShadeKitCliExitCodes.KitRootNotFound);

        A.CallTo(reader)
            .MustNotHaveHappened();

        if (json)
        {
            using var document = FeatureCommandTestHelper.ParseOutput(jsonOutput);

            document.RootElement.GetProperty("error").GetProperty("code").GetString()
                .Should().Be(ShadeKitCliErrorCodes.KitRootNotFound);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PlainTextWithWarning_WritesWarningFirst()
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console,
            CreateReader(CreateResult(DefinitionOfReadyGate, null, [FeatureCommandTestHelper.UpgradeWarning])));

        // Act
        await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "plan" });

        // Assert
        console.Lines[0]
            .Should().StartWith("Warning: ");

        console.Lines[1]
            .Should().Be($"Feature: {Id} (profile feature)");
    }

    [Theory]
    [InlineData(80.5, "Coverage threshold: 80.5")]
    [InlineData(0d, "Coverage threshold: 0")]
    public async Task ExecuteAsync_PlainTextThresholdUnderCommaDecimalCulture_WritesInvariantNumber(
        double threshold, string expectedLine)
    {
        // Arrange
        var console = FeatureCommandTestHelper.CreateWideConsole();
        var command = CreateCommand(console, CreateReader(CreateResult(DefinitionOfReadyGate, threshold)));

        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            // Act
            await command.ExecuteAsync(new GateInfoCommandOptions { Phase = "plan" });
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        // Assert
        console.Lines[^1]
            .Should().Be(expectedLine);
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), CreateReader(CreateResult(null, null)));

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Theory]
    [InlineData(0, "context")]
    [InlineData(1, "gateInfoReader")]
    public void Ctor_NullArgument_ThrowsArgumentNullException(int nullIndex, string parameterName)
    {
        // Act
        var act = () => new GateInfoCommand(
            nullIndex == 0 ? null! : TestShadeKitCommandContext.Create(new TestConsole()),
            nullIndex == 1 ? null! : A.Fake<IGateInfoReader>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void CliAssembly_CliCommandGroupAttributes_ContainGateGroup()
    {
        // Act
        var groupAttributes = typeof(GateInfoCommand).Assembly.GetCustomAttributes<CliCommandGroupAttribute>();

        // Assert
        groupAttributes
            .Where(x => x.Commands is ["gate"])
            .Should().ContainSingle();

        GateCommandGroup.Name
            .Should().Be("gate");
    }

    [Fact]
    public void GateInfoCommand_CliCommandAttribute_IsRegisteredInGateGroupWithJsonOptions()
    {
        // Act
        var attribute = typeof(GateInfoCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("gate", "info");

        typeof(GateInfoCommand)
            .Should().Implement<ICliCommand<GateInfoCommandOptions>>();

        typeof(GateInfoCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    [Fact]
    public void GateInfoCommandOptions_Attributes_PhaseIsRequiredFirstValueAndFeatureIsOptionalNamedOption()
    {
        // Act
        var phase = typeof(GateInfoCommandOptions).GetProperty(nameof(GateInfoCommandOptions.Phase))!
            .GetCustomAttribute<OptionValueAttribute>();
        var feature = typeof(GateInfoCommandOptions).GetProperty(nameof(GateInfoCommandOptions.FeatureId))!
            .GetCustomAttribute<OptionParameterAttribute>();

        // Assert
        phase
            .Should().NotBeNull();

        phase.Index
            .Should().Be(0);

        phase.IsRequired
            .Should().BeTrue();

        feature
            .Should().NotBeNull();

        feature.LongName
            .Should().Be("feature");

        feature.IsRequired
            .Should().BeFalse();
    }

    private static GateInfoResult CreateResult(GateInfoGate? gate, double? coverageThreshold,
        IReadOnlyList<KitWarning>? warnings = null)
    {
        return new GateInfoResult(Id, "feature", FeaturePaths.For(Id),
        [
            new PhaseSnapshot("new", PhaseState.Approved), new PhaseSnapshot("refine", PhaseState.Approved),
            new PhaseSnapshot("map", PhaseState.InProgress), new PhaseSnapshot("plan", PhaseState.Pending),
            new PhaseSnapshot("implement", PhaseState.Pending), new PhaseSnapshot("verify", PhaseState.Pending),
            new PhaseSnapshot("finish", PhaseState.Pending)
        ], gate, coverageThreshold, warnings ?? []);
    }

    private static IGateInfoReader CreateReader(GateInfoResult result)
    {
        var reader = A.Fake<IGateInfoReader>();
        A.CallTo(() => reader.ReadAsync(A<IKitStore>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(result);

        return reader;
    }

    private static GateInfoCommand CreateCommand(IAnsiConsole console, IGateInfoReader reader,
        TextWriter? jsonOutput = null)
    {
        return new GateInfoCommand(PhaseCommandTestHelper.CreateContext(console, jsonOutput), reader);
    }
}
