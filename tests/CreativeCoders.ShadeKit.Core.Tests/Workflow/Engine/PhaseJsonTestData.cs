using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

/// <summary>
/// Provides feature documents and their JSON for the tests of the phase state types, which are serialized only as
/// part of <c>feature.json</c>.
/// </summary>
internal static class PhaseJsonTestData
{
    public const string FilePath = ".shadekit/features/gh-142-login-sso/feature.json";

    public static VersionedJsonSerializer CreateSerializer()
    {
        return new VersionedJsonSerializer(FormatRegistry.CreateDefault());
    }

    public static FeatureDocument Deserialize(string json)
    {
        return CreateSerializer().Deserialize<FeatureDocument>(json, FormatIds.Feature, FilePath, false).Document;
    }

    public static FeatureDocument CreateDocument()
    {
        return new FeatureDocument
        {
            Id = "gh-142-login-sso",
            Title = "Login SSO",
            Profile = WorkflowProfileNames.Feature,
            Source = new FeatureSource { Kind = FeatureSourceKind.Text },
            CreatedAt = new DateTimeOffset(2026, 10, 20, 15, 30, 0, TimeSpan.FromHours(2)),
            CreatedBy = new FeatureCreator { Name = "Jane Doe", Email = "jane@example.com" }
        };
    }

    public static string CreateJson(string extraField)
    {
        return $$"""
                 {
                   "schemaVersion": 1,
                   "minEngineVersion": "0.1.0",
                   "id": "gh-142-login-sso",
                   "title": "Login SSO",
                   "profile": "feature",
                   "source": { "kind": "text" },
                   "createdAt": "2026-10-20T15:30:00+02:00",
                   "createdBy": { "name": "Jane Doe", "email": "jane@example.com" },
                   {{extraField}}
                 }
                 """;
    }

    public static string CreateHistoryJson(string entry)
    {
        return CreateJson($$""" "history": [ {{entry}} ] """);
    }
}
