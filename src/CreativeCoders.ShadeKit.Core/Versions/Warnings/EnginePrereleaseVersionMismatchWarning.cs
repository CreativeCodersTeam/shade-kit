using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Versions.Warnings;

/// <summary>
/// Represents a warning that the major or minor version of a prerelease engine differs from that of the expected
/// version; the engine is used anyway because it is a prerelease.
/// </summary>
/// <param name="EngineVersion">The version of the running engine, for example <c>1.3.0-beta.1</c>.</param>
/// <param name="ExpectedVersion">The expected version as it was passed to the check.</param>
/// <param name="Direction">
/// One of the enumeration values that specifies whether the engine is older or newer than the expected version.
/// </param>
public sealed record EnginePrereleaseVersionMismatchWarning(
    string EngineVersion,
    string ExpectedVersion,
    EngineVersionMismatchDirection Direction)
    : KitWarning;
