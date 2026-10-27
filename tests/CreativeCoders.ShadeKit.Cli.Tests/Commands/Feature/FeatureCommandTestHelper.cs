using System.Text.Json;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using FakeItEasy;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;

/// <summary>
/// Provides the fakes and the test data that the unit tests of the feature commands share.
/// </summary>
internal static class FeatureCommandTestHelper
{
    public const string FeatureId = "gh-142-login-sso";

    public static FormatWarning UpgradeWarning { get; } =
        new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/config.json", 1, 2);

    /// <summary>
    /// Creates a test console that is wide enough that long warning messages are not wrapped.
    /// </summary>
    public static TestConsole CreateWideConsole()
    {
        var console = new TestConsole();
        console.Profile.Width = 1000;

        return console;
    }

    public static IKitStoreFactory CreateKitStoreFactory(IKitStore? kitStore = null)
    {
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Returns(kitStore ?? A.Fake<IKitStore>());

        return kitStoreFactory;
    }

    public static IKitStoreFactory CreateThrowingKitStoreFactory(Exception exception)
    {
        var kitStoreFactory = A.Fake<IKitStoreFactory>();
        A.CallTo(() => kitStoreFactory.Create()).Throws(exception);

        return kitStoreFactory;
    }

    /// <summary>
    /// Creates a <c>feature.json</c> document without phase fields, like one of an older engine; a document with the
    /// status <see cref="FeatureStatus.Done" /> gets the phase fields of a completed profile instead, because a done
    /// feature without phase fields is invalid.
    /// </summary>
    public static FeatureDocument CreateDocument(string id = FeatureId, string title = "Login SSO",
        FeatureStatus status = FeatureStatus.Active, FeatureSource? source = null,
        string profile = WorkflowProfileNames.Feature)
    {
        var document = new FeatureDocument
        {
            SchemaVersion = 1,
            MinEngineVersion = "0.1.0",
            Id = id,
            Title = title,
            Profile = profile,
            Source = source ?? new FeatureSource
            {
                Kind = FeatureSourceKind.Url,
                Value = "https://github.com/acme/shop/issues/142"
            },
            CreatedAt = new DateTimeOffset(2026, 10, 20, 15, 30, 0, TimeSpan.FromHours(2)),
            CreatedBy = new FeatureCreator { Name = "Jane Doe", Email = "jane@example.com" },
            Status = status
        };

        if (status == FeatureStatus.Done && new BuiltInWorkflowProfileCatalog().TryGetProfile(profile, out var known))
        {
            document.CurrentPhase = known.Phases[^1].Name;
            document.Phases = new OrderedDictionary<string, PhaseState>(
                known.Phases.Select(x => KeyValuePair.Create(x.Name, PhaseState.Approved)));
            document.History = [];
        }

        return document;
    }

    /// <summary>
    /// Reads the normalized phase state of the specified document as <see cref="IFeatureStore" /> does, with the real
    /// <see cref="PhaseEngine" /> and the built-in profiles.
    /// </summary>
    public static FeaturePhaseState ReadPhaseState(FeatureDocument document, string featureId = FeatureId)
    {
        var profile = new BuiltInWorkflowProfileCatalog().GetProfile(document.Profile);

        return new PhaseEngine().ReadState(profile,
            new StoredPhaseState(featureId, $".shadekit/features/{featureId}/feature.json", document.Status,
                document.CurrentPhase, document.Phases, document.PendingTransition));
    }

    public static JsonDocument ParseOutput(StringWriter jsonOutput)
    {
        return JsonDocument.Parse(jsonOutput.ToString());
    }

    public static string[] GetPropertyNames(JsonElement element)
    {
        return [.. element.EnumerateObject().Select(x => x.Name)];
    }

    public static string?[] GetWarningCodes(JsonElement root)
    {
        return [.. root.GetProperty("warnings").EnumerateArray().Select(x => x.GetProperty("code").GetString())];
    }
}
