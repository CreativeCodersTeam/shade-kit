using CreativeCoders.ShadeKit.Core.Versions.Exceptions;
using CreativeCoders.ShadeKit.Core.Versions.Warnings;

namespace CreativeCoders.ShadeKit.Core.Versions;

/// <summary>
/// Checks whether the running engine fits the version that the kit skills expect.
/// </summary>
public interface IEngineVersionChecker
{
    /// <summary>
    /// Checks the version of the running engine against the specified expected version.
    /// </summary>
    /// <remarks>
    /// The engine fits if its major and minor versions equal those of <paramref name="expectedVersion" />. The patch
    /// version, the prerelease label, and the build metadata of <paramref name="expectedVersion" /> take no part in
    /// the check. A prerelease engine that does not fit is used anyway and gets an
    /// <see cref="EnginePrereleaseVersionMismatchWarning" />.
    /// </remarks>
    /// <param name="expectedVersion">The SemVer 2.0.0 version that the kit skills expect, for example <c>1.2.0</c>.</param>
    /// <returns>The result of the check with the versions and its warnings.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="expectedVersion" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidSemanticVersionException">
    /// <paramref name="expectedVersion" /> is not a valid SemVer 2.0.0 version.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The version of the running engine is not a valid SemVer 2.0.0 version, which is a defect of the engine build.
    /// </exception>
    /// <exception cref="EngineVersionMismatchException">
    /// The engine is a release, and its major or minor version differs from that of
    /// <paramref name="expectedVersion" />.
    /// </exception>
    EngineVersionCheckResult Check(string expectedVersion);
}
