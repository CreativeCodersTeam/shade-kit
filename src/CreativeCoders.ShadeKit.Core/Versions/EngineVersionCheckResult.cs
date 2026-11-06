using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Versions;

/// <summary>
/// Represents the outcome of a successful check of the running engine version against an expected version.
/// </summary>
/// <param name="EngineVersion">
/// The version of the running engine, as the <see cref="Engine.IEngineVersionProvider" /> provides it.
/// </param>
/// <param name="ExpectedVersion">The expected version as it was passed to the check.</param>
/// <param name="Warnings">
/// The warnings of the check, for example an <see cref="Warnings.EnginePrereleaseVersionMismatchWarning" />; an empty
/// list if there were none.
/// </param>
public sealed record EngineVersionCheckResult(
    string EngineVersion,
    string ExpectedVersion,
    IReadOnlyList<KitWarning> Warnings);
