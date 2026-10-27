using System.Text.Json;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Core.Tests.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow;

/// <summary>
/// Provides a temporary git repository with a <c>.shadekit</c> directory, a real <see cref="KitStore" />, and a
/// <see cref="PhaseWorkflow" /> with the real engine and the built-in profiles; the resolver of the current feature,
/// the git information, and the gate provider are faked, so no test starts <c>git</c>.
/// </summary>
internal sealed class PhaseWorkflowTestContext : IDisposable
{
    public const string FeatureId = "gh-142-login-sso";

    public const string UserName = "Jane Doe";

    public const string UserEmail = "jane@example.com";

    public const string FeatureFilePath = ".shadekit/features/gh-142-login-sso/feature.json";

    // 2026-10-20 13:30:45 UTC is 15:30:45 in UTC+02.
    public static readonly DateTimeOffset UtcNow = new DateTimeOffset(2026, 10, 20, 13, 30, 45, TimeSpan.Zero);

    public static readonly DateTimeOffset LocalNow =
        new DateTimeOffset(2026, 10, 20, 15, 30, 45, TimeSpan.FromHours(2));

    public const string LocalNowText = "2026-10-20T15:30:45+02:00";

    private static readonly TimeZoneInfo UtcPlusTwo =
        TimeZoneInfo.CreateCustomTimeZone("Test UTC+02", TimeSpan.FromHours(2), "Test UTC+02", "Test UTC+02");

    public PhaseWorkflowTestContext()
    {
        RepositoryRootPath = Repository.CreateGitRoot();
        Repository.CreateDirectory(KitStorePaths.RootPath);

        SelectFeature(FeatureId);
        A.CallTo(() => GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .Returns(new GitIdentity(UserName, UserEmail));
        A.CallTo(() => GateProvider.GetGateAsync(A<IKitStore>._, A<WorkflowProfile>._, A<string>._,
                A<CancellationToken>._))
            .Returns(TransitionGate.Default);

        TimeProvider.SetLocalTimeZone(UtcPlusTwo);
    }

    public TempKitDirectory Repository { get; } = new TempKitDirectory();

    public string RepositoryRootPath { get; }

    public ICurrentFeatureResolver CurrentFeatureResolver { get; } = A.Fake<ICurrentFeatureResolver>();

    public IGitRepositoryInfo GitRepositoryInfo { get; } = A.Fake<IGitRepositoryInfo>();

    public ITransitionGateProvider GateProvider { get; } = A.Fake<ITransitionGateProvider>();

    public FakeTimeProvider TimeProvider { get; } = new FakeTimeProvider(UtcNow);

    /// <summary>
    /// Gets or sets the format registry of the stores that <see cref="CreateStore" /> creates.
    /// </summary>
    public FormatRegistry Registry { get; set; } = FormatRegistry.CreateDefault();

    /// <summary>
    /// Gets or sets the warnings that the line ending resolver of the stores returns for every written file.
    /// </summary>
    public KitWarning[] WriteWarnings { get; set; } = [];

    /// <summary>
    /// Builds the phase fields of a feature of the specified profile as JSON members: <c>currentPhase</c>,
    /// <c>phases</c> with the specified states for the first phases of the profile in profile order,
    /// <c>pendingTransition</c> if given, and <c>history</c>.
    /// </summary>
    public static string PhaseFields(WorkflowProfile profile, string currentPhase, string? pendingTransition,
        string[] states, string history = "[]")
    {
        var phases = string.Join(", ", states.Select((state, index) => $"\"{profile.Phases[index].Name}\": \"{state}\""));
        var pending = pendingTransition is null ? string.Empty : $", \"pendingTransition\": \"{pendingTransition}\"";

        return $"\"currentPhase\": \"{currentPhase}\", \"phases\": {{ {phases} }}{pending}, \"history\": {history}";
    }

    public void Dispose()
    {
        Repository.Dispose();
    }

    public KitStore CreateStore()
    {
        return TestKitStores.Create(Path.Combine(RepositoryRootPath, KitStorePaths.RootPath),
            FixedLineEnding.CreateWriter(LineEnding.Lf, WriteWarnings), Registry);
    }

    /// <summary>
    /// Creates a real store whose serializer throws the specified exception when a feature document is serialized,
    /// so writing <c>feature.json</c> fails after every check passed.
    /// </summary>
    public KitStore CreateStoreFailingFeatureWrite(Exception exception)
    {
        var serializer = A.Fake<IVersionedJsonSerializer>(options =>
            options.Wrapping(new VersionedJsonSerializer(Registry)));
        A.CallTo(() => serializer.Serialize(A<FeatureDocument>._, FormatIds.Feature))
            .Throws(exception);

        return new KitStore(Path.Combine(RepositoryRootPath, KitStorePaths.RootPath), serializer, Registry,
            FixedLineEnding.CreateWriter(LineEnding.Lf, WriteWarnings));
    }

    public PhaseWorkflow CreateWorkflow()
    {
        return new PhaseWorkflow(CurrentFeatureResolver, new BuiltInWorkflowProfileCatalog(), new PhaseEngine(),
            GateProvider, GitRepositoryInfo, TimeProvider);
    }

    /// <summary>
    /// Makes the resolver return the specified feature as current feature, selected by the branch, with the specified
    /// warnings; <see langword="null" /> resolves no current feature.
    /// </summary>
    public void SelectFeature(string? featureId, params KitWarning[] warnings)
    {
        A.CallTo(() => CurrentFeatureResolver.ResolveAsync(A<IKitStore>._, A<CancellationToken>._))
            .Returns(new CurrentFeatureResolution(featureId, featureId is null ? null : CurrentFeatureOrigin.Branch,
                warnings));
    }

    public void SetGate(string targetPhase, TransitionGate gate)
    {
        A.CallTo(() => GateProvider.GetGateAsync(A<IKitStore>._, A<WorkflowProfile>._, targetPhase,
                A<CancellationToken>._))
            .Returns(gate);
    }

    /// <summary>
    /// Writes <c>features/&lt;featureId&gt;/feature.json</c> with the specified phase fields, given as JSON members
    /// without a leading comma, for example <c>"currentPhase": "refine"</c>; an empty string writes no phase fields.
    /// </summary>
    public void WriteFeature(string phaseFields, string profile = WorkflowProfileNames.Feature,
        string status = "active", string featureId = FeatureId, string? recordedId = null)
    {
        var phaseFieldsText = phaseFields.Length == 0 ? string.Empty : $",\n  {phaseFields}";

        WriteKitFile(
            $$"""
              {
                "schemaVersion": 1,
                "minEngineVersion": "0.1.0",
                "id": "{{recordedId ?? featureId}}",
                "title": "Login SSO",
                "profile": "{{profile}}",
                "source": { "kind": "text" },
                "createdAt": "2026-10-20T15:30:00+02:00",
                "createdBy": { "name": "Jane Doe", "email": "jane@example.com" },
                "status": "{{status}}"{{phaseFieldsText}}
              }
              """,
            KitStorePaths.FeaturesDirName, featureId, KitStorePaths.FeatureFileName);
    }

    public void WriteOverride(string featureId)
    {
        WriteOverrideContent($$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "featureId": "{{featureId}}" }""");
    }

    public void WriteOverrideContent(string content)
    {
        WriteKitFile(content, KitStorePaths.FeaturesDirName, KitStorePaths.CurrentFeatureFileName);
    }

    public void WriteKitFile(string content, params string[] kitRelativePathParts)
    {
        Repository.WriteFile(content, [KitStorePaths.RootPath, .. kitRelativePathParts]);
    }

    public bool OverrideExists()
    {
        return File.Exists(Path.Combine(RepositoryRootPath, KitStorePaths.RootPath, KitStorePaths.FeaturesDirName,
            KitStorePaths.CurrentFeatureFileName));
    }

    public string ReadFeatureText()
    {
        return Repository.ReadFileText(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, FeatureId,
            KitStorePaths.FeatureFileName);
    }

    public async Task<FeatureDocument> ReadFeatureAsync()
    {
        var result = await CreateStore()
            .ReadJsonAsync<FeatureDocument>(KitStorePaths.GetFeatureFilePath(FeatureId), FormatIds.Feature,
                FormatAccess.Read, TestContext.Current.CancellationToken);

        return result?.Document ?? throw new InvalidOperationException("feature.json is missing.");
    }

    /// <summary>
    /// Gets the names of the members of the <c>phases</c> object of <c>feature.json</c> in the order of the file.
    /// </summary>
    public string[] ReadPhaseNamesInFileOrder()
    {
        using var document = JsonDocument.Parse(ReadFeatureText());

        return [.. document.RootElement.GetProperty("phases").EnumerateObject().Select(x => x.Name)];
    }

    public void VerifyIdentityNotRead()
    {
        A.CallTo(() => GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    public void SetIdentityMissing()
    {
        A.CallTo(() => GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .Throws(new GitIdentityNotConfiguredException(RepositoryRootPath, ["user.email"]));
    }
}
