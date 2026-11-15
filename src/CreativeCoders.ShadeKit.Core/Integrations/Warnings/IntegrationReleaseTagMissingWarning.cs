using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Integrations.Warnings;

/// <summary>
/// Represents a warning that the release tag of the engine version does not exist in the marketplace repository, so
/// the integrations are installed from a branch instead, which may not fit the engine.
/// </summary>
/// <remarks>
/// A build of an untagged commit, for example of <c>main</c>, has a release version whose tag does not exist yet.
/// </remarks>
/// <param name="EngineVersion">The version of the running engine, for example <c>0.8.1</c>.</param>
/// <param name="Tag">The release tag that does not exist, for example <c>v0.8.1</c>.</param>
/// <param name="Ref">The branch from which the integrations are installed, for example <c>main</c>.</param>
public sealed record IntegrationReleaseTagMissingWarning(string EngineVersion, string Tag, string Ref) : KitWarning;
