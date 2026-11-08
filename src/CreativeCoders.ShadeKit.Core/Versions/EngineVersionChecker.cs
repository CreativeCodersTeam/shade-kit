using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Versions.Exceptions;
using CreativeCoders.ShadeKit.Core.Versions.Warnings;

namespace CreativeCoders.ShadeKit.Core.Versions;

/// <summary>
/// Checks whether the running engine, whose version an <see cref="IEngineVersionProvider" /> provides, fits the
/// version that the kit skills expect.
/// </summary>
/// <param name="engineVersionProvider">The provider of the running engine version.</param>
public sealed class EngineVersionChecker(IEngineVersionProvider engineVersionProvider) : IEngineVersionChecker
{
    private readonly IEngineVersionProvider _engineVersionProvider = Ensure.NotNull(engineVersionProvider);

    /// <inheritdoc />
    public EngineVersionCheckResult Check(string expectedVersion)
    {
        Ensure.NotNull(expectedVersion);

        if (!SemanticVersion.TryParse(expectedVersion, out var expected))
        {
            throw new InvalidSemanticVersionException(expectedVersion);
        }

        var engineVersionText = _engineVersionProvider.Version;

        if (!SemanticVersion.TryParse(engineVersionText, out var engine))
        {
            throw new InvalidOperationException(
                $"The engine version '{engineVersionText}' is not a valid SemVer 2.0.0 version, so it cannot be " +
                "checked. The engine build is defective.");
        }

        var comparison = (engine.Major, engine.Minor).CompareTo((expected.Major, expected.Minor));

        if (comparison == 0)
        {
            return new EngineVersionCheckResult(engineVersionText, expectedVersion, []);
        }

        var direction = comparison < 0
            ? EngineVersionMismatchDirection.EngineOlder
            : EngineVersionMismatchDirection.EngineNewer;

        if (!engine.IsPrerelease)
        {
            throw new EngineVersionMismatchException(engineVersionText, expectedVersion, direction);
        }

        return new EngineVersionCheckResult(engineVersionText, expectedVersion,
            [new EnginePrereleaseVersionMismatchWarning(engineVersionText, expectedVersion, direction)]);
    }
}
