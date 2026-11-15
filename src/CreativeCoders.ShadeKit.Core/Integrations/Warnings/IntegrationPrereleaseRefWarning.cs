using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Integrations.Warnings;

/// <summary>
/// Represents a warning that the engine is a prerelease, so the integrations are installed from a branch instead of
/// the release tag of the engine version.
/// </summary>
/// <param name="EngineVersion">The version of the running engine, for example <c>0.8.0-beta.1</c>.</param>
/// <param name="Ref">The branch from which the integrations are installed, for example <c>main</c>.</param>
public sealed record IntegrationPrereleaseRefWarning(string EngineVersion, string Ref) : KitWarning;
