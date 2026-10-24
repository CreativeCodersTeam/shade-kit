using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Templates;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Core.Tests.Formats;
using CreativeCoders.ShadeKit.Core.Tests.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

/// <summary>
/// Provides a temporary git repository with a <c>.shadekit</c> directory, a real <see cref="KitStore" />, and a
/// <see cref="FeatureStore" /> with real collaborators; only <see cref="IGitRepositoryInfo" /> and the line ending
/// resolver of the store are faked, so no test starts <c>git</c> and the written line ending does not depend on the
/// operating system.
/// </summary>
internal sealed class FeatureStoreTestContext : IDisposable
{
    public const string UserName = "Jane Doe";

    public const string UserEmail = "jane@example.com";

    // 2026-10-20 13:30:45 UTC is 15:30:45 in UTC+02.
    public static readonly DateTimeOffset UtcNow = new DateTimeOffset(2026, 10, 20, 13, 30, 45, TimeSpan.Zero);

    public const string LocalCreatedAt = "2026-10-20T15:30:45+02:00";

    public const string TimeBasedIdPrefix = "20261020-1530-";

    private static readonly TimeZoneInfo UtcPlusTwo =
        TimeZoneInfo.CreateCustomTimeZone("Test UTC+02", TimeSpan.FromHours(2), "Test UTC+02", "Test UTC+02");

    public FeatureStoreTestContext()
    {
        RepositoryRootPath = Repository.CreateGitRoot();
        Repository.CreateDirectory(KitStorePaths.RootPath);

        SetBranch("main");
        A.CallTo(() => GitRepositoryInfo.GetIdentityAsync(A<string>._, A<CancellationToken>._))
            .Returns(new GitIdentity(UserName, UserEmail));
    }

    public TempKitDirectory Repository { get; } = new TempKitDirectory();

    public string RepositoryRootPath { get; }

    public IGitRepositoryInfo GitRepositoryInfo { get; } = A.Fake<IGitRepositoryInfo>();

    /// <summary>
    /// Gets or sets the format registry of the stores that <see cref="CreateStore" /> creates.
    /// </summary>
    public FormatRegistry Registry { get; set; } = FormatRegistry.CreateDefault();

    /// <summary>
    /// Gets or sets the line ending with which the stores that <see cref="CreateStore" /> creates write every file.
    /// </summary>
    public LineEnding LineEnding { get; set; } = LineEnding.Lf;

    /// <summary>
    /// Gets or sets the warnings that the line ending resolver of the stores returns for every written file.
    /// </summary>
    public KitWarning[] WriteWarnings { get; set; } = [];

    /// <summary>
    /// Gets or sets the line ending resolver of the stores that <see cref="CreateStore" /> creates; if it is
    /// <see langword="null" />, <see cref="LineEnding" /> and <see cref="WriteWarnings" /> apply to every file.
    /// </summary>
    public ILineEndingResolver? LineEndingResolver { get; set; }

    /// <summary>
    /// Creates the default registry, except that the specified file types are at format version 2 with a no-op
    /// migration from version 1, so the version 1 files of the tests are outdated.
    /// </summary>
    public static FormatRegistry CreateRegistryWithVersion2(params string[] formatIds)
    {
        return new FormatRegistry(FormatRegistry.CreateDefault().Formats.Select(format =>
            formatIds.Contains(format.Id, StringComparer.Ordinal)
                ? new FormatDefinition(format.Id, SampleFormat.CreateMinEngineVersions(2),
                    [new NoOpMigration(format.Id, 1)])
                : format));
    }

    public void Dispose()
    {
        Repository.Dispose();
    }

    public KitStore CreateStore()
    {
        return TestKitStores.Create(Path.Combine(RepositoryRootPath, KitStorePaths.RootPath), CreateWriter(), Registry);
    }

    /// <summary>
    /// Creates a real store whose serializer throws the specified exception when the local override is serialized,
    /// so writing the override fails after the store has checked, and if necessary deleted, the existing override.
    /// </summary>
    public KitStore CreateStoreFailingOverrideWrite(Exception exception)
    {
        var serializer = A.Fake<IVersionedJsonSerializer>(options =>
            options.Wrapping(new VersionedJsonSerializer(Registry)));
        A.CallTo(() => serializer.Serialize(A<CurrentFeatureDocument>._, FormatIds.CurrentFeature))
            .Throws(exception);

        return new KitStore(Path.Combine(RepositoryRootPath, KitStorePaths.RootPath), serializer, Registry,
            CreateWriter());
    }

    public FeatureStore CreateFeatureStore(IShippedTemplateReader? shippedTemplateReader = null)
    {
        var timeProvider = new FakeTimeProvider(UtcNow);
        timeProvider.SetLocalTimeZone(UtcPlusTwo);

        return new FeatureStore(new FeatureIdFactory(timeProvider), new CurrentFeatureResolver(GitRepositoryInfo),
            GitRepositoryInfo, shippedTemplateReader ?? new ShippedTemplateReader());
    }

    public NewFeatureRequest CreateRequest(string source, string? title = null, string? profile = null)
    {
        return new NewFeatureRequest(source, title, profile, RepositoryRootPath);
    }

    public void SetBranch(string? branchName)
    {
        A.CallTo(() => GitRepositoryInfo.GetCurrentBranchAsync(A<string>._, A<CancellationToken>._))
            .Returns(branchName);
    }

    public void WriteConfig(string fields)
    {
        WriteKitFile($$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0"{{fields}} }""",
            KitStorePaths.ConfigFileName);
    }

    public void WriteRequestTemplate(string content)
    {
        WriteKitFile(content, KitStorePaths.TemplatesDirName, KitStorePaths.RequestFileName);
    }

    /// <summary>
    /// Writes <c>features/&lt;featureId&gt;/feature.json</c>; its <c>id</c> is <paramref name="recordedId" /> if given,
    /// otherwise the folder name.
    /// </summary>
    public void WriteFeature(string featureId, string title = "Login SSO", string profile = "feature",
        string status = "active", string? recordedId = null)
    {
        WriteKitFile(
            $$"""
              {
                "schemaVersion": 1,
                "minEngineVersion": "0.1.0",
                "id": "{{recordedId ?? featureId}}",
                "title": "{{title}}",
                "profile": "{{profile}}",
                "source": { "kind": "text" },
                "createdAt": "2026-10-20T15:30:00+02:00",
                "createdBy": { "name": "Jane Doe", "email": "jane@example.com" },
                "status": "{{status}}"
              }
              """,
            KitStorePaths.FeaturesDirName, featureId, KitStorePaths.FeatureFileName);
    }

    public void CreateFeatureFolder(string featureId)
    {
        Repository.CreateDirectory(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, featureId);
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

    public bool KitFileExists(params string[] kitRelativePathParts)
    {
        return File.Exists(GetKitPath(kitRelativePathParts));
    }

    public bool KitDirectoryExists(params string[] kitRelativePathParts)
    {
        return Directory.Exists(GetKitPath(kitRelativePathParts));
    }

    public string ReadRequest(string featureId)
    {
        return Repository.ReadFileText(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, featureId,
            KitStorePaths.RequestFileName);
    }

    public async Task<FeatureDocument> ReadFeatureFileAsync(string featureId)
    {
        var result = await CreateStore()
            .ReadJsonAsync<FeatureDocument>($"features/{featureId}/feature.json", FormatIds.Feature, FormatAccess.Read,
                TestContext.Current.CancellationToken);

        return result?.Document ?? throw new InvalidOperationException($"feature.json of '{featureId}' is missing.");
    }

    public async Task<string?> ReadOverrideFeatureIdAsync()
    {
        var result = await CreateStore()
            .ReadJsonAsync<CurrentFeatureDocument>("features/current-feature.json", FormatIds.CurrentFeature,
                FormatAccess.Read, TestContext.Current.CancellationToken);

        return result?.Document.FeatureId;
    }

    private TextFileWriter CreateWriter()
    {
        return LineEndingResolver is null
            ? FixedLineEnding.CreateWriter(LineEnding, WriteWarnings)
            : new TextFileWriter(LineEndingResolver);
    }

    private string GetKitPath(string[] kitRelativePathParts)
    {
        return Path.Combine([RepositoryRootPath, KitStorePaths.RootPath, .. kitRelativePathParts]);
    }
}
