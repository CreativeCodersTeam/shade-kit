using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.ShadeKit.Cli.Commands;
using CreativeCoders.ShadeKit.Cli.Commands.Version;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Warnings;
using CreativeCoders.ShadeKit.Core.Versions;
using CreativeCoders.ShadeKit.Core.Versions.Exceptions;
using FakeItEasy;
using Spectre.Console;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Version;

public class VersionCommandTests
{
    // The context of the tests maps failures with CliErrorMapperTests.CreateMapper, whose engine version is 1.0.0.
    private const string MapperEngineVersion = "1.0.0";

    private const string EngineOutdatedMessage =
        "The SHADE kit engine 1.0.0 supports the CLI contracts 1 to 1, but the kit skills need the CLI contract 2. " +
        "Update the engine, then run the skill again.";

    private const string SkillsOutdatedMessage =
        "The kit skills are written for the CLI contract 1, but the SHADE kit engine 1.0.0 supports only the CLI " +
        "contracts 2 to 3. Run 'shade-kit integrations install' to install the matching kit skills, then run the " +
        "skill again.";

    private const string ContractLine = "CLI contract 1 (supported: 1 to 1)";

    private const string PluginId = "shade-kit@creativecoders-shade-kit";

    private static readonly InstalledIntegration ClaudeIntegration =
        new InstalledIntegration("claude", PluginId, "0.8.0", "v0.8.0", true);

    [Fact]
    [Trait("Requirement", "FP06-1-AC6")]
    public async Task ExecuteAsync_PlainMode_WritesToolNameAndEngineVersion()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3");

        // Act
        await command.ExecuteAsync(new VersionCommandOptions());

        // Assert
        console.Lines
            .Should().Equal("shade-kit 1.2.3", ContractLine);

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PlainMode_WritesContractLineAfterVersionLine()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var command = CreateCommand(console, TextWriter.Null, "1.2.3",
            integrationService: CreateIntegrationService(ClaudeIntegration));

        // Act
        await command.ExecuteAsync(new VersionCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                "shade-kit 1.2.3",
                $"CLI contract {CliContract.Current} (supported: {CliContract.MinSupported} to {CliContract.Current})",
                "claude: shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC7")]
    [Trait("Requirement", "FP01-AC12")]
    public async Task ExecuteAsync_JsonMode_WritesEnvelopeWithEngineVersionAndOneFormatVersionPerFileType()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3");

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.EnumerateObject().Select(x => x.Name)
            .Should().Equal("ok", "data", "warnings", "error");

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        root.GetProperty("data").GetProperty("engineVersion").GetString()
            .Should().Be("1.2.3");

        root.GetProperty("data").GetProperty("formats").EnumerateObject()
            .Select(x => (x.Name, x.Value.GetInt32()))
            .Should().Equal(
                ("config", 1),
                ("gates", 1),
                ("feature", 1),
                ("current-feature", 1),
                ("templates-lock", 1));

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        root.GetProperty("error").ValueKind
            .Should().Be(JsonValueKind.Null);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonMode_WritesContractWithCurrentAndMinSupported()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3");

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var data = document.RootElement.GetProperty("data");

        data.EnumerateObject().Select(x => x.Name)
            .Should().Equal("engineVersion", "formats", "contract", "integrations");

        data.GetProperty("contract").EnumerateObject().Select(x => (x.Name, x.Value.GetInt32()))
            .Should().Equal(("current", CliContract.Current), ("minSupported", CliContract.MinSupported));
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithWiderWindow_WritesCurrentAndMinSupportedOfWindow()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3",
            checker: new CliContractChecker(new CliContractWindow(2, 4)));

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("data").GetProperty("contract").EnumerateObject()
            .Select(x => (x.Name, x.Value.GetInt32()))
            .Should().Equal(("current", 4), ("minSupported", 2));
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeWithWiderWindow_WritesContractLineOfWindow()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var command = CreateCommand(console, TextWriter.Null, "1.2.3",
            checker: new CliContractChecker(new CliContractWindow(2, 4)));

        // Act
        await command.ExecuteAsync(new VersionCommandOptions());

        // Assert
        console.Lines
            .Should().Equal("shade-kit 1.2.3", "CLI contract 4 (supported: 2 to 4)");
    }

    [Fact]
    [Trait("Requirement", "FP06-1-AC6")]
    public async Task ExecuteAsync_JsonModeWithoutExpectContract_WritesEnvelopeWithContractAndIntegrations()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3");

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        jsonOutput.ToString()
            .Should().Be(
                """
                {
                  "ok": true,
                  "data": {
                    "engineVersion": "1.2.3",
                    "formats": {
                      "config": 1,
                      "gates": 1,
                      "feature": 1,
                      "current-feature": 1,
                      "templates-lock": 1
                    },
                    "contract": {
                      "current": 1,
                      "minSupported": 1
                    },
                    "integrations": []
                  },
                  "warnings": [],
                  "error": null
                }
                """.ReplaceLineEndings("\n") + "\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_WithoutExpectContract_DoesNotCheckContract(bool json)
    {
        // Arrange
        var checker = CreateChecker();
        var command = CreateCommand(new TestConsole(), TextWriter.Null, "1.2.3", checker: checker);

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { Json = json });

        // Assert
        result.ExitCode
            .Should().Be(0);

        A.CallTo(() => checker.Check(A<int>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeExpectContractInWindow_WritesSuccess()
    {
        // Arrange
        var checker = CreateChecker();
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, "1.2.3", checker: checker);

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { Json = true, ExpectContract = 1 });

        // Assert
        result.ExitCode
            .Should().Be(0);

        A.CallTo(() => checker.Check(1))
            .MustHaveHappenedOnceExactly();

        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);

        root.GetProperty("error").ValueKind
            .Should().Be(JsonValueKind.Null);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeExpectContractWithRealChecker_SucceedsForEveryContractInWindow()
    {
        // Arrange
        var checker = new CliContractChecker(new CliContractWindow(2, 4));
        var expectedContracts = new List<int>();

        // Act
        foreach (var contract in new[] { 2, 3, 4 })
        {
            using var jsonOutput = new StringWriter();
            var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3", checker: checker);

            await command.ExecuteAsync(new VersionCommandOptions { Json = true, ExpectContract = contract });

            using var document = JsonDocument.Parse(jsonOutput.ToString());

            expectedContracts.Add(document.RootElement.GetProperty("data").GetProperty("expectedContract").GetInt32());
        }

        // Assert
        expectedContracts
            .Should().Equal(2, 3, 4);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeExpectContract_WritesExpectedContract()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3");

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true, ExpectContract = 1 });

        // Assert
        jsonOutput.ToString()
            .Should().Be(
                """
                {
                  "ok": true,
                  "data": {
                    "engineVersion": "1.2.3",
                    "formats": {
                      "config": 1,
                      "gates": 1,
                      "feature": 1,
                      "current-feature": 1,
                      "templates-lock": 1
                    },
                    "contract": {
                      "current": 1,
                      "minSupported": 1
                    },
                    "expectedContract": 1
                  },
                  "warnings": [],
                  "error": null
                }
                """.ReplaceLineEndings("\n") + "\n");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithoutExpectContract_OmitsExpectedContract()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3");

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("data").TryGetProperty("expectedContract", out _)
            .Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeEngineOutdated_WritesEngineOutdatedEnvelopeAndAbortsWith54()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, MapperEngineVersion,
            checker: CreateThrowingChecker(new EngineContractOutdatedException(2, 1, 1)));

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { Json = true, ExpectContract = 2 });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.EngineOutdated);

        exception.PrintMessage
            .Should().BeFalse();

        AssertFailureEnvelope(jsonOutput.ToString(), ShadeKitCliErrorCodes.EngineOutdated, EngineOutdatedMessage);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeSkillsOutdated_WritesSkillsOutdatedEnvelopeAndAbortsWith55()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, MapperEngineVersion,
            checker: CreateThrowingChecker(new SkillsContractOutdatedException(1, 2, 3)));

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { Json = true, ExpectContract = 1 });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.SkillsOutdated);

        exception.PrintMessage
            .Should().BeFalse();

        AssertFailureEnvelope(jsonOutput.ToString(), ShadeKitCliErrorCodes.SkillsOutdated, SkillsOutdatedMessage);

        console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeEngineOutdated_AbortsWith54AndMessageWithoutOutput()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, MapperEngineVersion,
            checker: CreateThrowingChecker(new EngineContractOutdatedException(2, 1, 1)));

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { ExpectContract = 2 });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.EngineOutdated);

        exception.PrintMessage
            .Should().BeTrue();

        exception.Message
            .Should().Be(EngineOutdatedMessage);

        console.Output
            .Should().BeEmpty();

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeSkillsOutdated_AbortsWith55AndMessageWithoutOutput()
    {
        // Arrange
        var console = new TestConsole();
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(console, jsonOutput, MapperEngineVersion,
            checker: CreateThrowingChecker(new SkillsContractOutdatedException(1, 2, 3)));

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { ExpectContract = 1 });

        // Assert
        var exception = (await act.Should().ThrowAsync<CliCommandAbortException>()).Which;

        exception.ExitCode
            .Should().Be(ShadeKitCliExitCodes.SkillsOutdated);

        exception.PrintMessage
            .Should().BeTrue();

        exception.Message
            .Should().Be(SkillsOutdatedMessage);

        console.Output
            .Should().BeEmpty();

        jsonOutput.ToString()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ExpectContract_DoesNotReadIntegrationsAndOmitsField()
    {
        // Arrange
        var integrationService = CreateIntegrationService(ClaudeIntegration);
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3", integrationService: integrationService);

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true, ExpectContract = 1 });

        // Assert
        A.CallTo(integrationService)
            .MustNotHaveHappened();

        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("data").TryGetProperty("integrations", out _)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task ExecuteAsync_ContractMismatch_DoesNotReadIntegrations(bool engineOutdated, bool json)
    {
        // Arrange
        Exception mismatch = engineOutdated
            ? new EngineContractOutdatedException(7, 1, 1)
            : new SkillsContractOutdatedException(7, 8, 9);

        var integrationService = CreateIntegrationService(ClaudeIntegration);
        var command = CreateCommand(new TestConsole(), TextWriter.Null, "1.2.3",
            integrationService: integrationService, checker: CreateThrowingChecker(mismatch));

        // Act
        var act = () => command.ExecuteAsync(new VersionCommandOptions { Json = json, ExpectContract = 7 });

        // Assert
        await act.Should().ThrowAsync<CliCommandAbortException>();

        A.CallTo(integrationService)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeExpectContract_WritesNoIntegrationLines()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var integrationService = CreateIntegrationService(ClaudeIntegration);
        var command = CreateCommand(console, TextWriter.Null, "1.2.3", integrationService: integrationService);

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { ExpectContract = 1 });

        // Assert
        result.ExitCode
            .Should().Be(0);

        console.Lines
            .Should().Equal("shade-kit 1.2.3", ContractLine);

        A.CallTo(integrationService)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithoutExpectContract_WritesIntegrations()
    {
        // Arrange
        var integrationService = CreateIntegrationService(ClaudeIntegration);
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3", integrationService: integrationService);

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        A.CallTo(() => integrationService.GetInstalledAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("data").GetProperty("integrations").GetArrayLength()
            .Should().Be(1);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC7")]
    public async Task ExecuteAsync_JsonModeWithRaisedFormatVersion_ReportsCurrentVersionOfFileType()
    {
        // Arrange
        var registry = new FormatRegistry(
            [new FormatDefinition("config", new Dictionary<int, string> { [1] = "0.1.0", [2] = "0.2.0" },
                [A.Fake<IFormatMigration>(x => x.ConfigureFake(m =>
                {
                    A.CallTo(() => m.FormatId).Returns("config");
                    A.CallTo(() => m.FromVersion).Returns(1);
                }))])]);

        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3", registry);

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("data").GetProperty("formats").GetProperty("config").GetInt32()
            .Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeWithoutFileTypes_WritesEmptyFormatsObject()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "1.2.3", new FormatRegistry([]));

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());

        document.RootElement.GetProperty("data").GetProperty("formats").EnumerateObject()
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_AnyMode_ReturnsSuccess(bool json)
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), TextWriter.Null, "1.2.3");

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { Json = json });

        // Assert
        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var command = CreateCommand(new TestConsole(), TextWriter.Null, "1.2.3");

        // Act
        var act = () => command.ExecuteAsync(null!);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("options");
    }

    [Fact]
    public void VersionCommandOptions_Type_DerivesFromJsonCommandOptions()
    {
        // Assert
        typeof(VersionCommandOptions)
            .Should().BeDerivedFrom<JsonCommandOptions>();
    }

    [Fact]
    [Trait("Requirement", "FP07-AC4")]
    public async Task ExecuteAsync_JsonModeWithInstalledIntegration_ListsIntegrationWithKitVersion()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "0.8.0",
            integrationService: CreateIntegrationService(ClaudeIntegration));

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        JsonSerializer.Serialize(root.GetProperty("data").GetProperty("integrations"))
            .Should().Be(
                "[{\"agent\":\"claude\",\"pluginId\":\"shade-kit@creativecoders-shade-kit\",\"kitVersion\":\"0.8.0\"," +
                "\"ref\":\"v0.8.0\",\"enabled\":true}]");

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);
    }

    [Fact]
    [Trait("Requirement", "FP07-AC4")]
    public async Task ExecuteAsync_JsonModeWithIntegrationOfUnknownVersion_WritesNullKitVersionAndRef()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "0.8.0",
            integrationService: CreateIntegrationService(
                new InstalledIntegration("claude", PluginId, null, null, false)));

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        using var document = JsonDocument.Parse(jsonOutput.ToString());

        JsonSerializer.Serialize(document.RootElement.GetProperty("data").GetProperty("integrations"))
            .Should().Be(
                "[{\"agent\":\"claude\",\"pluginId\":\"shade-kit@creativecoders-shade-kit\",\"kitVersion\":null," +
                "\"ref\":null,\"enabled\":false}]");
    }

    [Fact]
    [Trait("Requirement", "FP07-AC4")]
    public async Task ExecuteAsync_PlainModeWithInstalledIntegrations_WritesOneLinePerIntegrationAfterVersionLine()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var command = CreateCommand(console, TextWriter.Null, "0.8.0",
            integrationService: CreateIntegrationService(ClaudeIntegration,
                new InstalledIntegration("claude", PluginId, null, "main", false)));

        // Act
        await command.ExecuteAsync(new VersionCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                "shade-kit 0.8.0",
                ContractLine,
                "claude: shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "claude: shade-kit@creativecoders-shade-kit (main) (disabled)");
    }

    [Fact]
    public async Task ExecuteAsync_JsonModeAgentUnavailable_WritesSuccessWithEmptyIntegrationsAndWarning()
    {
        // Arrange
        using var jsonOutput = new StringWriter();
        var command = CreateCommand(new TestConsole(), jsonOutput, "0.8.0",
            integrationService: CreateIntegrationService(new InstalledIntegrationsResult([],
                [new AgentUnavailableWarning("claude", "claude was not found on the PATH.")])));

        // Act
        var result = await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        result.ExitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(jsonOutput.ToString());
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        root.GetProperty("data").GetProperty("integrations").GetArrayLength()
            .Should().Be(0);

        root.GetProperty("warnings").EnumerateArray()
            .Select(x => (x.GetProperty("code").GetString(), x.GetProperty("message").GetString()))
            .Should().Equal((ShadeKitCliWarningCodes.AgentUnavailable,
                "The SHADE kit integration of the agent 'claude' cannot be read, so it is not listed: claude was not " +
                "found on the PATH."));
    }

    [Fact]
    public async Task ExecuteAsync_PlainModeAgentUnavailable_WritesWarningBeforeVersionLine()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var command = CreateCommand(console, TextWriter.Null, "0.8.0",
            integrationService: CreateIntegrationService(new InstalledIntegrationsResult([],
                [new AgentUnavailableWarning("claude", "claude was not found on the PATH.")])));

        // Act
        await command.ExecuteAsync(new VersionCommandOptions());

        // Assert
        console.Lines
            .Should().Equal(
                "Warning: The SHADE kit integration of the agent 'claude' cannot be read, so it is not listed: " +
                "claude was not found on the PATH.",
                "shade-kit 0.8.0",
                ContractLine);
    }

    [Fact]
    public async Task ExecuteAsync_AnyMode_PassesCancellationTokenOfRunToIntegrationService()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var integrationService = CreateIntegrationService();
        var command = new VersionCommand(
            TestShadeKitCommandContext.Create(new TestConsole(),
                cancellation: new TestCliCancellation(cancellationTokenSource.Token)),
            CreateEngineVersionProvider("1.2.3"), FormatRegistry.CreateDefault(), CreateChecker(),
            integrationService);

        // Act
        await command.ExecuteAsync(new VersionCommandOptions { Json = true });

        // Assert
        A.CallTo(() => integrationService.GetInstalledAsync(cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Ctor_NullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(null!, CreateEngineVersionProvider("1.0.0"), FormatRegistry.CreateDefault(),
            A.Fake<ICliContractChecker>(), CreateIntegrationService());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Ctor_NullEngineVersionProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(TestShadeKitCommandContext.Create(new TestConsole()), null!,
            FormatRegistry.CreateDefault(), A.Fake<ICliContractChecker>(), CreateIntegrationService());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("engineVersionProvider");
    }

    [Fact]
    public void Ctor_NullFormatRegistry_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(TestShadeKitCommandContext.Create(new TestConsole()),
            CreateEngineVersionProvider("1.0.0"), null!, A.Fake<ICliContractChecker>(), CreateIntegrationService());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formatRegistry");
    }

    [Fact]
    public void Ctor_NullCliContractChecker_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(TestShadeKitCommandContext.Create(new TestConsole()),
            CreateEngineVersionProvider("1.0.0"), FormatRegistry.CreateDefault(), null!, CreateIntegrationService());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cliContractChecker");
    }

    [Fact]
    public void Ctor_NullIntegrationService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(TestShadeKitCommandContext.Create(new TestConsole()),
            CreateEngineVersionProvider("1.0.0"), FormatRegistry.CreateDefault(), A.Fake<ICliContractChecker>(),
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("integrationService");
    }

    [Fact]
    public void VersionCommand_CliCommandAttribute_IsRegisteredAsTopLevelVersionCommand()
    {
        // Act
        var attribute = typeof(VersionCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute.Commands
            .Should().Equal("version");
    }

    private static VersionCommand CreateCommand(IAnsiConsole console, TextWriter jsonOutput, string engineVersion,
        IFormatRegistry? formatRegistry = null, IIntegrationService? integrationService = null,
        ICliContractChecker? checker = null)
    {
        return new VersionCommand(TestShadeKitCommandContext.Create(console, jsonOutput: jsonOutput),
            CreateEngineVersionProvider(engineVersion), formatRegistry ?? FormatRegistry.CreateDefault(),
            checker ?? CreateChecker(), integrationService ?? CreateIntegrationService());
    }

    // FakeItEasy cannot create a dummy CliContractWindow (its constructor rejects 0), so every fake checker that the
    // command runs with reports the window of the engine.
    private static ICliContractChecker CreateChecker()
    {
        var checker = A.Fake<ICliContractChecker>();
        A.CallTo(() => checker.Window).Returns(CliContract.Window);

        return checker;
    }

    private static ICliContractChecker CreateThrowingChecker(Exception exception)
    {
        var checker = CreateChecker();
        A.CallTo(() => checker.Check(A<int>._)).Throws(exception);

        return checker;
    }

    private static void AssertFailureEnvelope(string output, string errorCode, string message)
    {
        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("data").ValueKind
            .Should().Be(JsonValueKind.Null);

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(errorCode);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be(message);
    }

    private static IIntegrationService CreateIntegrationService(params InstalledIntegration[] integrations)
    {
        return CreateIntegrationService(new InstalledIntegrationsResult(integrations, []));
    }

    private static IIntegrationService CreateIntegrationService(InstalledIntegrationsResult result)
    {
        var integrationService = A.Fake<IIntegrationService>();
        A.CallTo(() => integrationService.GetInstalledAsync(A<CancellationToken>._)).Returns(result);

        return integrationService;
    }

    private static IEngineVersionProvider CreateEngineVersionProvider(string version)
    {
        var engineVersionProvider = A.Fake<IEngineVersionProvider>();
        A.CallTo(() => engineVersionProvider.Version).Returns(version);

        return engineVersionProvider;
    }
}
