using System.Diagnostics;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Features;
using CreativeCoders.ShadeKit.Core.Tests.Store;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Gates;

public sealed class GatesConfigReaderTests : IDisposable
{
    private const string GatesDisplayPath = ".shadekit/gates.json";

    private const string Header = """ "schemaVersion": 1, "minEngineVersion": "0.1.0" """;

    private static readonly TimeSpan ReadStartTimeout = TimeSpan.FromSeconds(10);

    private readonly TempKitDirectory _repository = new TempKitDirectory();

    private readonly string _kitStorePath;

    public GatesConfigReaderTests()
    {
        _repository.CreateGitRoot();
        _kitStorePath = _repository.CreateDirectory(KitStorePaths.RootPath);
    }

    public void Dispose()
    {
        _repository.Dispose();
    }

    [Fact]
    public void Ctor_NullWorkflowProfileCatalog_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GatesConfigReader(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("workflowProfileCatalog");
    }

    [Fact]
    public async Task ReadAsync_NullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("store");
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsDefaultsWithFileMissingWarning()
    {
        // Arrange
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Profiles
            .Should().BeEquivalentTo(DefaultProfilesView());

        result.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));
    }

    [Fact]
    public async Task ReadAsync_FileWithoutProfilesButOtherFields_ReturnsDefaultsWithFieldMissingWarning()
    {
        // Arrange
        WriteGates("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "team": "core" }""");
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Profiles
            .Should().BeEquivalentTo(DefaultProfilesView());

        result.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: false));
    }

    [Fact]
    public async Task ReadAsync_ExplicitEmptyProfiles_ReturnsEmptyProfilesWithoutWarning()
    {
        // Arrange
        WriteGatesWithProfiles("{}");
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Profiles
            .Should().NotBeNull()
            .And.BeEmpty();

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_KnownEntries_ReturnsEntriesAsReadWithoutWarning()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "feature": { "plan": { "approval": false, "checklist": "checks/plan.md" } },
              "quick": { "quick": { "approval": true }, "finish": { } }
            }
            """);
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        var profiles = result.Profiles;

        profiles.Keys
            .Should().Equal(WorkflowProfileNames.Feature, WorkflowProfileNames.Quick);

        profiles[WorkflowProfileNames.Feature].Keys
            .Should().Equal(WorkflowPhaseNames.Plan);

        profiles[WorkflowProfileNames.Feature][WorkflowPhaseNames.Plan]
            .Should().Be(new TransitionGate(Approval: false, Checklist: "checks/plan.md"));

        profiles[WorkflowProfileNames.Quick].Keys
            .Should().Equal(WorkflowPhaseNames.Quick, WorkflowPhaseNames.Finish);

        profiles[WorkflowProfileNames.Quick][WorkflowPhaseNames.Finish].Approval
            .Should().BeTrue();

        result.Warnings
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{ "deploy": { "approval": true } }""")]
    [InlineData("{}")]
    public async Task ReadAsync_UnknownProfile_ReturnsOneWarningForProfileAndKeepsEntry(string gates)
    {
        // Arrange
        WriteGatesWithProfiles($$"""{ "release": {{gates}} }""");
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile));

        result.Profiles
            .Should().ContainKey("release");
    }

    [Fact]
    public async Task ReadAsync_ProfileNameInOtherCase_ReturnsUnknownProfileWarning()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "Feature": { "plan": { } } }""");
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning("Feature", null, GateEntryIgnoredReason.UnknownProfile));
    }

    [Theory]
    [InlineData(WorkflowProfileNames.Feature, WorkflowPhaseNames.Diagnose)]
    [InlineData(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Refine)]
    [InlineData(WorkflowProfileNames.Quick, WorkflowPhaseNames.Plan)]
    [InlineData(WorkflowProfileNames.Feature, "Plan")]
    [InlineData(WorkflowProfileNames.Feature, "deploy")]
    public async Task ReadAsync_PhaseNotInProfile_ReturnsUnknownPhaseWarningAndKeepsEntry(string profileName,
        string phaseName)
    {
        // Arrange
        WriteGatesWithProfiles($$"""{ "{{profileName}}": { "{{phaseName}}": { "approval": false } } }""");
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning(profileName, phaseName, GateEntryIgnoredReason.UnknownPhase));

        result.Profiles[profileName]
            .Should().ContainKey(phaseName);
    }

    [Theory]
    [InlineData(WorkflowProfileNames.Feature)]
    [InlineData(WorkflowProfileNames.Bugfix)]
    [InlineData(WorkflowProfileNames.Quick)]
    public async Task ReadAsync_FirstPhaseOfProfile_ReturnsFirstPhaseWarningAndKeepsEntry(string profileName)
    {
        // Arrange
        WriteGatesWithProfiles($$"""{ "{{profileName}}": { "new": { "approval": false } } }""");
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning(profileName, WorkflowPhaseNames.New,
                GateEntryIgnoredReason.FirstPhase));

        result.Profiles[profileName]
            .Should().ContainKey(WorkflowPhaseNames.New);
    }

    [Fact]
    public async Task ReadAsync_SeveralIgnoredEntries_ReturnsWarningsForWholeFileInFileOrder()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "quick": { "plan": { }, "finish": { }, "new": { } },
              "release": { "ship": { } },
              "feature": { "new": { }, "refine": { }, "deploy": { } },
              "bugfix": { "diagnose": { } },
              "archive": { }
            }
            """);
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new GateEntryIgnoredWarning("quick", "plan", GateEntryIgnoredReason.UnknownPhase),
                new GateEntryIgnoredWarning("quick", "new", GateEntryIgnoredReason.FirstPhase),
                new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile),
                new GateEntryIgnoredWarning("feature", "new", GateEntryIgnoredReason.FirstPhase),
                new GateEntryIgnoredWarning("feature", "deploy", GateEntryIgnoredReason.UnknownPhase),
                new GateEntryIgnoredWarning("archive", null, GateEntryIgnoredReason.UnknownProfile));

        result.Profiles.Keys
            .Should().Equal("quick", "release", "feature", "bugfix", "archive");
    }

    [Fact]
    public async Task ReadAsync_ExistingFile_ReturnsProfilesThatCannotBeChanged()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "feature": { "plan": { "approval": false } } }""");
        var reader = CreateReader();
        var store = CreateStore();
        var result = await reader.ReadAsync(store, TestContext.Current.CancellationToken);
        var profiles = (IDictionary<string, IReadOnlyDictionary<string, TransitionGate>>)result.Profiles;
        var gates = (IDictionary<string, TransitionGate>)result.Profiles[WorkflowProfileNames.Feature];

        // Act
        var changeProfiles = () => profiles.Remove(WorkflowProfileNames.Feature);
        var changeGates = () => gates[WorkflowPhaseNames.Plan] = TransitionGate.Default;

        // Assert
        changeProfiles.Should().Throw<NotSupportedException>();
        changeGates.Should().Throw<NotSupportedException>();

        (await reader.ReadAsync(store, TestContext.Current.CancellationToken))
            .Profiles[WorkflowProfileNames.Feature][WorkflowPhaseNames.Plan]
            .Should().Be(new TransitionGate(Approval: false, Checklist: null));
    }

    [Fact]
    public async Task ReadAsync_TwoGatesShareChecklist_WarnsLaterGateAndIgnoresOnlyItsChecklist()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "feature": {
                "implement": { "approval": true, "checklist": "checks/security.md" },
                "finish": { "approval": false, "checklist": "checks/security.md" }
              }
            }
            """);
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning(WorkflowProfileNames.Feature, WorkflowPhaseNames.Finish,
                GateEntryIgnoredReason.SharedChecklist));

        result.Profiles[WorkflowProfileNames.Feature][WorkflowPhaseNames.Implement]
            .Should().Be(new TransitionGate(Approval: true, Checklist: "checks/security.md"));

        result.Profiles[WorkflowProfileNames.Feature][WorkflowPhaseNames.Finish]
            .Should().Be(new TransitionGate(Approval: false, Checklist: null));
    }

    [Theory]
    [InlineData("checks/security.md", "checks/Security.md")]
    [InlineData("checks/team/dor.md", "checks/TEAM/DoR.md")]
    public async Task ReadAsync_TwoGatesNameChecklistInOtherCase_WarnsLaterGateAsSharedChecklist(
        string earlierChecklist, string laterChecklist)
    {
        // Arrange
        WriteGatesWithProfiles(
            $$"""
            {
              "feature": {
                "implement": { "checklist": "{{earlierChecklist}}" },
                "finish": { "checklist": "{{laterChecklist}}" }
              }
            }
            """);
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning(WorkflowProfileNames.Feature, WorkflowPhaseNames.Finish,
                GateEntryIgnoredReason.SharedChecklist));

        result.Profiles[WorkflowProfileNames.Feature][WorkflowPhaseNames.Implement]
            .Should().Be(new TransitionGate(Approval: true, Checklist: earlierChecklist));

        result.Profiles[WorkflowProfileNames.Feature][WorkflowPhaseNames.Finish]
            .Should().Be(new TransitionGate(Approval: true, Checklist: null));
    }

    [Fact]
    public async Task ReadAsync_SharedChecklistInOtherFileOrder_GateIntoEarliestPhaseKeepsChecklist()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "bugfix": {
                "finish": { "checklist": "checks/review.md" },
                "verify": { "checklist": "checks/review.md" },
                "plan": { "checklist": "checks/review.md" }
              }
            }
            """);
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new GateEntryIgnoredWarning(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Verify,
                    GateEntryIgnoredReason.SharedChecklist),
                new GateEntryIgnoredWarning(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Finish,
                    GateEntryIgnoredReason.SharedChecklist));

        result.Profiles[WorkflowProfileNames.Bugfix].Values.Select(gate => gate.Checklist)
            .Should().Equal(null, null, "checks/review.md");
    }

    [Fact]
    public async Task ReadAsync_SharedChecklistWithIgnoredEntries_WarnsOnlyGatesThatApplyAfterOtherWarnings()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "quick": {
                "new": { "checklist": "checks/x.md" },
                "deploy": { "checklist": "checks/x.md" },
                "finish": { "checklist": "checks/x.md" },
                "quick": { "checklist": "checks/x.md" }
              },
              "release": { "ship": { "checklist": "checks/x.md" }, "done": { "checklist": "checks/x.md" } }
            }
            """);
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new GateEntryIgnoredWarning(WorkflowProfileNames.Quick, WorkflowPhaseNames.New,
                    GateEntryIgnoredReason.FirstPhase),
                new GateEntryIgnoredWarning(WorkflowProfileNames.Quick, "deploy", GateEntryIgnoredReason.UnknownPhase),
                new GateEntryIgnoredWarning(WorkflowProfileNames.Quick, WorkflowPhaseNames.Finish,
                    GateEntryIgnoredReason.SharedChecklist),
                new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile));

        result.Profiles[WorkflowProfileNames.Quick][WorkflowPhaseNames.Quick].Checklist
            .Should().Be("checks/x.md");
    }

    [Theory]
    [InlineData("""{ "feature": { "plan": { "checklist": "checks/a.md" } }, "bugfix": { "plan": { "checklist": "checks/a.md" } } }""")]
    [InlineData("""{ "feature": { "plan": { "checklist": "checks/a.md" }, "finish": { "checklist": "checks/team/a.md" } } }""")]
    public async Task ReadAsync_ChecklistsDifferOrBelongToOtherProfiles_ReturnsNoSharedChecklistWarning(
        string profiles)
    {
        // Arrange
        WriteGatesWithProfiles(profiles);
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().BeEmpty();

        result.Profiles.Values.SelectMany(gates => gates.Values)
            .Should().OnlyContain(gate => gate.Checklist != null);
    }

    [Fact]
    public async Task ReadAsync_OutdatedFileWithIgnoredEntry_ReturnsFormatWarningBeforeGateWarning()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "release": { } }""");
        var reader = CreateReader();
        var store = CreateStore(FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Gates));

        // Act
        var result = await reader.ReadAsync(store, TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, GatesDisplayPath, 1, 2),
                new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile));
    }

    [Fact]
    public async Task ReadAsync_OutdatedFileWithoutProfiles_ReturnsFormatWarningBeforeDefaultsWarning()
    {
        // Arrange
        WriteGates($$"""{ {{Header}} }""");
        var reader = CreateReader();
        var store = CreateStore(FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Gates));

        // Act
        var result = await reader.ReadAsync(store, TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, GatesDisplayPath, 1, 2),
                new GatesDefaultsUsedWarning(IsFileMissing: false));

        result.Profiles
            .Should().BeEquivalentTo(DefaultProfilesView());
    }

    [Fact]
    public async Task ReadAsync_OutdatedFileWithStopSetting_ThrowsUpgradeNeededException()
    {
        // Arrange
        WriteGatesWithProfiles("{}");
        _repository.WriteFile(StoreTestFormats.Config(1, "stop"), KitStorePaths.RootPath, KitStorePaths.ConfigFileName);
        var reader = CreateReader();
        var store = CreateStore(FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Gates));

        // Act
        var act = () => reader.ReadAsync(store, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UpgradeNeededException>()).Which.FilePath
            .Should().Be(GatesDisplayPath);
    }

    [Theory]
    [InlineData($$"""{ {{Header}}, "profiles": null }""")]
    [InlineData($$"""{ {{Header}}, "profiles": { "feature": { "plan": { "checklist": "plan.md" } } } }""")]
    [InlineData("not json")]
    public async Task ReadAsync_InvalidFile_ThrowsInvalidKitFileException(string content)
    {
        // Arrange
        WriteGates(content);
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>()).Which.FilePath
            .Should().Be(GatesDisplayPath);
    }

    [Fact]
    public async Task ReadAsync_SameStoreTwice_ReadsFileOnceWithReadAccessAndReturnsSameResult()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "release": { } }""");
        var reader = CreateReader();
        var realStore = CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var first = await reader.ReadAsync(store, cancellationToken);
        WriteGatesWithProfiles("""{ "archive": { } }""");
        var second = await reader.ReadAsync(store, cancellationToken);

        // Assert
        second
            .Should().BeSameAs(first);

        second.Warnings
            .Should().Equal(new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile));

        // The shared read is not tied to the token of the call that starts it.
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(KitStorePaths.GatesFileName, FormatIds.Gates,
                FormatAccess.Read, CancellationToken.None))
            .MustHaveHappenedOnceExactly();

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReadAsync_MissingFileWithSameStoreTwice_ReturnsSameDefaultsWarning()
    {
        // Arrange
        var reader = CreateReader();
        var store = CreateStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var first = await reader.ReadAsync(store, cancellationToken);
        WriteGatesWithProfiles("{}");
        var second = await reader.ReadAsync(store, cancellationToken);

        // Assert
        second
            .Should().BeSameAs(first);

        second.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));
    }

    [Fact]
    public async Task ReadAsync_OtherStoreInstance_ReadsFileAgain()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "release": { } }""");
        var reader = CreateReader();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var first = await reader.ReadAsync(CreateStore(), cancellationToken);
        WriteGatesWithProfiles("""{ "archive": { } }""");
        var second = await reader.ReadAsync(CreateStore(), cancellationToken);

        // Assert
        first.Warnings
            .Should().Equal(new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile));

        second.Warnings
            .Should().Equal(new GateEntryIgnoredWarning("archive", null, GateEntryIgnoredReason.UnknownProfile));
    }

    [Fact]
    public async Task ReadAsync_ConcurrentCallsWithSameStore_ReadFileOnceAndShareResult()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var pendingRead = new TaskCompletionSource<VersionedReadResult<GatesConfig>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(pendingRead.Task);
        var reader = CreateReader();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        Task<GatesConfigReadResult>[] reads =
        [
            .. Enumerable.Range(0, 8).Select(_ => Task.Run(() => reader.ReadAsync(store, cancellationToken),
                cancellationToken))
        ];

        await WaitUntilReadStartedAsync(store, cancellationToken);
        var config = new GatesConfig { Profiles = [] };
        pendingRead.SetResult(new VersionedReadResult<GatesConfig>(config, []));
        var results = await Task.WhenAll(reads);

        // Assert
        results
            .Should().AllSatisfy(result => result.Should().BeSameAs(results[0]));

        results[0].Profiles
            .Should().BeEmpty();

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReadAsync_ReadFails_ThrowsAndNextCallReadsAgain()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var failure = new InvalidKitFileException(GatesDisplayPath, "The file is not valid JSON.");
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .ThrowsAsync(failure).Once()
            .Then.Returns(Task.FromResult<VersionedReadResult<GatesConfig>?>(null));
        var reader = CreateReader();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var firstAct = () => reader.ReadAsync(store, cancellationToken);
        var firstException = (await firstAct.Should().ThrowAsync<InvalidKitFileException>()).Which;
        var second = await reader.ReadAsync(store, cancellationToken);
        var third = await reader.ReadAsync(store, cancellationToken);

        // Assert
        firstException
            .Should().BeSameAs(failure);

        second.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));

        third
            .Should().BeSameAs(second);

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ReadAsync_ConcurrentCallsWhileReadFails_ShareFailureAndLaterCallReadsAgain()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var pendingRead = new TaskCompletionSource<VersionedReadResult<GatesConfig>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(pendingRead.Task).Once()
            .Then.Returns(Task.FromResult<VersionedReadResult<GatesConfig>?>(null));
        var reader = CreateReader();
        var cancellationToken = TestContext.Current.CancellationToken;
        var failure = new IOException("The disk is gone.");

        // Act
        var firstRead = reader.ReadAsync(store, cancellationToken);
        var secondRead = reader.ReadAsync(store, cancellationToken);
        pendingRead.SetException(failure);
        var firstAct = () => firstRead;
        var secondAct = () => secondRead;
        var firstException = (await firstAct.Should().ThrowAsync<IOException>()).Which;
        var secondException = (await secondAct.Should().ThrowAsync<IOException>()).Which;
        var laterResult = await reader.ReadAsync(store, cancellationToken);

        // Assert
        firstException
            .Should().BeSameAs(failure);

        secondException
            .Should().BeSameAs(failure);

        laterResult.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ReadAsync_CanceledToken_ThrowsOperationCanceledExceptionWithoutReading()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadAsync(store, new CancellationToken(canceled: true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(store)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ReadAsync_ReadCanceled_ThrowsOperationCanceledExceptionAndNextCallReadsAgain()
    {
        // Arrange
        WriteGatesWithProfiles("{}");
        var reader = CreateReader();
        var realStore = CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        using var cancellation = new CancellationTokenSource();
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Invokes(() => cancellation.Cancel())
            .ThrowsAsync(new OperationCanceledException(cancellation.Token)).Once();

        // Act
        var firstAct = () => reader.ReadAsync(store, cancellation.Token);
        await firstAct.Should().ThrowAsync<OperationCanceledException>();
        var second = await reader.ReadAsync(store, TestContext.Current.CancellationToken);

        // Assert
        second.Profiles
            .Should().BeEmpty();

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ReadAsync_WaitingCallCanceled_ThrowsForThatCallAndKeepsSharedRead()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var pendingRead = new TaskCompletionSource<VersionedReadResult<GatesConfig>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(pendingRead.Task);
        var reader = CreateReader();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var waitCancellation = new CancellationTokenSource();

        // Act
        var startingRead = reader.ReadAsync(store, cancellationToken);
        var waitingRead = reader.ReadAsync(store, waitCancellation.Token);
        await waitCancellation.CancelAsync();
        var waitingAct = () => waitingRead;
        await waitingAct.Should().ThrowAsync<OperationCanceledException>();
        pendingRead.SetResult(null);
        var started = await startingRead;
        var later = await reader.ReadAsync(store, cancellationToken);

        // Assert
        later
            .Should().BeSameAs(started);

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReadAsync_StartingCallCanceled_ThrowsForThatCallAndJoiningCallGetsResult()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var pendingRead = new TaskCompletionSource<VersionedReadResult<GatesConfig>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(pendingRead.Task);
        var reader = CreateReader();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var startCancellation = new CancellationTokenSource();

        // Act
        var startingRead = reader.ReadAsync(store, startCancellation.Token);
        var joiningRead = reader.ReadAsync(store, cancellationToken);
        await startCancellation.CancelAsync();
        var startingAct = () => startingRead;
        await startingAct.Should().ThrowAsync<OperationCanceledException>();
        pendingRead.SetResult(null);
        var joined = await joiningRead;
        var later = await reader.ReadAsync(store, cancellationToken);

        // Assert
        joined.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));

        later
            .Should().BeSameAs(joined);

        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(KitStorePaths.GatesFileName, FormatIds.Gates,
                FormatAccess.Read, CancellationToken.None))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReadAsync_CachedResultWithCanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var reader = CreateReader();
        var store = CreateStore();
        await reader.ReadAsync(store, TestContext.Current.CancellationToken);

        // Act
        var act = () => reader.ReadAsync(store, new CancellationToken(canceled: true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadAsync_DefaultFileAsInitWritesIt_ReturnsDefaultsWithoutWarning()
    {
        // Arrange
        WriteGates(new VersionedJsonSerializer(FormatRegistry.CreateDefault())
            .Serialize(GatesConfig.CreateDefault(), FormatIds.Gates));
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Profiles
            .Should().BeEquivalentTo(DefaultProfilesView());

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_KnownProfileWithoutGates_ReturnsEntryWithoutWarning()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "feature": { } }""");
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Profiles
            .Should().ContainKey(WorkflowProfileNames.Feature)
            .WhoseValue.Should().BeEmpty();

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_EmptyProfileAndPhaseNames_ReturnsUnknownProfileAndUnknownPhaseWarnings()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "": { "plan": { } }, "feature": { "": { } } }""");
        var reader = CreateReader();

        // Act
        var result = await reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new GateEntryIgnoredWarning("", null, GateEntryIgnoredReason.UnknownProfile),
                new GateEntryIgnoredWarning("feature", "", GateEntryIgnoredReason.UnknownPhase));
    }

    [Fact]
    public async Task ReadAsync_OutdatedFileWithKnownEntries_ReturnsOnlyFormatWarning()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "feature": { "plan": { } } }""");
        var reader = CreateReader();
        var store = CreateStore(FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Gates));

        // Act
        var result = await reader.ReadAsync(store, TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, GatesDisplayPath, 1, 2));
    }

    [Fact]
    public async Task ReadAsync_FileNewerThanEngine_ThrowsFormatTooNewException()
    {
        // Arrange
        WriteGates("""{ "schemaVersion": 2, "minEngineVersion": "0.2.0", "profiles": {} }""");
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadAsync(CreateStore(), TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<FormatTooNewException>()).Which;

        exception.FilePath
            .Should().Be(GatesDisplayPath);

        exception.FileVersion
            .Should().Be(2);
    }

    [Fact]
    public async Task ReadAsync_StoreThrowsSynchronously_ReturnsFaultedTaskAndNextCallReadsAgain()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var failure = new IOException("The disk is gone.");
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Throws(failure).Once()
            .Then.Returns(Task.FromResult<VersionedReadResult<GatesConfig>?>(null));
        var reader = CreateReader();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var firstRead = reader.ReadAsync(store, cancellationToken);
        var firstAct = () => firstRead;
        var firstException = (await firstAct.Should().ThrowAsync<IOException>()).Which;
        var second = await reader.ReadAsync(store, cancellationToken);

        // Assert
        firstException
            .Should().BeSameAs(failure);

        second.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));
    }

    [Fact]
    public async Task ReadAsync_StartingCallCanceledButReadCompletes_LaterCallReturnsCompletedResult()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var pendingRead = new TaskCompletionSource<VersionedReadResult<GatesConfig>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(pendingRead.Task);
        var reader = CreateReader();
        using var startingCancellation = new CancellationTokenSource();

        // Act
        var startingRead = reader.ReadAsync(store, startingCancellation.Token);
        await startingCancellation.CancelAsync();
        var startingAct = () => startingRead;
        await startingAct.Should().ThrowAsync<OperationCanceledException>();
        pendingRead.SetResult(null);
        var later = await reader.ReadAsync(store, TestContext.Current.CancellationToken);

        // Assert
        later.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedOnceExactly();
    }

    // The read-only view that the reader returns for the built-in defaults, built independently of the reader.
    private static Dictionary<string, Dictionary<string, TransitionGate>> DefaultProfilesView()
    {
        return GatesConfig.CreateDefault().Profiles!.ToDictionary(
            profile => profile.Key,
            profile => profile.Value.ToDictionary(gate => gate.Key,
                gate => new TransitionGate(gate.Value.Approval, gate.Value.Checklist)));
    }

    private static GatesConfigReader CreateReader()
    {
        return new GatesConfigReader(new BuiltInWorkflowProfileCatalog());
    }

    // Bounded on its own, so a read that never starts fails the test instead of hanging until the test token is
    // canceled.
    private static async Task WaitUntilReadStartedAsync(IKitStore store, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!Fake.GetCalls(store).Any(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync)))
        {
            if (stopwatch.Elapsed > ReadStartTimeout)
            {
                throw new TimeoutException($"The read of gates.json did not start within {ReadStartTimeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        }
    }

    private KitStore CreateStore(FormatRegistry? registry = null)
    {
        return TestKitStores.Create(_kitStorePath, formatRegistry: registry);
    }

    private void WriteGates(string content)
    {
        _repository.WriteFile(content, KitStorePaths.RootPath, KitStorePaths.GatesFileName);
    }

    private void WriteGatesWithProfiles(string profiles)
    {
        WriteGates($$"""{ {{Header}}, "profiles": {{profiles}} }""");
    }
}
